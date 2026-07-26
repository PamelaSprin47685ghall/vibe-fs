namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session

/// Production PTY backend: drives bun-pty under the OpenCode/Bun host.
module PtyBackend =
    type private LivePty =
        { Term: obj
          Buffer: StringBuilder
          mutable Closed: bool }

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

    let private live = Dictionary<string, LivePty>()
    let private pending = Dictionary<string, ResizeArray<PtyCommand>>()
    let private gate = obj ()
    let mutable private spawnFn: (string -> string array -> obj -> obj) option = None
    let mutable private loadTask: Task<unit> option = None

    [<Emit("""
        (loader, onOk, onErr) => {
          loader.then((spawn) => onOk(spawn)).catch((err) => onErr(String(err && err.message || err)));
        }
    """)>]
    let private runLoader (loader: JS.Promise<obj>) (onOk: obj -> unit) (onErr: string -> unit) : unit = jsNative

    let private ensureSpawn () : Task<unit> =
        match spawnFn with
        | Some _ -> Task.FromResult(())
        | None ->
            match loadTask with
            | Some t -> t
            | None ->
                let tcs = TaskCompletionSource<unit>()
                loadTask <- Some tcs.Task

                runLoader
                    (loadSpawn ())
                    (fun spawn ->
                        spawnFn <- Some(unbox spawn)
                        tcs.SetResult(()))
                    (fun err -> tcs.SetException(Exception err))

                tcs.Task

    let private getLive (id: PtyId) =
        lock gate (fun () ->
            match live.TryGetValue id.Value with
            | true, value -> Some value
            | false, _ -> None)

    let private drop (id: PtyId) =
        lock gate (fun () ->
            live.Remove id.Value |> ignore
            pending.Remove id.Value |> ignore)

    let private enqueue (id: PtyId) (command: PtyCommand) =
        lock gate (fun () ->
            match pending.TryGetValue id.Value with
            | true, queue -> queue.Add command
            | false, _ ->
                let queue = ResizeArray<PtyCommand>()
                queue.Add command
                pending.[id.Value] <- queue)

    let private takePending (id: PtyId) =
        lock gate (fun () ->
            match pending.TryGetValue id.Value with
            | true, queue ->
                pending.Remove id.Value |> ignore
                queue |> Seq.toList
            | false, _ -> [])

    let private signalName (signal: PtySignal) =
        match signal with
        | PtySignal.Terminate -> "SIGTERM"
        | PtySignal.Kill -> "SIGKILL"
        | PtySignal.Interrupt -> "SIGINT"

    let private spawnSync (command: string) (cwd: string) : obj =
        match spawnFn with
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

            spawn "sh" [| "-lc"; command |] options

    let rec private applyLive (port: PtyPort) (id: PtyId) (command: PtyCommand) =
        match command with
        | PtyCommand.Spawn _ -> ()
        | PtyCommand.Write bytes ->
            match getLive id with
            | None -> enqueue id command
            | Some livePty when livePty.Closed -> ()
            | Some livePty ->
                let text = Encoding.UTF8.GetString bytes
                livePty.Term?write text
        | PtyCommand.Read ->
            match getLive id with
            | None -> enqueue id command
            | Some livePty ->
                let text = livePty.Buffer.ToString()
                livePty.Buffer.Clear() |> ignore
                // Return the buffered output to the caller immediately; do NOT
                // complete the join here. Final exit belongs to onExit.
                port.ReadResult(id, text, livePty.Closed)
        | PtyCommand.Signal signal ->
            match getLive id with
            | None -> enqueue id command
            | Some livePty when livePty.Closed -> ()
            | Some livePty ->
                try
                    livePty.Term?kill(signalName signal)
                with _ ->
                    ()
        | PtyCommand.Resize(width, height) ->
            match getLive id with
            | None -> enqueue id command
            | Some livePty when livePty.Closed -> ()
            | Some livePty ->
                try
                    livePty.Term?resize(width, height)
                with _ ->
                    ()

    let private attach (port: PtyPort) (id: PtyId) (term: obj) =
        let entry =
            { Term = term
              Buffer = StringBuilder()
              Closed = false }

        lock gate (fun () -> live.[id.Value] <- entry)

        term?onData (fun (data: string) ->
            match getLive id with
            | None -> ()
            | Some livePty when livePty.Closed -> ()
            | Some livePty -> livePty.Buffer.Append data |> ignore)
        |> ignore

        term?onExit (fun (_event: obj) ->
            match getLive id with
            | None -> ()
            | Some livePty when livePty.Closed -> ()
            | Some livePty ->
                livePty.Closed <- true
                drop id
                port.Complete(id, Ok PtyOutcome.Closed))
        |> ignore

        for command in takePending id do
            applyLive port id command

    let private handle (port: PtyPort) (id: PtyId) (command: PtyCommand) =
        match command with
        | PtyCommand.Spawn(cmd, cwd) ->
            let load = ensureSpawn ()

            load.ContinueWith(fun (t: Task<unit>) ->
                if t.IsFaulted then
                    drop id
                    port.Complete(id, Error "PTY spawn load failed")
                else
                    try
                        let term = spawnSync cmd cwd
                        attach port id term
                    with ex ->
                        drop id
                        port.Complete(id, Error(sprintf "PTY spawn failed: %s" ex.Message)))
            |> ignore
        | other -> applyLive port id other

    /// Builds a PtyPort whose handler drives real bun-pty sessions.
    let createPort () : PtyPort =
        let mutable portRef: PtyPort option = None

        let handler id command =
            match portRef with
            | None -> ()
            | Some port -> handle port id command

        let port = PtyPort(handler = handler)
        portRef <- Some port
        port
