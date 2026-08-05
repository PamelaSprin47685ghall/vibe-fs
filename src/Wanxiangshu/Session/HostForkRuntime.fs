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
        | None -> task { return () } :> Task
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

    /// P0-RECOVERY-JOIN-001: permit-gated join. Signature proves FamilyRecoveryPermit.
    /// Consumes root + journalSequence; agent join requires journal (no fake empty permit for pure PTY).
    /// closureDigest not re-checked: authorizeFamilyResume already bound digest into the private
    /// permit token; re-discover would need a full RecoveryClosure recompute this path does not hold.
    /// Production JoinTool / JoinInterpreter must use this path — not bare Join.
    member this.JoinWithPermit(permit: FamilyRecoveryPermit, ?timeoutMs: int) : Task<Result<RunCompletion, ForkError>> =
        let root = FamilyRecoveryPermit.root permit
        let permitSeq = FamilyRecoveryPermit.journalSequence permit

        if root <> parentId then
            task {
                return
                    Error(
                        ForkError.NotFound(
                            sprintf
                                "family recovery permit root mismatch: permit=%s runtime=%s"
                                (SessionId.value root)
                                (SessionId.value parentId)
                        )
                    )
            }
        else
            match journal with
            | None ->
                // Agent join with FamilyRecoveryPermit is journal-backed. Pure PTY must not
                // present a family permit (no synthetic empty FamilyReady).
                task {
                    return
                        Error(
                            ForkError.NotFound
                                "family recovery permit requires journal; pure PTY join must not use JoinWithPermit"
                        )
                }
            | Some durable ->
                let currentSeq = JournalRevision.value (AgentJournal.revision durable)

                // Permit was sealed at recovery authorize time. Journal must not have
                // been replaced by an older stream (current < permit). Growth (current > permit)
                // is expected while join waits; only regression fails closed.
                if currentSeq < permitSeq then
                    task {
                        return
                            Error(
                                ForkError.NotFound(
                                    sprintf
                                        "family recovery permit journalSequence stale: permit=%d current=%d"
                                        permitSeq
                                        currentSeq
                                )
                            )
                    }
                else
                    this.Join(?timeoutMs = timeoutMs)

    /// EXEC-009 / P0-RECOVERY-JOIN-001: projection-first join after family recovery.
    /// Prefer JoinWithPermit from production JoinTool (permit token required).
    /// Durable `HandleProjection.joinable` is the fact source; mailbox wakes waiters.
    /// Agent payloads from the mailbox are discarded and the loop re-reads projection.
    /// PTY stays mailbox-driven (EXEC-015). Abandoned surfaces via tryConsumeDurable.
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

        let tryConsumeDurable (durable: AgentJournal) : Result<RunCompletion, ForkError> option =
            let projection = AgentJournal.handleProjection durable parentId

            // EXEC-009: Abandoned is durable and not joinable. Surface it before
            // waiting so Join does not hang or misreport Completed.
            let abandoned =
                HandleProjection.linkedChildren projection
                |> List.tryPick (fun record ->
                    match record.Lifecycle, HandleId.tryAgent record.Handle with
                    | HandleLifecycle.Abandoned reason, Some agentHandleId ->
                        let agentId = AgentHandleId.value agentHandleId

                        let reasonText =
                            match reason with
                            | HandleAbandonReason.ParentCancelled -> "ParentCancelled"
                            | HandleAbandonReason.DeadlineExceeded -> "DeadlineExceeded"
                            | HandleAbandonReason.HostSessionGone -> "HostSessionGone"

                        Some(Error(ForkError.Abandoned(agentId, reasonText)))
                    | _ -> None)

            match abandoned with
            | Some result -> Some result
            | None ->
                let joinable = HandleProjection.joinable projection

                let rec attempt records =
                    match records with
                    | [] -> None
                    | record :: rest ->
                        match HandleId.tryAgent record.Handle with
                        | None -> attempt rest
                        | Some agentHandleId ->
                            let agentId = AgentHandleId.value agentHandleId

                            match HandleCompletionCodec.tryRead durable record agentId with
                            | Error err -> Some(Error(ForkError.NotFound err))
                            | Ok None ->
                                // P0-RECOVERY-JOIN-001: hollow CompletedAwaitingJoin without
                                // blob is not joinable. Never synthesize aborted/CANCELLED.
                                match record.Lifecycle with
                                | HandleLifecycle.CompletedAwaitingJoin cell ->
                                    match cell.Kind with
                                    | HandleCompletionKind.Terminal
                                    | HandleCompletionKind.SendFailure ->
                                        Some(Error(ForkError.TerminalMaterializationFailed agentId))
                                    | HandleCompletionKind.Cancelled -> attempt rest
                                | _ -> attempt rest
                            | Ok(Some completion) ->
                                match record.Lifecycle with
                                | HandleLifecycle.CompletedAwaitingJoin durableCell ->
                                    let body = HandleCompletionCodec.encodeOutcome completion.RunId completion.Outcome

                                    match
                                        JoinableCompletion.tryFromDurableCompleted
                                            agentId
                                            record.Handle
                                            record.ChildSessionId
                                            durableCell.Kind
                                            (Some body)
                                    with
                                    | Error _ -> attempt rest
                                    | Ok _ ->
                                        match HandleController.consume durable parentId record.Handle with
                                        | Ok _ -> Some(Ok completion)
                                        | Error AlreadyRetired -> attempt rest
                                        | Error(NotJoinable _) -> attempt rest
                                        | Error(AppendFailed err) -> Some(Error(ForkError.NotFound err))
                                | _ -> attempt rest

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
                    | Some result -> return result
                    | None ->
                        if remainingMs <= 0 then
                            return Error ForkError.TimedOut
                        else
                            // Snapshot revision under gate, then recheck durable before waiting.
                            let _, fromRev = durable.SnapshotWithRevision

                            match tryConsumeDurable durable with
                            | Some result -> return result
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
                                    | Some result -> return result
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
