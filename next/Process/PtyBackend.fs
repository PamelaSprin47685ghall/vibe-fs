namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session

/// Production PTY backend: drives bun-pty under the OpenCode/Bun host.
/// All per-port backend state lives inside the record built by createPort so
/// that two PtyPorts never share live/pending/spawn state.
module PtyBackend =
    type private LivePty =
        { Term: obj
          Buffer: StringBuilder
          mutable Closed: bool
          Exit: TaskCompletionSource<unit> }

    type private PendingEntry = PtyCommand * TaskCompletionSource<Result<unit, string>> option

    type private BackendState =
        { Live: Dictionary<string, LivePty>
          Pending: Dictionary<string, ResizeArray<PendingEntry>>
          Gate: obj
          mutable SpawnFn: obj option
          mutable LoadTask: Task<unit> option }

        static member Create() =
            { Live = Dictionary()
              Pending = Dictionary()
              Gate = obj ()
              SpawnFn = None
              LoadTask = None }

    [<Emit("""
        (async () => {
          const candidates = ['bun-pty', process.cwd() + '/node_modules/bun-pty'];
          let lastErr;
          for (const spec of candidates) {
            try {
              const m = await import(spec);
              const spawn = m.spawn || (m.default && m.default.spawn) || m;
              if (typeof spawn === 'function') return spawn;
            } catch (err) { lastErr = err; }
          }
          throw lastErr || new Error('bun-pty spawn not found');
        })()
    """)>]
    let private loadSpawn () : JS.Promise<obj> = jsNative

    [<Emit("$0.then((spawn) => $1(spawn)).catch((err) => $2(String(err && err.message || err)))")>]
    let private runLoader (loader: JS.Promise<obj>) (onOk: obj -> unit) (onErr: string -> unit) : unit = jsNative

    let private ensureSpawn (state: BackendState) : Task<unit> =
        match state.SpawnFn with
        | Some _ -> Task.FromResult(())
        | None ->
            match state.LoadTask with
            | Some t -> t
            | None ->
                let tcs = TaskCompletionSource<unit>()
                state.LoadTask <- Some tcs.Task

                runLoader
                    (loadSpawn ())
                    (fun spawn ->
                        state.SpawnFn <- Some(unbox spawn)
                        tcs.SetResult(()))
                    (fun err -> tcs.SetException(Exception err))

                tcs.Task

    let private getLive (state: BackendState) (id: PtyId) =
        lock state.Gate (fun () ->
            let mutable value = Unchecked.defaultof<LivePty>

            if state.Live.TryGetValue(id.Value, &value) then
                Some value
            else
                None)

    let private drop (state: BackendState) (id: PtyId) =
        lock state.Gate (fun () ->
            state.Live.Remove id.Value |> ignore
            let mutable queue = Unchecked.defaultof<ResizeArray<PendingEntry>>

            if state.Pending.TryGetValue(id.Value, &queue) then
                state.Pending.Remove id.Value |> ignore
                queue |> Seq.toList
            else
                [])

    let private failPending (entries: PendingEntry list) (reason: string) =
        for (_, tcsOpt) in entries do
            tcsOpt |> Option.iter (fun tcs -> tcs.SetResult(Error reason))

    let private enqueue
        (state: BackendState)
        (id: PtyId)
        (command: PtyCommand)
        (tcsOpt: TaskCompletionSource<Result<unit, string>> option)
        =
        lock state.Gate (fun () ->
            let mutable queue = Unchecked.defaultof<ResizeArray<PendingEntry>>

            if state.Pending.TryGetValue(id.Value, &queue) then
                queue.Add(command, tcsOpt)
            else
                let newQueue = ResizeArray<PendingEntry>()
                newQueue.Add(command, tcsOpt)
                state.Pending.[id.Value] <- newQueue)

    let private takePending (state: BackendState) (id: PtyId) =
        lock state.Gate (fun () ->
            let mutable queue = Unchecked.defaultof<ResizeArray<PendingEntry>>

            if state.Pending.TryGetValue(id.Value, &queue) then
                state.Pending.Remove id.Value |> ignore
                queue |> Seq.toList
            else
                [])

    [<Emit("(() => { try { process.kill(-$0.pid, $1); } catch (_) { process.kill($0.pid, $1); } })()")>]
    let private killProcessTree (term: obj) (signal: string) : unit = jsNative

    let private signalName (signal: PtySignal) =
        match signal with
        | PtySignal.Terminate -> "SIGTERM"
        | PtySignal.Kill -> "SIGKILL"
        | PtySignal.Interrupt -> "SIGINT"

    [<Emit("$0('sh', ['-lc', $1], $2)")>]
    let private invokeSpawn (spawn: obj) (command: string) (options: obj) : obj = jsNative

    let private spawnSync (state: BackendState) (command: string) (cwd: string) : obj =
        match state.SpawnFn with
        | None -> failwith "bun-pty is not loaded"
        | Some spawn ->
            let cwdValue =
                if String.IsNullOrEmpty cwd then
                    emitJsExpr () "process.cwd()"
                else
                    box cwd

            let options =
                createObj
                    [ "name", box "xterm-256color"
                      "cols", box 80
                      "rows", box 24
                      "cwd", cwdValue ]

            invokeSpawn spawn command options

    let rec private applyLive
        (state: BackendState)
        (port: PtyPort)
        (id: PtyId)
        (command: PtyCommand)
        : Task<Result<unit, string>> =
        task {
            match command with
            | PtyCommand.Spawn _ -> return Ok()
            | PtyCommand.Write bytes ->
                match getLive state id with
                | None ->
                    // Pre-attach write: park the caller's Task until the
                    // process attaches (or the spawn fails and flushes it).
                    let tcs = TaskCompletionSource<Result<unit, string>>()
                    enqueue state id command (Some tcs)
                    return! tcs.Task
                | Some livePty when livePty.Closed -> return Error "PTY closed"
                | Some livePty ->
                    try
                        let text = Encoding.UTF8.GetString bytes
                        livePty.Term?write text
                        return Ok()
                    with ex ->
                        return Error ex.Message
            | PtyCommand.Read ->
                match getLive state id with
                | None ->
                    enqueue state id command None
                    return Ok()
                | Some livePty ->
                    let text = livePty.Buffer.ToString()
                    livePty.Buffer.Clear() |> ignore
                    // Return the buffered output immediately; do NOT complete
                    // the join. Final exit belongs to onExit.
                    port.ReadResult(id, text, livePty.Closed)
                    return Ok()
            | PtyCommand.Signal signal ->
                match getLive state id with
                | None ->
                    enqueue state id command None
                    return Ok()
                | Some livePty when livePty.Closed -> return Ok()
                | Some livePty ->
                    try
                        killProcessTree livePty.Term (signalName signal)
                        return Ok()
                    with ex ->
                        return Error ex.Message
            | PtyCommand.Resize(width, height) ->
                match getLive state id with
                | None ->
                    enqueue state id command None
                    return Ok()
                | Some livePty when livePty.Closed -> return Ok()
                | Some livePty ->
                    try
                        livePty.Term?resize(width, height)
                        return Ok()
                    with _ ->
                        return Ok()
        }

    let private attach
        (state: BackendState)
        (port: PtyPort)
        (id: PtyId)
        (term: obj)
        (exitTcs: TaskCompletionSource<unit>)
        =
        let entry =
            { Term = term
              Buffer = StringBuilder()
              Closed = false
              Exit = exitTcs }

        lock state.Gate (fun () -> state.Live.[id.Value] <- entry)

        // Bridge the backend's per-process exit TCS into the port so CloseAll
        // can await it without the backend reaching into port dicts.

        term?onData (fun (data: string) ->
            match getLive state id with
            | None -> ()
            | Some livePty when livePty.Closed -> ()
            | Some livePty -> livePty.Buffer.Append data |> ignore)
        |> ignore

        term?onExit (fun (_event: obj) ->
            match getLive state id with
            | None -> ()
            | Some livePty when livePty.Closed -> ()
            | Some livePty ->
                livePty.Closed <- true
                let residual = livePty.Buffer.ToString()
                livePty.Buffer.Clear() |> ignore
                livePty.Exit.SetResult(())
                let pending = drop state id
                failPending pending "PTY exited before command was applied"
                port.FailRead(id, "PTY exited before read completed")
                // Residual buffer at exit becomes the join completion outcome.
                port.Complete(
                    id,
                    Ok(
                        if String.IsNullOrEmpty residual then
                            PtyOutcome.Closed
                        else
                            residual
                    )
                ))
        |> ignore

        if not (port.Exists id) then
            try
                killProcessTree term "SIGKILL"
            with _ ->
                ()

        for (command, tcsOpt) in takePending state id do
            task {
                let! result = applyLive state port id command
                tcsOpt |> Option.iter (fun t -> t.SetResult result)
            }
            |> ignore

    let private handle
        (state: BackendState)
        (port: PtyPort)
        (id: PtyId)
        (command: PtyCommand)
        : Task<Result<unit, string>> =
        task {
            match command with
            | PtyCommand.Spawn(cmd, cwd) ->
                let exitTcs = TaskCompletionSource<unit>()
                port.RegisterExitTask(id, exitTcs.Task)

                try
                    do! ensureSpawn state
                    let term = spawnSync state cmd cwd
                    attach state port id term exitTcs
                    return Ok()
                with ex ->
                    try
                        exitTcs.SetResult(())
                    with _ ->
                        ()

                    let msg = sprintf "PTY spawn failed: %s" ex.Message
                    // Flush any parked reader and pending pre-attach writes.
                    port.FailRead(id, msg)

                    for (_, tcsOpt) in takePending state id do
                        tcsOpt |> Option.iter (fun t -> t.SetResult(Error msg))

                    drop state id |> ignore
                    port.Complete(id, Error msg)
                    return Error msg
            | other -> return! applyLive state port id other
        }

    /// Builds a PtyPort whose handler drives real bun-pty sessions. Each call
    /// yields a port with fully isolated backend state.
    let createPort () : PtyPort =
        let state = BackendState.Create()
        let mutable portRef: PtyPort option = None

        let handler (id: PtyId) (command: PtyCommand) : Task<Result<unit, string>> =
            match portRef with
            | None -> Task.FromResult(Ok())
            | Some port -> handle state port id command

        let port = PtyPort(handler = handler)
        portRef <- Some port
        port
