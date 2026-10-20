namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// DSL-state-combination: physical — spawn fn + load task are runtime resource slots
/// DSL-class: PhysicalHandle — process-owned spawn loader and live PTY session registry; owner PtySupervisor, law PTY lifecycle, proof pty-supervisor.
type PtySupervisor =
    { Gate: obj
      mutable SpawnFn: obj option
      mutable LoadTask: Task<unit> option
      Sessions: Dictionary<PtyId, PtySession> }

module PtySupervisor =

    /// PTY-READ-FIRST-BYTE: how long a read waits for an open terminal to say anything before it
    /// answers "". A slice, not a budget: the criterion is the byte arriving (`onData` resolves the
    /// parked reader immediately), and this only bounds silence so a quiet terminal still answers.
    [<Literal>]
    let PtyReadFirstByteMs = 250

    let create () : PtySupervisor =
        { Gate = obj ()
          SpawnFn = None
          LoadTask = None
          Sessions = Dictionary<PtyId, PtySession>() }

    let add (supervisor: PtySupervisor) (id: PtyId) (session: PtySession) : unit =
        lock supervisor.Gate (fun () -> supervisor.Sessions.[id] <- session)

    let private tryGetUnlocked (supervisor: PtySupervisor) (id: PtyId) : PtySession option =
        // DSL-MUTABLE: algorithm-scratch — byref out-slot for TryGetValue
        let mutable value = Unchecked.defaultof<PtySession>

        if supervisor.Sessions.TryGetValue(id, &value) then
            Some value
        else
            None

    let tryGet (supervisor: PtySupervisor) (id: PtyId) : PtySession option =
        lock supervisor.Gate (fun () -> tryGetUnlocked supervisor id)

    let get (supervisor: PtySupervisor) (id: PtyId) : PtySession =
        match tryGet supervisor id with
        | Some s -> s
        | None -> failwithf "Unknown PTY id: %s" id.Value

    let remove (supervisor: PtySupervisor) (id: PtyId) : unit =
        lock supervisor.Gate (fun () -> supervisor.Sessions.Remove id |> ignore)

    let list (supervisor: PtySupervisor) : PtyId list =
        lock supervisor.Gate (fun () -> supervisor.Sessions.Keys |> Seq.toList)

    let private ensureSessionUnlocked (supervisor: PtySupervisor) (id: PtyId) : PtySession =
        match tryGetUnlocked supervisor id with
        | Some s -> s
        | None ->
            let s = PtySession.create id.Value null
            supervisor.Sessions.[id] <- s
            s

    let signalName (signal: PtySignal) : string =
        match signal with
        | PtySignal.Terminate -> "SIGTERM"
        | PtySignal.Kill -> "SIGKILL"
        | PtySignal.Interrupt -> "SIGINT"
        | PtySignal.Hangup -> "SIGHUP"
        | PtySignal.Quit -> "SIGQUIT"
        | PtySignal.User1 -> "SIGUSR1"
        | PtySignal.User2 -> "SIGUSR2"

    /// The physical terminal is wanxiangli/pty: real tty, first-byte reads, process-group
    /// signals and close-with-sweep. This module only routes commands and results.
    [<Import("Terminal", "wanxiangli/pty")>]
    let private terminalApi: obj = jsNative

    [<Emit("$0.open($1)")>]
    let private openTerminal (_api: obj) (_options: obj) : obj = jsNative

    [<Emit("$0.read($1)")>]
    let private readTerminal (_terminal: obj) (_waitMs: int) : JS.Promise<obj> = jsNative

    [<Emit("$0.wait()")>]
    let private waitTerminal (_terminal: obj) : JS.Promise<obj> = jsNative

    [<Emit("$0.close($1)")>]
    let private closeTerminal (_terminal: obj) (_graceMs: int) : JS.Promise<obj> = jsNative

    [<Emit("$0.then($1)")>]
    let private whenDone (_promise: JS.Promise<obj>) (_next: obj -> unit) : unit = jsNative

    let private kindOf (result: obj) : string = string result?kind

    let private errorText (result: obj) : string = string result?error?message

    /// Library signal names (TERM, KILL, ...) for wanxiangli/pty.
    let private terminalSignal (signal: PtySignal) : string =
        match signal with
        | PtySignal.Terminate -> PtySignal.TermName
        | PtySignal.Kill -> PtySignal.KillName
        | PtySignal.Interrupt -> PtySignal.IntName
        | PtySignal.Hangup -> PtySignal.HupName
        | PtySignal.Quit -> PtySignal.QuitName
        | PtySignal.User1 -> PtySignal.User1Name
        | PtySignal.User2 -> PtySignal.User2Name

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

    let private startSpawnLoad (supervisor: PtySupervisor) : Task<unit> =
        match supervisor.LoadTask with
        | Some t -> t
        | None ->
            let tcs = TaskCompletionSource<unit>()
            supervisor.LoadTask <- Some tcs.Task

            runLoader
                (loadSpawn ())
                (fun spawn ->
                    supervisor.SpawnFn <- Some(unbox spawn)
                    tcs.SetResult(()))
                (fun err -> tcs.SetException(Exception err))

            tcs.Task

    let ensureSpawn (supervisor: PtySupervisor) : Task<unit> =
        match supervisor.SpawnFn with
        | Some _ -> Task.FromResult(())
        | None -> startSpawnLoad supervisor

    let private resolveCwd (cwd: string) : obj =
        if String.IsNullOrEmpty cwd then
            emitJsExpr () "process.cwd()"
        else
            box cwd

    let private opened (result: obj) : Task<obj> =
        if kindOf result = "Opened" then
            Task.FromResult(result?terminal)
        else
            failwith (errorText result)

    let spawn (supervisor: PtySupervisor) (command: string) (cwd: string) : Task<obj> =
        match supervisor.SpawnFn with
        | None -> failwith "bun-pty is not loaded"
        | Some driver ->
            createObj
                [ "driver", driver
                  "command", box command
                  "cwd", resolveCwd cwd
                  "cols", box 80
                  "rows", box 24 ]
            |> openTerminal terminalApi
            |> opened

    let failPending
        (entries: (PtyCommand * TaskCompletionSource<Result<unit, string>> option) list)
        (reason: string)
        : unit =
        for (_, tcsOpt) in entries do
            tcsOpt |> Option.iter (fun tcs -> tcs.SetResult(Error reason))

    let takePending
        (supervisor: PtySupervisor)
        (id: PtyId)
        : (PtyCommand * TaskCompletionSource<Result<unit, string>> option) list =
        lock supervisor.Gate (fun () ->
            match tryGetUnlocked supervisor id with
            | Some s ->
                let entries = s.Pending |> Seq.toList
                s.Pending.Clear()
                entries
            | None -> [])

    let drop
        (supervisor: PtySupervisor)
        (id: PtyId)
        : (PtyCommand * TaskCompletionSource<Result<unit, string>> option) list =
        lock supervisor.Gate (fun () ->
            // DSL-MUTABLE: algorithm-scratch — byref out-slot for TryGetValue in drop
            let mutable session = Unchecked.defaultof<PtySession>

            if supervisor.Sessions.TryGetValue(id, &session) then
                supervisor.Sessions.Remove id |> ignore
                session.Pending |> Seq.toList
            else
                [])

    let private commandResult (result: obj) : Result<unit, string> =
        match kindOf result with
        | "Closed" -> Error "PTY exited before command was applied"
        | "Failed" -> Error(errorText result)
        | _ -> Ok()

    let private writeBackend (session: PtySession) (bytes: byte[]) =
        Task.FromResult(commandResult (session.Backend?write(Encoding.UTF8.GetString bytes)))

    let private deliverRead (port: PtyPort) (id: PtyId) (result: obj) =
        if kindOf result = "Read" then
            port.ReadResult(id, string result?output, unbox<bool> result?closed)

    let private readBackend (port: PtyPort) (id: PtyId) (session: PtySession) =
        // PTY-READ-FIRST-BYTE: wanxiangli/pty answers a read on an empty open terminal with the
        // first byte that arrives, or "" after this bound; the answer reaches the parked port read.
        whenDone (readTerminal session.Backend PtyReadFirstByteMs) (deliverRead port id)
        Task.FromResult(Ok())

    let private signalBackend (session: PtySession) (signal: PtySignal) =
        Task.FromResult(commandResult (session.Backend?signal(terminalSignal signal)))

    let private resizeBackend (session: PtySession) (width: int) (height: int) =
        session.Backend?resize(width, height) |> ignore
        Task.FromResult(Ok())

    let private applyBackendCommand
        (supervisor: PtySupervisor)
        (port: PtyPort)
        (id: PtyId)
        (session: PtySession)
        (command: PtyCommand)
        =
        match command with
        | PtyCommand.Write bytes -> writeBackend session bytes
        | PtyCommand.Read -> readBackend port id session
        | PtyCommand.Signal signal -> signalBackend session signal
        | PtyCommand.Resize(width, height) -> resizeBackend session width height
        | PtyCommand.Spawn _ -> Task.FromResult(Ok())

    let private pendingWriteTcs (command: PtyCommand) =
        match command with
        | PtyCommand.Write _ ->
            let tcs = TaskCompletionSource<Result<unit, string>>()
            Some tcs
        | _ -> None

    let private awaitPending (tcsOpt: TaskCompletionSource<Result<unit, string>> option) =
        task {
            match tcsOpt with
            | Some tcs -> return! tcs.Task
            | None -> return Ok()
        }

    let private enqueuePending (supervisor: PtySupervisor) (session: PtySession) (command: PtyCommand) =
        let tcsOpt = pendingWriteTcs command
        lock supervisor.Gate (fun () -> session.Pending.Add(command, tcsOpt))
        awaitPending tcsOpt

    let private sessionOrCreate (supervisor: PtySupervisor) (id: PtyId) =
        lock supervisor.Gate (fun () ->
            match tryGetUnlocked supervisor id with
            | Some s -> s
            | None ->
                let s = PtySession.create id.Value null
                supervisor.Sessions.[id] <- s
                s)

    let applyLive
        (supervisor: PtySupervisor)
        (port: PtyPort)
        (id: PtyId)
        (command: PtyCommand)
        : Task<Result<unit, string>> =
        task {
            let session = sessionOrCreate supervisor id

            if session.Closed then
                return Ok()
            elif isNull session.Backend then
                return! enqueuePending supervisor session command
            else
                return! applyBackendCommand supervisor port id session command
        }

    let private residualOrClosed (residual: string) =
        if String.IsNullOrEmpty residual then
            PtyOutcome.Closed
        else
            residual

    let private finishSession (supervisor: PtySupervisor) (port: PtyPort) (id: PtyId) (residual: string) =
        match tryGet supervisor id with
        | None -> ()
        | Some s when s.Closed -> ()
        | Some s ->
            s.Closed <- true
            s.ExitCompleted <- true
            s.ExitCompletion.SetResult(())
            let pending = drop supervisor id
            failPending pending "PTY exited before command was applied"
            port.FailRead(id, "PTY exited before read completed")
            port.Complete(id, Ok(residualOrClosed residual))

    let private residualOf (result: obj) : string =
        if kindOf result = "Read" then string result?output else ""

    let private onSessionExit (supervisor: PtySupervisor) (port: PtyPort) (id: PtyId) (term: obj) =
        whenDone (readTerminal term 0) (fun result -> finishSession supervisor port id (residualOf result))

    let private closeOrphan (term: obj) =
        if not (isNull term) then
            closeTerminal term 0 |> ignore

    let private closeUnlessClosed (session: PtySession) =
        if not session.Closed then
            closeOrphan session.Backend

    let attach
        (supervisor: PtySupervisor)
        (port: PtyPort)
        (id: PtyId)
        (term: obj)
        (exitTcs: TaskCompletionSource<unit>)
        : unit =
        let pending =
            lock supervisor.Gate (fun () ->
                let placeholder = ensureSessionUnlocked supervisor id

                let live =
                    { placeholder with
                        Backend = term
                        Closed = false
                        ExitCompletion = exitTcs
                        ExitCompleted = false
                        Pending = ResizeArray<_>() }

                supervisor.Sessions.[id] <- live
                placeholder.Pending |> Seq.toList)

        whenDone (waitTerminal term) (fun _ -> onSessionExit supervisor port id term)

        if not (port.Exists id) then
            closeOrphan term

        for (command, tcsOpt) in pending do
            task {
                let! result = applyLive supervisor port id command
                tcsOpt |> Option.iter (fun t -> t.SetResult result)
            }
            |> ignore

    let cancelAll (supervisor: PtySupervisor) : unit =
        let sessions =
            lock supervisor.Gate (fun () -> supervisor.Sessions.Values |> Seq.toList)

        for session in sessions do
            closeUnlessClosed session
