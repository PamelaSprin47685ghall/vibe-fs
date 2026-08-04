namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Microsoft.FSharp.Control
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Journal
open Wanxiangshu.Session.AgentRoleIdentity

/// Bridges real child sessions to the existing completion mailbox.
/// Fork / Reuse / Pty operations live in extension files (semantic split).
type HostForkRuntime
    (
        parentId: SessionId,
        sessions: ISessionHostPort,
        ?journal: AgentJournal,
        ?onChildCreated: string -> AgentRole -> SessionId -> unit,
        ?onChildCreatedDir: string -> SessionId -> string option -> unit,
        ?ptyPort: PtyPort,
        ?directoryFor: string -> string option,
        ?onRunStarted: SessionId -> AgentRole -> string option -> unit,
        ?parentWorkRecordFor: SessionId -> string option,
        ?childWorkRecordFor: SessionId -> string option,
        ?sessionSnapshot: ISessionSnapshotPort,
        ?cancelSignals: SessionId seq -> unit,
        ?publishToMailbox: bool,
        /// REVIEW-007: a Manager's own review fork opens a barrier for the forked
        /// Reviewer. The Orchestrator's runtime keeps this off — it opens barriers
        /// itself (ORCH-006) — so exactly one writer owns each barrier.
        ?openReviewBarrier: bool,
        /// REVIEW-007: the Git tree hash of a forked Reviewer's directory, used to
        /// open the barrier. `None` for a directory with no readable tree: the
        /// Reviewer's verdict then fails closed under REVIEW-008, which is the
        /// correct outcome for a review without a tree.
        ?treeHashFor: string -> GitTreeHash option
    ) as this =
    let runtime = ForkRuntime(publishToMailbox = defaultArg publishToMailbox true)
    let children = Dictionary<string, SessionId>()
    let pendingRuns = Dictionary<string, PendingHostRun>()
    let ptyRuns = HashSet<string>()
    let mutable lastPtyId: string option = None
    let gate = obj ()

    let directoryOf = defaultArg directoryFor (fun _ -> None)
    let childCreated = defaultArg onChildCreated (fun _ _ _ -> ())
    let childCreatedDir = defaultArg onChildCreatedDir (fun _ _ _ -> ())
    let runStarted = defaultArg onRunStarted (fun _ _ _ -> ())
    let parentWorkRecordOf = defaultArg parentWorkRecordFor (fun _ -> None)
    let childWorkRecordOf = defaultArg childWorkRecordFor (fun _ -> None)
    let cancelSignals = defaultArg cancelSignals (fun _ -> ())

    let ptyPortInstance = defaultArg ptyPort (PtyBackend.createPort ())
    let parentKey = SessionId.value parentId

    let sendChildPrompt =
        HostForkRunLifecycle.childPromptSender sessions parentId journal directoryOf

    let sendBusyNudge = HostForkBusyNudge.sender sessions parentId journal directoryOf

    let parentAbortToken = Pty.registerParentAbort parentKey (fun () -> this.Cancel())

    do
        ptyPortInstance.AddMailboxSender(fun completion ->
            let owned = lock gate (fun () -> ptyRuns.Contains completion.RunId)

            if owned then
                // A PtyPort can be shared by multiple runtimes. Its sender fan-out
                // must not turn another runtime's exit into this runtime's join.
                runtime.PublishCompletion completion
                runtime.UnregisterPty completion.RunId)

    let completedTask () : Task = task { return () } :> Task
    let mutable recoveryTask: Task = completedTask ()

    let restoreChildren () =
        match journal with
        | None -> completedTask ()
        | Some j ->
            HostForkRestart.restoreLinkedChildren
                runtime
                sessionSnapshot
                j
                parentId
                children
                childCreatedDir
                directoryOf

    do recoveryTask <- restoreChildren ()

    member internal _.Runtime = runtime
    member internal _.Children = children
    member internal _.PendingRuns = pendingRuns
    member internal _.PtyRuns = ptyRuns

    /// 最近创建的 PTY id。fork-pty 写/读/signal 无 agent 时作用于它
    /// （canary DSL 的「最近创建」语义；`TryPty ""` 的解析目标）。
    member internal _.LastPtyId
        with get () = lock gate (fun () -> lastPtyId)
        and set value = lock gate (fun () -> lastPtyId <- value)

    member internal _.Gate = gate
    member internal _.Sessions = sessions
    member internal _.Journal = journal
    member internal _.SessionSnapshot = sessionSnapshot
    member internal _.ParentId = parentId
    member internal _.ParentKey = parentKey
    member internal _.PtyPort = ptyPortInstance
    member internal _.DirectoryOf = directoryOf
    member internal _.RunStarted = runStarted
    member internal _.ChildCreated = childCreated
    member internal _.ChildCreatedDir = childCreatedDir
    member internal _.ParentWorkRecordOf = parentWorkRecordOf
    member internal _.ChildWorkRecordOf = childWorkRecordOf
    member internal _.SendChildPrompt = sendChildPrompt
    member internal _.SendBusyNudge = sendBusyNudge
    member internal _.ParentAbortToken = parentAbortToken
    member internal _.OpenReviewBarrier = defaultArg openReviewBarrier false
    member internal _.TreeHashFor = defaultArg treeHashFor (fun _ -> None)

    member _.IsRetiredHandle(agentId: string) =
        journal
        |> Option.map (fun durable ->
            HandleProjection.isRetired
                (HandleController.agentHandle agentId)
                (AgentJournal.handleProjection durable parentId))

    member this.AwaitRecovery() =
        task {
            try
                do! recoveryTask
            with _ ->
                ()
        }

    member this.Complete(run: PendingHostRun, outcome: TerminalOutcome) =
        let workRecord =
            match outcome with
            | TerminalOutcome.Completed _ -> childWorkRecordOf run.ChildId
            | _ -> None

        // EXEC-009's durable completion is written by `HostForkRunLifecycle.complete`
        // (which now takes the journal), so this path only delivers the mailbox
        // result. The single-assignment fold absorbs the cancel-path completion
        // that `HandleController.cancelChildren` writes first.
        HostForkRunLifecycle.complete gate pendingRuns journal parentId sessions run outcome workRecord

    member this.InstallRun(agentId: string, childId: SessionId, role: AgentRole) =
        let run =
            HostForkRunLifecycle.installRun
                gate
                pendingRuns
                journal
                parentId
                sessions
                childWorkRecordOf
                agentId
                childId
                role

        runtime.BindChildSession(agentId, childId)
        runStarted childId role (directoryOf agentId)
        run

    member this.FailRun(run: PendingHostRun, error: string) =
        HostForkRunLifecycle.failRun gate pendingRuns journal parentId sessions run error

    member this.MarkReady(run: PendingHostRun) =
        let workRecord = childWorkRecordOf run.ChildId

        HostForkRunLifecycle.markReady gate pendingRuns journal parentId sessions run workRecord

    member this.Cancel() : unit =
        Async.StartImmediate(
            HostForkChildDispatch.cancelParent
                cancelSignals
                (fun () -> this.AwaitRecovery())
                runtime
                ptyPortInstance
                parentKey
                parentAbortToken
                gate
                pendingRuns
                children
                sessions
                journal
                (journal
                 |> Option.map (fun durable -> AgentJournal.handleProjection durable parentId))
                parentId
                (fun run outcome -> this.Complete(run, outcome))
        )

    /// EXEC-009: projection-first join. Durable `HandleProjection.joinable` is the
    /// fact source; the mailbox only wakes waiters. Agent payloads from the mailbox
    /// are discarded and the loop re-reads the projection. PTY stays mailbox-driven
    /// (EXEC-015).
    ///
    /// Consume = read blob → CAS `HandleRetired`. CommitUnknown / append failure
    /// must not deliver. Concurrent joins: single winner via journal gate.
    ///
    /// Join never waits forever: default budget 600s. On timeout, one final durable
    /// consume is attempted; still empty → TimedOut.
    member this.Join(?timeoutMs: int) : Task<Result<RunCompletion, ForkError>> =
        let budgetMs = defaultArg timeoutMs 600_000

        let isPty runId =
            lock gate (fun () -> ptyRuns.Contains runId)

        let tryConsumeDurable (durable: AgentJournal) : Result<RunCompletion, string> option =
            let joinable =
                HandleProjection.joinable (AgentJournal.handleProjection durable parentId)

            let rec attempt records =
                match records with
                | [] -> None
                | record :: rest ->
                    match HandleId.tryAgent record.Handle with
                    | None -> attempt rest
                    | Some agentHandleId ->
                        let agentId = AgentHandleId.value agentHandleId

                        match HandleCompletionCodec.tryRead durable record agentId with
                        | Error err -> Some(Error err)
                        | Ok None ->
                            // Cancelled / 0.5.1 line without blob: still CAS-retire so
                            // the handle cannot be joined twice after a hollow complete.
                            match HandleController.consume durable parentId record.Handle with
                            | Ok _ ->
                                Some(
                                    Ok
                                        { RunId = "run-" + agentId
                                          AgentId = agentId
                                          AgentName = record.TargetAgent
                                          Role = AgentRoleIdentity.ofRole record.CanonicalRole
                                          Outcome =
                                            AgentCompletion.aborted
                                                agentId
                                                ("run-" + agentId)
                                                (Some(AgentRoleIdentity.ofRole record.CanonicalRole))
                                                (Some record.ChildSessionId)
                                                "CANCELLED"
                                                "handle completed without durable payload"
                                          CompletedAt = System.DateTimeOffset.UtcNow }
                                )
                            | Error AlreadyRetired -> attempt rest
                            | Error(NotJoinable _) -> attempt rest
                            | Error(AppendFailed err) -> Some(Error err)
                        | Ok(Some completion) ->
                            match HandleController.consume durable parentId record.Handle with
                            | Ok _ -> Some(Ok completion)
                            | Error AlreadyRetired -> attempt rest
                            | Error(NotJoinable _) -> attempt rest
                            | Error(AppendFailed err) -> Some(Error err)

            attempt joinable

        /// Race journal revision wake vs mailbox completion (PTY + agent notify).
        /// Choice1 = durable change (re-loop); Choice2 = mailbox result.
        let raceChangeAndMailbox
            (durable: AgentJournal)
            (fromRev: JournalRevision)
            (ms: int)
            : Task<Choice<JournalChange, Result<RunCompletion, ForkError>>> =
            task {
                let changeTask =
                    task {
                        let! change = durable.AwaitChangeFrom fromRev
                        return Choice1Of2 change
                    }

                let mailTask =
                    task {
                        let! joined = runtime.Join(timeoutMs = ms)
                        return Choice2Of2 joined
                    }

                return! emitJsExpr (changeTask, mailTask) "Promise.race([$0, $1])"
            }

        let rec loop (remainingMs: int) : Task<Result<RunCompletion, ForkError>> =
            task {
                match journal with
                | Some durable ->
                    match tryConsumeDurable durable with
                    | Some(Ok completion) -> return Ok completion
                    | Some(Error err) ->
                        return Error(ForkError.NotFound(sprintf "join could not consume handle: %s" err))
                    | None ->
                        if remainingMs <= 0 then
                            return Error ForkError.TimedOut
                        else
                            // Snapshot revision under gate, then recheck durable before waiting.
                            let _, fromRev = durable.SnapshotWithRevision

                            match tryConsumeDurable durable with
                            | Some(Ok completion) -> return Ok completion
                            | Some(Error err) ->
                                return Error(ForkError.NotFound(sprintf "join could not consume handle: %s" err))
                            | None ->
                                let started = DateTimeOffset.UtcNow
                                let! raced = raceChangeAndMailbox durable fromRev remainingMs

                                let elapsed = int (DateTimeOffset.UtcNow - started).TotalMilliseconds

                                let next = max 0 (remainingMs - max 0 elapsed)

                                match raced with
                                | Choice1Of2 _ ->
                                    // Journal advanced — re-read projection (may still be None).
                                    return! loop next
                                | Choice2Of2(Ok completion) when isPty completion.RunId -> return Ok completion
                                | Choice2Of2(Ok _) ->
                                    // Agent mailbox payload is notification only — re-loop.
                                    return! loop next
                                | Choice2Of2(Error ForkError.TimedOut) ->
                                    match tryConsumeDurable durable with
                                    | Some(Ok completion) -> return Ok completion
                                    | Some(Error err) ->
                                        return
                                            Error(
                                                ForkError.NotFound(
                                                    sprintf "join could not consume handle after timeout: %s" err
                                                )
                                            )
                                    | None -> return Error ForkError.TimedOut
                                | Choice2Of2(Error e) -> return Error e
                | None ->
                    if remainingMs <= 0 then
                        return Error ForkError.TimedOut
                    else
                        let! joined = runtime.Join(timeoutMs = remainingMs)
                        return joined
            }

        task {
            do! this.AwaitRecovery()
            return! loop budgetMs
        }

    member this.AwaitAgent(agentId: string) : Task<Result<RunCompletion, string>> =
        task {
            do! this.AwaitRecovery()
            return! runtime.AwaitAgent agentId
        }

    member _.List() = runtime.List()

    member _.TryFindAgent(agentId: string) =
        runtime.List() |> fst |> List.tryFind (fun a -> a.AgentId = agentId)

    /// The Host child session a forked agent id drives.
    ///
    /// ORCH-006 needs it right after a fork, to record `ManagerJobCreated`. The map is
    /// the same one restart recovery repopulates from `HandleLinked.ChildSessionId`, so
    /// a resumed job reads the session the Host actually issued rather than one derived
    /// from the agent id.
    member _.TryChildSession(agentId: string) : SessionId option =
        lock gate (fun () ->
            match children.TryGetValue agentId with
            | true, childId -> Some childId
            | false, _ -> None)

    member _.PendingRunCount = lock gate (fun () -> pendingRuns.Count)
    member _.PendingCompletionCount = runtime.PendingCompletionCount
    member _.IsCancelled = runtime.IsCancelled
