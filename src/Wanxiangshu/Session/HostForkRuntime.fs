namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Microsoft.FSharp.Control
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.Domain.SessionRecovery
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
    let runtime = ForkRuntime()
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
        ptyPortInstance.AddMailboxSender(fun item ->
            let id = PtyJoinItem.ptyId item
            let owned = lock gate (fun () -> ptyRuns.Contains id)

            if owned then
                // A PtyPort can be shared by multiple runtimes. Its sender fan-out
                // must not turn another runtime's exit into this runtime's join.
                runtime.PublishPtyCompletion item
                runtime.UnregisterPty id)
    // GREEN-4: HostForkRuntime does not own recovery. SessionRecoveryWorkflow
    // RestoreHandles → HostForkRestart.restoreLinkedChildren is the sole path.

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

    /// EXEC-009: retired OR abandoned ids must never re-fork under the same handle.
    member _.IsRetiredHandle(agentId: string) =
        journal
        |> Option.map (fun durable ->
            let projection = AgentJournal.handleProjection durable parentId
            let handle = HandleController.agentHandle agentId

            HandleProjection.isRetired handle projection
            || HandleProjection.isAbandoned handle projection)

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
                // GREEN-4: no second recovery ownership; cancel does not start restore.
                (fun () -> Task.FromResult(()))
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

    /// EXEC-009 + EXEC-018 + GREEN-5: agent facts from Journal; PTY from mailbox as PtyJoinItem.
    /// journal=None: agent join fail-closed; pure PTY drain still allowed.
    /// Abandoned items join the same ResultsAvailable batch — never withhold
    /// completed results behind a top-level ForkError.Abandoned.
    /// EXEC-020: PTY stays JoinItem/PtyJoinItem until renderer (no toRunCompletion on batch path).
    member private this.tryDrainAvailable(maxCount: int) : Result<JoinWaitOutcome<JoinItem>, ForkError> option =
        let cap = min maxCount JoinBatch.Max

        if cap <= 0 then
            None
        else
            // Clear agent wakes — they never carry payload (GREEN-5).
            ignore (runtime.DrainAgentWakes JoinBatch.Max)

            match journal with
            | None ->
                // Agent join requires Journal. Pure PTY join may proceed without it.
                let hasActiveAgents =
                    runtime.ActiveRunCount > 0
                    || lock gate (fun () -> pendingRuns.Count > 0)
                    || runtime.PendingCompletionCount > runtime.PendingPtyCount

                if hasActiveAgents then
                    Some(
                        Error(
                            ForkError.NotFound
                                "agent join requires journal; journal=None is fail-closed for agent handles"
                        )
                    )
                else
                    let ptyBatch = runtime.DrainPtyCompletions cap |> List.map JoinItem.ofPtyJoinItem

                    match NonEmptyBatch.tryOfList ptyBatch with
                    | Some batch -> Some(Ok(ResultsAvailable batch))
                    | None -> None
            | Some durable ->
                match JoinDrain.drainFromJournal durable parentId cap with
                | Error e -> Some(Error e)
                | Ok durableBatch ->
                    // PTY channel only (EXEC-015). Leftover stays queued for next join.
                    let remaining = cap - List.length durableBatch

                    let agentItems = durableBatch |> List.map JoinItem.ofAgentRunCompletion

                    let ptyItems =
                        if remaining <= 0 then
                            []
                        else
                            runtime.DrainPtyCompletions remaining |> List.map JoinItem.ofPtyJoinItem

                    match NonEmptyBatch.tryOfList (agentItems @ ptyItems) with
                    | Some batch -> Some(Ok(ResultsAvailable batch))
                    | None -> None

    /// Permit gate shared by JoinWithPermit / JoinAvailableWithPermit.
    member private this.validatePermit(permit: FamilyRecoveryPermit) : Result<unit, ForkError> =
        let root = FamilyRecoveryPermit.root permit
        let permitSeq = FamilyRecoveryPermit.journalSequence permit
        let permitDigest = FamilyRecoveryPermit.closureDigest permit

        if root <> parentId then
            Error(
                ForkError.NotFound(
                    sprintf
                        "family recovery permit root mismatch: permit=%s runtime=%s"
                        (SessionId.value root)
                        (SessionId.value parentId)
                )
            )
        else
            match journal with
            | None ->
                Error(
                    ForkError.NotFound
                        "family recovery permit requires journal; pure PTY join must not use JoinWithPermit"
                )
            | Some durable ->
                let currentSeq = JournalRevision.value (AgentJournal.revision durable)

                if currentSeq < permitSeq then
                    Error(
                        ForkError.NotFound(
                            sprintf
                                "family recovery permit journalSequence stale: permit=%d current=%d"
                                permitSeq
                                currentSeq
                        )
                    )
                else
                    let current =
                        RecoveryClosureProjection.discover
                            root
                            (AgentJournal.snapshot durable).AgentProjections
                            currentSeq

                    if current.Digest <> permitDigest then
                        Error(
                            ForkError.NotFound(
                                sprintf
                                    "family recovery permit closureDigest mismatch: permit=%s current=%s"
                                    permitDigest
                                    current.Digest
                            )
                        )
                    else
                        Ok()

    /// P0-RECOVERY-JOIN-001 + GREEN-4: permit-gated single-result join.
    /// Validates root / journalSequence lower bound / closureDigest only; never starts recovery.
    member this.JoinWithPermit(permit: FamilyRecoveryPermit, ?timeoutMs: int) : Task<Result<RunCompletion, ForkError>> =
        match this.validatePermit permit with
        | Error e -> Task.FromResult(Error e)
        | Ok() -> this.Join(?timeoutMs = timeoutMs)

    /// EXEC-018 batch join under FamilyRecoveryPermit.
    /// GREEN-4: validate permit then drain; does not start RestoreHandles.
    /// Batch carries JoinItem so PtyAborted survives to renderer (EXEC-020).
    member this.JoinAvailableWithPermit
        (permit: FamilyRecoveryPermit, maxCount: int, interrupt: Task<unit>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        match this.validatePermit permit with
        | Error e -> Task.FromResult(Error e)
        | Ok() -> this.JoinAvailable(maxCount, interrupt)

    /// EXEC-017 / EXEC-018: bounded batch join with local interrupt (≠ runtime.Cancel).
    /// GREEN-5: agent facts from Journal; agent mailbox channel is wake-only.
    /// PTY facts from PTY mailbox channel as PtyJoinItem (EXEC-015 / EXEC-020).
    /// journal=None agent join fails closed.
    member this.JoinAvailable
        (maxCount: int, interrupt: Task<unit>)
        : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
        let cap = min (max 0 maxCount) JoinBatch.Max

        let hasWork () =
            runtime.ActiveRunCount > 0
            || runtime.PendingCompletionCount > 0
            || lock gate (fun () -> pendingRuns.Count > 0 || ptyRuns.Count > 0)
            || match journal with
               | None -> false
               | Some durable ->
                   let p = AgentJournal.handleProjection durable parentId

                   not (List.isEmpty (HandleProjection.joinable p))
                   || not (List.isEmpty (HandleProjection.reportableAbandoned p))
                   || not (List.isEmpty (HandleProjection.activeHandles p))

        /// Outer race arms: mailbox wake | journal change | user interrupt.
        /// PulseWake after race so a losing WaitForWake waiter never piles up.
        let rec loop () : Task<Result<JoinWaitOutcome<JoinItem>, ForkError>> =
            task {
                match this.tryDrainAvailable cap with
                | Some result -> return result
                | None ->
                    if runtime.IsCancelled then
                        return Error ForkError.Cancelled
                    elif not (hasWork ()) then
                        return Error ForkError.NothingToJoin
                    else
                        match journal with
                        | Some durable ->
                            let _, fromRev = durable.SnapshotWithRevision

                            match this.tryDrainAvailable cap with
                            | Some result -> return result
                            | None ->
                                // Tagged race arms without nested task{} (dsl-ownership).
                                // kind: 0=wake(+reason), 1=journal change, 2=user interrupt.
                                let wakeTask: Task<obj> =
                                    emitJsExpr
                                        (runtime.WaitForWake())
                                        "$0.then(function (r) { return { kind: 0, reason: r }; })"

                                let changeTask: Task<obj> =
                                    emitJsExpr
                                        (durable.AwaitChangeFrom fromRev)
                                        "$0.then(function () { return { kind: 1 }; })"

                                let userTask: Task<obj> =
                                    emitJsExpr interrupt "$0.then(function () { return { kind: 2 }; })"

                                let! winner =
                                    emitJsExpr (wakeTask, changeTask, userTask) "Promise.race([$0, $1, $2])": Task<obj>

                                runtime.PulseWake()

                                match this.tryDrainAvailable cap with
                                | Some result -> return result
                                | None ->
                                    let kind: int = emitJsExpr winner "$0.kind"

                                    if kind = 0 then
                                        let reason: MailboxWakeReason = emitJsExpr winner "$0.reason"

                                        match reason with
                                        | MailboxCancelled -> return Error ForkError.Cancelled
                                        | CompletionMayBeAvailable
                                        | UserInterrupted -> return! loop ()
                                    elif kind = 2 then
                                        return Ok InterruptedByUserMessage
                                    else
                                        return! loop ()
                        | None ->
                            let! signal = runtime.WaitForSignal interrupt

                            match this.tryDrainAvailable cap with
                            | Some result -> return result
                            | None ->
                                match signal with
                                | MailboxCancelled -> return Error ForkError.Cancelled
                                | UserInterrupted -> return Ok InterruptedByUserMessage
                                | CompletionMayBeAvailable -> return! loop ()
            }

        // GREEN-4: JoinAvailable does not start recovery; permit path already recovered.
        loop ()

    /// Compatibility single-result join (Executor map/reduce agent path).
    /// None is unbounded; Some is an explicit internal waiter budget.
    /// Projects JoinItem → RunCompletion for callers that still need agent Outcome.
    member this.Join(?timeoutMs: int) : Task<Result<RunCompletion, ForkError>> =
        let budgetMs = timeoutMs

        let headAsRunCompletion (batch: NonEmptyBatch<JoinItem>) : RunCompletion =
            match NonEmptyBatch.toList batch |> List.head with
            | AgentItem(AgentCompletedItem payload) ->
                { RunId = payload.RunId
                  AgentId = payload.AgentId
                  AgentName = payload.AgentId
                  Role = payload.Role
                  Outcome = AgentCompleted payload
                  CompletedAt = DateTimeOffset.UtcNow }
            | AgentItem(AgentFailedItem payload) ->
                { RunId = payload.RunId
                  AgentId = payload.AgentId
                  AgentName = payload.AgentId
                  Role = defaultArg payload.Role AgentRole.Executor
                  Outcome = AgentFailed payload
                  CompletedAt = DateTimeOffset.UtcNow }
            | AgentItem(AgentAbandonedItem(agentId, reason)) ->
                { RunId = "abandoned-" + agentId
                  AgentId = agentId
                  AgentName = agentId
                  Role = AgentRole.Executor
                  Outcome = AgentAbandoned(agentId, reason)
                  CompletedAt = DateTimeOffset.UtcNow }
            | PtyItem item -> PtyJoinItem.toRunCompletion item

        task {
            let interrupt =
                match budgetMs with
                | Some milliseconds -> PtyTiming.timerTask milliseconds
                | None ->
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
                        .Task

            let! outcome = this.JoinAvailable(1, interrupt)

            match outcome with
            | Error e -> return Error e
            | Ok InterruptedByUserMessage ->
                // Timer interrupt under legacy Join API → TimedOut after final drain.
                match this.tryDrainAvailable 1 with
                | Some(Ok(ResultsAvailable batch)) -> return Ok(headAsRunCompletion batch)
                | Some(Error e) -> return Error e
                | Some(Ok InterruptedByUserMessage)
                | None -> return Error ForkError.TimedOut
            | Ok(ResultsAvailable batch) -> return Ok(headAsRunCompletion batch)
        }

    member this.AwaitAgent(agentId: string, ?timeoutMs: int) : Task<Result<RunCompletion, string>> =
        runtime.AwaitAgent(agentId, ?timeoutMs = timeoutMs)

    /// Targeted cancel for one forked agent (Executor map/reduce sibling abort).
    /// Completes the pending run cell and aborts the Host child so Join unblocks;
    /// ForkRuntime CTS cancel alone cannot settle Source.Task.
    member this.CancelAgent(agentId: string) : unit =
        runtime.CancelAgent(agentId)

        let pending, childId =
            lock gate (fun () ->
                let run =
                    match pendingRuns.TryGetValue agentId with
                    | true, r -> Some r
                    | false, _ -> None

                let child =
                    match children.TryGetValue agentId with
                    | true, id -> Some id
                    | false, _ -> None

                run, child)

        match pending with
        | Some run -> this.FailRun(run, "cancelled")
        | None -> ()

        match childId with
        | Some id ->
            sessions.AbortSession id
            |> Async.AwaitTask
            |> Async.Ignore
            |> Async.StartImmediate
        | None -> ()

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
