namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session

/// Typed PTY lifecycle boundary. A backend receives commands; completion events
/// are supplied by Complete and share every registered mailbox sender.
type PtyPort
    (?mailboxSender: RunCompletion -> unit, ?handler: PtyBackendHandler, ?agentProvider: unit -> AgentRecord list) as this
    =
    let handler = defaultArg handler (fun _ _ -> Task.FromResult(Ok()))
    let agentProvider = defaultArg agentProvider (fun () -> [])
    let mailboxSenders = ResizeArray<RunCompletion -> unit>()
    let gate = obj ()
    let active = Dictionary<PtyId, PtyHandle * ref<bool>>()
    let closedIds = HashSet<PtyId>()

    let readWaiters =
        Dictionary<PtyId, TaskCompletionSource<Result<string * bool, string>>>()

    let exitTasks = Dictionary<PtyId, Task>()
    do mailboxSender |> Option.iter mailboxSenders.Add

    /// Owner-initiated terminate: sends TERM to the backend ONLY. Does NOT
    /// remove from active, does NOT mark closed, does NOT FailRead, does NOT
    /// publish completion. Completion belongs exclusively to the backend's
    /// onExit → Complete path. (SSOT §7 cleanup policy.)
    let requestTerminate (id: PtyId) =
        let live =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, (_, closed) when not closed.Value -> true
                | _ -> false)

        if live then
            try
                handler id (PtyCommand.Signal PtySignal.Terminate) |> ignore
            with _ ->
                ()

    /// Complete from a backend exit (onExit). This is the ONLY path that
    /// publishes completion to mailbox senders. Removes from active, marks
    /// closed, fails any parked reader, then delivers the completion.
    let completeFromExit (id: PtyId) (outcome: Result<string, string>) =
        let target =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, value -> Some value
                | _ -> None)

        match target with
        | None -> ()
        | Some(handle, closed) ->
            let alreadyClosed =
                lock closed (fun () ->
                    if closed.Value then
                        true
                    else
                        closed.Value <- true
                        false)

            if not alreadyClosed then
                lock gate (fun () ->
                    active.Remove id |> ignore
                    closedIds.Add id |> ignore)

                // Any in-flight read must resolve with an error; the completion
                // below is the authoritative exit outcome delivered to Join.
                this.FailRead(id, "PTY closed before read completed")

                let agentId = defaultArg handle.AgentId id.Value
                let role = defaultArg handle.Role AgentRole.Executor

                let typedOutcome =
                    match outcome with
                    | Ok text -> AgentCompletion.ofSimpleText agentId id.Value role text
                    | Error err -> AgentCompletion.ofSimpleError agentId id.Value role err

                // AgentName derived directly from role; PTY is compiled before the
                // managed-role identity adapter.
                let agentName =
                    sprintf
                        "fast-%s"
                        (role.ToString().ToLowerInvariant())

                let completion =
                    { RunId = id.Value
                      AgentId = agentId
                      AgentName = agentName
                      Role = role
                      Outcome = typedOutcome
                      CompletedAt = DateTimeOffset.UtcNow }

                let senders = lock gate (fun () -> mailboxSenders |> Seq.toList)

                for sender in senders do
                    try
                        sender completion
                    with _ ->
                        ()

    member _.AddMailboxSender(sender: RunCompletion -> unit) =
        lock gate (fun () -> mailboxSenders.Add sender)

    member _.MailboxSender = mailboxSender
    member _.Handler = handler
    member _.AgentProvider = agentProvider

    member this.Fork(command: string, ?agentId: string, ?role: AgentRole, ?ptyId: PtyId, ?cwd: string) : PtyId =
        let id =
            defaultArg ptyId (PtyId("pty-" + Guid.NewGuid().ToString("N").Substring(0, 8)))

        let handle =
            { Id = id
              Command = command
              StartedAt = DateTimeOffset.UtcNow
              AgentId = agentId
              Role = role }

        lock gate (fun () ->
            closedIds.Remove id |> ignore
            active.[id] <- (handle, ref false))

        handler id (PtyCommand.Spawn(command, defaultArg cwd "")) |> ignore
        id

    member this.Exists(id: PtyId) =
        lock gate (fun () -> active.ContainsKey id)

    member this.Known(id: PtyId) =
        lock gate (fun () -> active.ContainsKey id || closedIds.Contains id)

    /// Sends a command to the backend. Returns the backend's outcome so callers
    /// (e.g. SendPty) can surface write errors as tool errors instead of always
    /// succeeding. Completion/exit still belongs to the backend's onExit.
    member this.Send(id: PtyId, command: PtyCommand) : Task<Result<unit, string>> =
        let live, closed =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, (_, c) -> (not c.Value, c.Value)
                | false, _ -> (false, closedIds.Contains id))

        if not live then
            if closed then
                Task.FromResult(Error "PTY closed")
            else
                Task.FromResult(Error(sprintf "Unknown PTY id: %s" id.Value))
        else
            task {
                try
                    return! handler id command
                with ex ->
                    return Error ex.Message
            }

    /// Reads the currently buffered PTY output without completing the join.
    /// At most one read may be in flight per id; a second concurrent Read
    /// returns immediately with an error. A Read after the PTY has closed
    /// returns (output="", closed=true) without parking.
    member this.Read(id: PtyId) : Task<Result<string * bool, string>> =
        let plan =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, (_, closed) when not closed.Value ->
                    if readWaiters.ContainsKey id then
                        AlreadyInProgress
                    else
                        let tcs = TaskCompletionSource<Result<string * bool, string>>()
                        readWaiters.[id] <- tcs
                        Park tcs
                | true, _ -> ClosedImmediate
                | false, _ when closedIds.Contains id -> ClosedImmediate
                | false, _ -> Unknown(sprintf "Unknown PTY id: %s" id.Value))

        match plan with
        | Unknown msg -> Task.FromResult(Error msg)
        | AlreadyInProgress -> Task.FromResult(Error "PTY read already in progress")
        | ClosedImmediate -> Task.FromResult(Ok("", true))
        | Park tcs ->
            handler id PtyCommand.Read |> ignore

            task {
                let! result = tcs.Task
                return result
            }

    /// Resolved by the backend when it has drained the buffer for a Read.
    member _.ReadResult(id: PtyId, output: string, closed: bool) =
        let tcs =
            lock gate (fun () ->
                match readWaiters.TryGetValue id with
                | true, t ->
                    readWaiters.Remove id |> ignore
                    Some t
                | false, _ -> None)

        match tcs with
        | Some t -> t.SetResult(Ok(output, closed))
        | None -> ()

    /// Resolves any parked read waiter with an error. Used by every path that
    /// ends a PTY (close, spawn failure, pending drop, onExit) so a parked
    /// reader never hangs.
    member _.FailRead(id: PtyId, reason: string) =
        let tcs =
            lock gate (fun () ->
                match readWaiters.TryGetValue id with
                | true, t ->
                    readWaiters.Remove id |> ignore
                    Some t
                | false, _ -> None)

        match tcs with
        | Some t -> t.SetResult(Error reason)
        | None -> ()

    /// Bridges a backend per-process exit task into the port so CloseAll can
    /// await process exit without the backend reaching into port dicts.
    member this.RegisterExitTask(id: PtyId, task: Task) =
        lock gate (fun () -> exitTasks.[id] <- task)

    /// Complete from a backend exit (onExit). This is the ONLY path that
    /// publishes completion to mailbox senders.
    member this.Complete(id: PtyId, ?outcome: Result<string, string>) =
        completeFromExit id (defaultArg outcome (Ok PtyOutcome.Closed))
        lock gate (fun () -> exitTasks.Remove id |> ignore)

    /// Owner-initiated close: sends TERM only. Does NOT publish completion —
    /// completion is delivered by the backend's onExit → Complete. The caller
    /// must await the exit (via CloseAll or the registered exit task) for the
    /// process to actually exit and Complete to fire.
    member this.Close(id: PtyId, ?outcome: Result<string, string>) : unit = requestTerminate id

    /// Async owner cleanup: for each active id, send TERM (requestTerminate),
    /// await exit for `termToKillGraceMs` (or the supplied override), then
    /// escalate to KILL. If KILL itself fails, propagate the error instead of
    /// waiting forever. The exitTask resolves via the backend's onExit, which
    /// calls Complete (the only completion-publishing path). See SSOT §7.
    member this.CloseAll(?graceMs: int) : Task<unit> =
        let grace = max 0 (defaultArg graceMs PtyOutcome.termToKillGraceMs)
        let ids = lock gate (fun () -> active.Keys |> Seq.toList)

        task {
            for id in ids do
                requestTerminate id

                let exitTaskOpt =
                    lock gate (fun () ->
                        match exitTasks.TryGetValue id with
                        | true, t -> Some t
                        | false, _ -> None)

                match exitTaskOpt with
                | None -> ()
                | Some exitTask ->
                    let! exited = PtyTiming.raceExit exitTask grace

                    if not exited then
                        // Grace elapsed without exit: escalate to KILL.
                        let! killResult = handler id (PtyCommand.Signal PtySignal.Kill)

                        match killResult with
                        | Error err ->
                            // KILL failed: do NOT wait forever for an exit that
                            // will never come. Propagate the kill error.
                            return raise (InvalidOperationException(sprintf "PTY kill failed for %s: %s" id.Value err))
                        | Ok() ->
                            // KILL sent; await the real onExit which calls
                            // Complete (publishes completion).
                            do! exitTask

                    lock gate (fun () -> exitTasks.Remove id |> ignore)
        }

    member _.List() : AgentRecord list * PtyHandle list =
        let agents = agentProvider ()
        let ptys = lock gate (fun () -> active.Values |> Seq.map fst |> Seq.toList)
        agents, ptys
