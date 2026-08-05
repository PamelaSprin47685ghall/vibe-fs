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

    // RECOVERY-FAMILY: constructor must not start recovery side effects.
    // Linked-child restore is RestoreLinkedHandles under FamilyRecoveryPermit.
    let recoveryStartLock = obj ()
    let mutable recoveryTask: Task option = None

    let restoreChildren () =
        match journal with
        | None -> AsyncSupport.completedTask ()
        | Some j ->
            HostForkRestart.restoreLinkedChildren
                runtime
                sessionSnapshot
                j
                parentId
                children
                childCreatedDir
                directoryOf

    /// Start restore at most once when a permit-holding caller first needs it.
    member private _.EnsureChildRestoreStarted() =
        lock recoveryStartLock (fun () ->
            match recoveryTask with
            | Some t -> t
            | None ->
                let t = restoreChildren ()
                recoveryTask <- Some t
                t)

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

    /// Recovery restore failures must surface. Silent success would let Join
    /// proceed against a half-restored family (P0-RECOVERY-JOIN-001).
    member this.AwaitRecovery() =
        task { do! this.EnsureChildRestoreStarted() }

    /// Explicit restore instruction for SessionRecovery RestoreLinkedHandles.
    member this.RestoreLinkedHandles() : Task = this.EnsureChildRestoreStarted()

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

    /// EXEC-009 + EXEC-018: durable drain via JoinDrain pure path + PTY mailbox.
    /// Abandoned items join the same ResultsAvailable batch — never withhold
    /// completed results behind a top-level ForkError.Abandoned.
    member private this.tryDrainAvailable(maxCount: int) : Result<JoinWaitOutcome<RunCompletion>, ForkError> option =
        let cap = min maxCount JoinBatch.Max

        if cap <= 0 then
            None
        else
            match journal with
            | None ->
                match NonEmptyBatch.tryOfList (runtime.DrainAvailable cap) with
                | Some batch -> Some(Ok(ResultsAvailable batch))
                | None -> None
            | Some durable ->
                match JoinDrain.drainFromJournal durable parentId cap with
                | Error e -> Some(Error e)
                | Ok durableBatch ->
                    // Mailbox: PTY facts (EXEC-015) + agent wake payloads (discard).
                    // Only drain when batch has room; leftover PTY stays queued for next join.
                    let remaining = cap - List.length durableBatch

                    let ptyBatch =
                        if remaining <= 0 then
                            []
                        else
                            let rec pull acc need =
                                if need <= 0 then
                                    List.rev acc
                                else
                                    match runtime.DrainAvailable 1 with
                                    | [] -> List.rev acc
                                    | c :: _ when lock gate (fun () -> ptyRuns.Contains c.RunId) ->
                                        pull (c :: acc) (need - 1)
                                    | _ -> pull acc need

                            pull [] remaining

                    match NonEmptyBatch.tryOfList (durableBatch @ ptyBatch) with
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

    /// P0-RECOVERY-JOIN-001: permit-gated single-result join for legacy interpreters
    /// and internal waiters. Production JoinTool uses JoinAvailable.
    member this.JoinWithPermit(permit: FamilyRecoveryPermit, ?timeoutMs: int) : Task<Result<RunCompletion, ForkError>> =
        match this.validatePermit permit with
        | Error e -> Task.FromResult(Error e)
        | Ok() -> this.Join(?timeoutMs = timeoutMs)

    /// EXEC-018 batch join under FamilyRecoveryPermit.
    /// Production JoinTool uses this path.
    member this.JoinAvailableWithPermit
        (permit: FamilyRecoveryPermit, maxCount: int, interrupt: Task<unit>)
        : Task<Result<JoinWaitOutcome<RunCompletion>, ForkError>> =
        match this.validatePermit permit with
        | Error e -> Task.FromResult(Error e)
        | Ok() -> this.JoinAvailable(maxCount, interrupt)

    /// EXEC-017 / EXEC-018: bounded batch join with local interrupt (≠ runtime.Cancel).
    /// Durable agent: projection is fact source; mailbox/journal are wake only.
    /// PTY: mailbox remains fact source (EXEC-015).
    member this.JoinAvailable
        (maxCount: int, interrupt: Task<unit>)
        : Task<Result<JoinWaitOutcome<RunCompletion>, ForkError>> =
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
        let rec loop () : Task<Result<JoinWaitOutcome<RunCompletion>, ForkError>> =
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

        task {
            do! this.AwaitRecovery()
            return! loop ()
        }

    /// Compatibility single-result join. None is unbounded; Some is an explicit
    /// internal waiter budget. New model callers use JoinAvailable.
    member this.Join(?timeoutMs: int) : Task<Result<RunCompletion, ForkError>> =
        let budgetMs = timeoutMs

        task {
            do! this.AwaitRecovery()

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
                | Some(Ok(ResultsAvailable batch)) -> return Ok(NonEmptyBatch.toList batch |> List.head)
                | Some(Error e) -> return Error e
                | Some(Ok InterruptedByUserMessage)
                | None -> return Error ForkError.TimedOut
            | Ok(ResultsAvailable batch) -> return Ok(NonEmptyBatch.toList batch |> List.head)
        }

    member this.AwaitAgent(agentId: string, ?timeoutMs: int) : Task<Result<RunCompletion, string>> =
        task {
            do! this.AwaitRecovery()
            return! runtime.AwaitAgent(agentId, ?timeoutMs = timeoutMs)
        }

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
