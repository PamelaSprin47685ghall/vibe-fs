namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.OpenCode
open Wanxiangshu.Session

/// Typed PTY lifecycle boundary. A backend receives commands; completion events
/// are supplied by Complete and share every registered mailbox sender.
/// GREEN-5: sender carries PtyJoinItem (physical PTY fact), not agent RunCompletion.
type PtyPort(?mailboxSender: PtyJoinItem -> unit, ?handler: PtyBackendHandler, ?agentProvider: unit -> AgentRecord list) as this
    =
    let handler = defaultArg handler (fun _ _ -> Task.FromResult(Ok()))
    let agentProvider = defaultArg agentProvider (fun () -> [])
    let mailboxSenders = ResizeArray<PtyJoinItem -> unit>()
    let gate = obj ()
    let active = Dictionary<PtyId, PtyHandle * ref<bool>>()
    let closedIds = HashSet<PtyId>()
    /// Owner TERM/KILL requested: next Complete for this id → PtyAborted (EXEC-020).
    let abortPending = HashSet<PtyId>()

    let readWaiters =
        Dictionary<PtyId, TaskCompletionSource<Result<string * bool, string>>>()

    let exitTasks = Dictionary<PtyId, Task>()
    do mailboxSender |> Option.iter mailboxSenders.Add

    /// Owner-initiated terminate: marks abort-pending + sends TERM. Does NOT
    /// remove from active, does NOT mark closed, does NOT FailRead, does NOT
    /// publish completion. Completion belongs exclusively to the backend's
    /// onExit → Complete path, which emits PtyAborted when abort-pending.
    let requestTerminate (id: PtyId) =
        let live =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, (_, closed) when not closed.Value ->
                    abortPending.Add id |> ignore
                    true
                | _ -> false)

        if live then
            try
                handler id (PtyCommand.Signal PtySignal.Terminate) |> ignore
            with _ ->
                ()

    /// Complete from a backend exit (onExit). This is the ONLY path that
    /// publishes completion to mailbox senders. Removes from active, marks
    /// closed, fails any parked reader, then delivers the completion.
    /// EXEC-020: physical abort (owner kill / parent cancel TERM|KILL) → PtyAborted;
    /// natural exit → PtyExited; backend spawn/IO error → PtyFailed.
    let completeFromExit (id: PtyId) (item: PtyJoinItem) =
        let target =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, value -> Some value
                | _ -> None)

        match target with
        | None -> ()
        | Some(_handle, closed) ->
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

                // AGENT-013: PTY DevOps-exclusive. GREEN-5: physical PtyJoinItem only.
                let senders = lock gate (fun () -> mailboxSenders |> Seq.toList)

                for sender in senders do
                    try
                        sender item
                    with _ ->
                        ()

    member _.AddMailboxSender(sender: PtyJoinItem -> unit) =
        lock gate (fun () -> mailboxSenders.Add sender)

    member _.MailboxSender = mailboxSender
    member _.Handler = handler
    member _.AgentProvider = agentProvider

    /// Open a PTY for a managed agent.
    ///
    /// `agent` is required. It used to be an optional `AgentRole` that no caller
    /// supplied, so every completion reported `fast-executor` — PROMPT-008 forbids
    /// inventing a managed name, and the only way to keep that true here is to make
    /// the real one non-optional.
    member this.Fork(command: string, agent: ManagedAgent, ?ptyId: PtyId, ?cwd: string) : PtyId =
        let id =
            defaultArg ptyId (PtyId("pty-" + Guid.NewGuid().ToString("N").Substring(0, 8)))

        let handle =
            { Id = id
              Command = command
              StartedAt = DateTimeOffset.UtcNow
              Agent = agent }

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
    /// TERM/KILL/INT marks abort-pending so onExit → PtyAborted (EXEC-020).
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
            match command with
            | PtyCommand.Signal(PtySignal.Terminate | PtySignal.Kill | PtySignal.Interrupt) ->
                lock gate (fun () -> abortPending.Add id |> ignore)
            | _ -> ()

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
    /// If owner requested terminate (Close/CloseAll/TERM), emits PtyAborted;
    /// else Ok → PtyExited, Error → PtyFailed. Tests may call CompleteAborted
    /// to force abort without the terminate mark.
    member this.Complete(id: PtyId, ?outcome: Result<string, string>) =
        let wasAbort =
            lock gate (fun () ->
                let marked = abortPending.Remove id
                exitTasks.Remove id |> ignore
                marked)

        let item =
            if wasAbort then
                let text =
                    match defaultArg outcome (Ok PtyOutcome.Closed) with
                    | Ok t -> t
                    | Error e -> e

                let msg =
                    if String.IsNullOrWhiteSpace text || text = PtyOutcome.Closed then
                        "PTY aborted"
                    else
                        text

                PtyAborted
                    { PtyId = id.Value
                      Outcome = msg
                      Closed = true
                      Code = "PTY_ABORTED"
                      Message = msg }
            else
                match defaultArg outcome (Ok PtyOutcome.Closed) with
                | Ok text ->
                    PtyExited
                        { PtyId = id.Value
                          Outcome = text
                          Closed = true }
                | Error err ->
                    PtyFailed
                        { PtyId = id.Value
                          Outcome = err
                          Closed = true
                          Code = "ERROR"
                          Message = err }

        completeFromExit id item

    /// Force PtyAborted (tests / callers that already know physical interrupt).
    member this.CompleteAborted(id: PtyId, ?message: string) =
        lock gate (fun () ->
            abortPending.Remove id |> ignore
            exitTasks.Remove id |> ignore)

        let msg = defaultArg message "PTY aborted"

        completeFromExit
            id
            (PtyAborted
                { PtyId = id.Value
                  Outcome = msg
                  Closed = true
                  Code = "PTY_ABORTED"
                  Message = msg })

    /// Owner-initiated close: sends TERM only. Does NOT publish completion —
    /// completion is delivered by the backend's onExit → Complete / CompleteAborted.
    /// The caller must await the exit (via CloseAll or the registered exit task).
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
