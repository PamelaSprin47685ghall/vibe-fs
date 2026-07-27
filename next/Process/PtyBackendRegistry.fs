namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session

/// Live-handle registry and spawn bookkeeping for the production PTY backend.
/// All per-port backend state lives inside the record built by
/// PtyBackend.createPort so that two PtyPorts never share live/pending/spawn
/// state. Split out of PtyBackend.fs so that file stays within the
/// architecture line gate, while the command pipeline (applyLive/attach/handle)
/// and port assembly (createPort) remain in PtyBackend. The completion
/// ownership is unchanged: only the backend's onExit path publishes Complete.
module PtyBackendRegistry =

    type internal LivePty =
        { Term: obj
          Buffer: StringBuilder
          mutable Closed: bool
          Exit: TaskCompletionSource<unit> }

    type internal PendingEntry = PtyCommand * TaskCompletionSource<Result<unit, string>> option

    type internal BackendState =
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
    let internal loadSpawn () : JS.Promise<obj> = jsNative

    [<Emit("$0.then((spawn) => $1(spawn)).catch((err) => $2(String(err && err.message || err)))")>]
    let internal runLoader (loader: JS.Promise<obj>) (onOk: obj -> unit) (onErr: string -> unit) : unit = jsNative

    let internal ensureSpawn (state: BackendState) : Task<unit> =
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

    let internal getLive (state: BackendState) (id: PtyId) =
        lock state.Gate (fun () ->
            let mutable value = Unchecked.defaultof<LivePty>

            if state.Live.TryGetValue(id.Value, &value) then
                Some value
            else
                None)

    let internal drop (state: BackendState) (id: PtyId) =
        lock state.Gate (fun () ->
            state.Live.Remove id.Value |> ignore
            let mutable queue = Unchecked.defaultof<ResizeArray<PendingEntry>>

            if state.Pending.TryGetValue(id.Value, &queue) then
                state.Pending.Remove id.Value |> ignore
                queue |> Seq.toList
            else
                [])

    let internal failPending (entries: PendingEntry list) (reason: string) =
        for (_, tcsOpt) in entries do
            tcsOpt |> Option.iter (fun tcs -> tcs.SetResult(Error reason))

    let internal enqueue
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

    let internal takePending (state: BackendState) (id: PtyId) =
        lock state.Gate (fun () ->
            let mutable queue = Unchecked.defaultof<ResizeArray<PendingEntry>>

            if state.Pending.TryGetValue(id.Value, &queue) then
                state.Pending.Remove id.Value |> ignore
                queue |> Seq.toList
            else
                [])

    [<Emit("(() => { try { process.kill(-$0.pid, $1); } catch (_) { process.kill($0.pid, $1); } })()")>]
    let internal killProcessTree (term: obj) (signal: string) : unit = jsNative

    let internal signalName (signal: PtySignal) =
        match signal with
        | PtySignal.Terminate -> "SIGTERM"
        | PtySignal.Kill -> "SIGKILL"
        | PtySignal.Interrupt -> "SIGINT"
        | PtySignal.Hangup -> "SIGHUP"
        | PtySignal.Quit -> "SIGQUIT"
        | PtySignal.User1 -> "SIGUSR1"
        | PtySignal.User2 -> "SIGUSR2"

    [<Emit("$0('sh', ['-lc', $1], $2)")>]
    let internal invokeSpawn (spawn: obj) (command: string) (options: obj) : obj = jsNative

    let internal spawnSync (state: BackendState) (command: string) (cwd: string) : obj =
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

    let internal applyLive
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

    let internal attach
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
