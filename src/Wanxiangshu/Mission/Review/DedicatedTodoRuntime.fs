namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoAfter
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoProcessReview
open Wanxiangshu.Mission.Finality
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Resources
open Wanxiangshu.Mission.Review
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// HOST-021 / TODO-006/008 / REVIEW-013..017: Host-owned dedicated process reviewer.
/// ensureReview is idempotent and may reenter from after / restart / next todowrite / suicide.
module DedicatedTodoReviewerRuntime =

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let private agentIdOf (dedicatedId: DedicatedReviewerId) =
        "todo-process-reviewer:" + DedicatedReviewerId.value dedicatedId

    let private directoryOf (sessionId: SessionId) =
        match SharedState.SessionDirectories.TryGetValue(SessionId.value sessionId) with
        | true, path -> Some path
        | false, _ -> SharedState.RootWorkspace

    let private reviewerHead (journal: AgentJournal) (reviewerSessionId: SessionId) =
        AgentProjection.tryFind reviewerSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.map XTraceProjection.head
        |> Option.defaultValue 0L

    let private readObligations
        (journal: AgentJournal)
        (blobRef: BlobRef)
        (expected: BlobDigest)
        : Task<Result<ObligationList, string>> =
        taskResult {
            let! body = journal.Writer.BlobWriter.Read blobRef

            if HostDigest.sha256Hex body <> BlobDigest.value expected then
                return! Error "obligation blob digest mismatch"
            else
                return! MagicTodoObligationCodec.tryDecode body
        }

    let private openingRaw (journal: AgentJournal) (life: LifeProjection) : Task<string> =
        task {
            match! journal.Writer.BlobWriter.Read life.OpeningTextRef with
            | Ok body when HostDigest.sha256Hex body = BlobDigest.value life.OpeningTextDigest -> return body
            | _ -> return ""
        }

    let private managerCheckpointLwr
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (life: LifeProjection)
        (reviewFrontier: XTraceCursor)
        : Task<string> =
        task {
            let snapshot = AgentJournal.snapshot journal

            let xTrace =
                AgentProjection.tryFind managerSessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.XTrace)
                |> Option.defaultValue XTraceProjection.empty

            let start =
                ManagerOpeningFloor.workRecordStart
                    life
                    (MagicTodoProjection.tryLife life.LifeId snapshot.AgentProjections.MagicTodo)
                    xTrace
                |> Option.defaultValue life.OpeningCursor

            let range =
                { MagicTodoLwr.BoundedRange.StartInclusive = start
                  MagicTodoLwr.BoundedRange.EndExclusive = reviewFrontier }

            let! record = LifecycleWorkRecordProjection.lifecycleWorkRecordBounded (Some journal) managerSessionId range

            return record |> Option.defaultValue ""
        }

    let private capturePortMessages
        (port: ISessionSnapshotPort)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        : Task<Result<unit, string>> =
        taskResult {
            let! messages =
                port.GetMessages managerSessionId
                |> TaskValue.map (Result.mapError (fun reason -> "snapshot unavailable: " + reason))

            return! XTraceCapture.captureSessionMessages (Some journal) managerSessionId messages
        }

    let private captureManagerSnapshot
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        : Task<Result<unit, string>> =
        match snapshot with
        | None -> Task.FromResult(Ok())
        | Some port -> capturePortMessages port journal managerSessionId

    let private treeHashFromRaw (raw: string) (fallback: GitTreeHash) =
        if String.IsNullOrWhiteSpace raw then
            fallback
        else
            GitTreeHash.create raw

    let private tryReadTreeHash (port: GitTreePort) (fallback: GitTreeHash) =
        try
            treeHashFromRaw (port.GetTreeHash().Trim()) fallback
        with _ex ->
            fallback

    let private treeHash (gitTree: GitTreePort option) (reviewId: TodoReviewId) =
        let fallback =
            GitTreeHash.create (HostDigest.sha256Hex (TodoReviewId.value reviewId))

        match gitTree with
        | None -> fallback
        | Some port -> tryReadTreeHash port fallback

    let private reviewerAgentName (journal: AgentJournal) (managerSessionId: SessionId) =
        PromptAuthorityLedger.activeProfile managerSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.map (fun profile -> profile.SelectedTier)
        |> Option.defaultValue AgentTier.Deep
        |> fun tier -> ManagedAgent.nameOf tier Role.Reviewer

    let private forkRuntime
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        =
        HostForkRuntime(
            managerSessionId,
            sessions,
            ?journal = Some journal,
            onChildCreated =
                (fun _ _ childId ->
                    SharedState.SessionParents.[SessionId.value childId] <- SessionId.value managerSessionId),
            onChildCreatedDir =
                (fun _ childId directory ->
                    directory
                    |> Option.iter (fun path -> SharedState.SessionDirectories.[SessionId.value childId] <- path)),
            directoryFor = (fun _ -> directoryOf managerSessionId),
            ?sessionSnapshot = snapshot,
            managerOpensReviewBarrier = false,
            ownership = HandleOwnership.HostOwnedHidden
        )

    let private currentLife (journal: AgentJournal) (managerSessionId: SessionId) =
        AgentProjection.tryFind managerSessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)

    type private ReusableWorkUnitDecision =
        | AlreadyActive
        | Abandoned
        | Relink of targetAgent: string * byname: string
        | LinkFresh of agentName: string

    let private lifecycleReusableDecision (record: HandleRecord) : ReusableWorkUnitDecision =
        match record.Lifecycle with
        | HandleLifecycle.Active -> AlreadyActive
        | HandleLifecycle.Abandoned _ -> Abandoned
        | HandleLifecycle.CompletedAwaitingJoin _
        | HandleLifecycle.Retired -> Relink(record.TargetAgent, record.Byname)

    let private reusableWorkUnitDecision
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (fallbackAgentName: string)
        : ReusableWorkUnitDecision =
        match Map.tryFind reviewerSessionId snapshot.AgentProjections.HandleByChildSession with
        | None -> LinkFresh fallbackAgentName
        | Some record -> lifecycleReusableDecision record

    let private applyReusableWorkUnitDecision
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (agentId: string)
        (reviewerSessionId: SessionId)
        (decision: ReusableWorkUnitDecision)
        : Task<Result<unit, string>> =
        match decision with
        | AlreadyActive -> Task.FromResult(Ok())
        | Abandoned -> Task.FromResult(Error "dedicated reviewer work-unit is abandoned")
        | Relink(targetAgent, byname) ->
            HandleController.linkNamed
                (Some journal)
                managerSessionId
                agentId
                reviewerSessionId
                targetAgent
                byname
                Role.Reviewer
                HandleOwnership.HostOwnedHidden
        | LinkFresh agentName ->
            HandleController.linkNamed
                (Some journal)
                managerSessionId
                agentId
                reviewerSessionId
                agentName
                agentName
                Role.Reviewer
                HandleOwnership.HostOwnedHidden

    /// A dedicated reviewer session spans checkpoints, but its Host-owned work-unit
    /// handle may legitimately finish after each assignment. New assignments reuse
    /// the logical/physical reviewer by durably writing a fresh HandleLinked edge;
    /// already-assigned checkpoints never call this helper.
    let private ensureReusableReviewerWorkUnit
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (agentId: string)
        (reviewerSessionId: SessionId)
        (fallbackAgentName: string)
        (allowRelink: bool)
        : Task<Result<unit, string>> =
        if not allowRelink then
            Task.FromResult(Ok())
        else
            let snapshot = AgentJournal.snapshot journal

            reusableWorkUnitDecision snapshot reviewerSessionId fallbackAgentName
            |> applyReusableWorkUnitDecision journal managerSessionId agentId reviewerSessionId

    let private appendAssigned
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (prepared: TodoWritePrepared)
        (enlisted: DedicatedTodoReviewerEnlisted)
        (reviewWorkStart: XTraceCursor)
        : Task<Result<unit, string>> =
        taskResult {
            let assigned =
                MagicTodoAfter.planEnsureReview HostDigest.sha256Hex prepared enlisted reviewWorkStart

            let! _ =
                AgentJournal.appendMagicTodo
                    (StreamId.Session managerSessionId)
                    None
                    (MagicTodoFact.TodoProcessReviewAssigned assigned)
                    journal
                |> TaskValue.map (Result.mapError JournalAppendFailure.describe)

            return ()
        }

    let private concludeFromOutcome (outcome: TodoProcessReviewProgram.ConcludeOutcome) =
        match outcome with
        | TodoProcessReviewProgram.ConcludeOutcome.Failed reason -> Error reason
        | _ -> Ok()

    let private concludeReview (journal: AgentJournal) (lifeId: ManagerLifeId) (writeId: TodoWriteId) =
        taskResult {
            let! outcome = TodoProcessReviewProgram.tryConclude journal lifeId writeId |> TaskResultCE.ofTask
            return! concludeFromOutcome outcome
        }

    let private openBarrierIfUnassigned
        (gitTree: GitTreePort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        (enlisted: DedicatedTodoReviewerEnlisted)
        (reviewId: TodoReviewId)
        : Task<Result<unit, string>> =
        match checkpoint.Assignment with
        | Some _ -> Task.FromResult(Ok())
        | None ->
            ReviewBarrier.openBarrier
                (Some journal)
                managerSessionId
                enlisted.ReviewerSessionId
                (ReviewBarrierId.create (TodoReviewId.value reviewId))
                (treeHash gitTree reviewId)

    let private appendAssignedIfNeeded
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        (prepared: TodoWritePrepared)
        (enlisted: DedicatedTodoReviewerEnlisted)
        : Task<Result<unit, string>> =
        match checkpoint.Assignment with
        | Some _ -> Task.FromResult(Ok())
        | None ->
            appendAssigned
                journal
                managerSessionId
                prepared
                enlisted
                { Sequence = reviewerHead journal enlisted.ReviewerSessionId }

    let private sendOwnerRootAssignment
        (sessions: ISessionHostPort)
        (journal: AgentJournal)
        (runtime: HostForkRuntime)
        (handleId: string)
        (reviewerSessionId: SessionId)
        (sendAgent: string)
        (assignmentDirectory: string option)
        (assignmentText: string)
        : Task<Result<unit, string>> =
        runtime.DiscardDeferredFirstPrompt handleId

        taskResult {
            let! _ =
                HostForkAgentOwner.sendFirstPrompt
                    sessions
                    (Some journal)
                    reviewerSessionId
                    sendAgent
                    assignmentDirectory
                    assignmentText

            return ()
        }

    let private sendContinuationAssignment
        (sessions: ISessionHostPort)
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        (assignmentDirectory: string option)
        (assignmentText: string)
        : Task<Result<unit, string>> =
        taskResult {
            let! _ =
                HostSessionNudge.sendContinuation
                    sessions
                    reviewerSessionId
                    assignmentText
                    PromptAuthority.ContinuationKind.ReviewerGuard
                    assignmentDirectory
                    (Some journal)

            return ()
        }

    let private sendAssignmentDelivery
        (sessions: ISessionHostPort)
        (journal: AgentJournal)
        (runtime: HostForkRuntime)
        (handleId: string)
        (enlisted: DedicatedTodoReviewerEnlisted)
        (agentName: string)
        (delivery: MagicTodoAfter.AssignmentDelivery)
        (assignmentText: string)
        : Task<Result<unit, string>> =
        let assignmentDirectory = directoryOf enlisted.ReviewerSessionId

        let sendAgent =
            runtime.BoundManagedAgent(handleId, enlisted.ReviewerSessionId)
            |> Option.defaultValue agentName

        match delivery with
        | MagicTodoAfter.AssignmentDelivery.OwnerRoot ->
            sendOwnerRootAssignment
                sessions
                journal
                runtime
                handleId
                enlisted.ReviewerSessionId
                sendAgent
                assignmentDirectory
                assignmentText
        | MagicTodoAfter.AssignmentDelivery.Continuation ->
            sendContinuationAssignment
                sessions
                journal
                enlisted.ReviewerSessionId
                assignmentDirectory
                assignmentText

    type private AssignmentDispatchDecision =
        | AlreadyAcceptedOrPending
        | NeedsDispatch of MagicTodoAfter.AssignmentDelivery

    let private assignmentDispatchDecision
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        (payloadDigest: string)
        : AssignmentDispatchDecision =
        match
            PromptAuthorityLedger.dispatchStatusFor
                reviewerSessionId
                payloadDigest
                (AgentJournal.snapshot journal).AgentProjections
        with
        | PromptAuthorityLedger.DispatchStatus.Accepted _
        | PromptAuthorityLedger.DispatchStatus.Pending -> AlreadyAcceptedOrPending
        | PromptAuthorityLedger.DispatchStatus.Dispatchable ->
            let hasActiveProfile =
                PromptAuthorityLedger.activeProfile
                    reviewerSessionId
                    (AgentJournal.snapshot journal).AgentProjections
                |> Option.isSome

            NeedsDispatch(MagicTodoAfter.assignmentDelivery hasActiveProfile)

    let private deliverOrConclude
        (sessions: ISessionHostPort)
        (journal: AgentJournal)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (enlisted: DedicatedTodoReviewerEnlisted)
        (handleId: string)
        (agentName: string)
        (runtime: HostForkRuntime)
        (assignmentText: string)
        : Task<Result<unit, string>> =
        taskResult {
            let payloadDigest = HostDigest.sha256Hex assignmentText

            match assignmentDispatchDecision journal enlisted.ReviewerSessionId payloadDigest with
            | AlreadyAcceptedOrPending -> return! concludeReview journal lifeId writeId
            | NeedsDispatch delivery ->
                do!
                    sendAssignmentDelivery
                        sessions
                        journal
                        runtime
                        handleId
                        enlisted
                        agentName
                        delivery
                        assignmentText

                return! concludeReview journal lifeId writeId
        }

    /// Assignment + delivery for one unresolved checkpoint (HOST-021).
    ///
    /// Everything happens in one direct CE: the barrier opens once per
    /// assignment; the assignment is appended durable BEFORE the physical send,
    /// freezing the pre-dispatch reviewer frontier; resend admission comes from
    /// PromptAuthority's durable dispatch evidence, never an XTrace head
    /// watermark (REVIEW-018).
    let private ensureAssignedAndDeliver
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        (managerLife: Wanxiangshu.Mission.Manager.Life.LifeProjection)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        (prepared: TodoWritePrepared)
        (enlisted: DedicatedTodoReviewerEnlisted)
        (handleId: string)
        (agentName: string)
        (runtime: HostForkRuntime)
        : Task<Result<unit, string>> =
        taskResult {
            let reviewId = MagicTodo.todoReviewId HostDigest.sha256Hex lifeId writeId

            // The barrier opens exactly once per assignment; reentry with the
            // assignment already durable finds it open (startBarrier no-ops on
            // the same id).
            do! openBarrierIfUnassigned gitTree journal managerSessionId checkpoint enlisted reviewId

            let! oldItems = readObligations journal checkpoint.BaseTodoRef checkpoint.BaseTodoDigest
            let! proposed = readObligations journal checkpoint.ProposedTodoRef checkpoint.ProposedTodoDigest
            do! captureManagerSnapshot snapshot journal managerSessionId
            let! opening = openingRaw journal managerLife |> TaskResultCE.ofTask

            let! checkpointLwr =
                managerCheckpointLwr journal managerSessionId managerLife checkpoint.ReviewFrontier
                |> TaskResultCE.ofTask

            let request: ProcessReviewRequest =
                { TodoReviewId = reviewId
                  TodoWriteId = writeId
                  ManagerLifeId = lifeId
                  OpeningRaw = opening
                  ManagerCheckpointLwr = checkpointLwr
                  EffectivePlanComplete = MagicTodoProjection.isPlanCommitted life
                  OldTodo = oldItems
                  ProposedTodo = proposed }

            let preamble =
                ProviderProse.render
                    (ProviderProse.languageOf managerSessionId)
                    MagicTodoSurface.Path.ProcessReviewerPreamble
                    Map.empty

            let assignmentText =
                MagicTodoProcessReview.renderAssignmentUserMessage preamble request

            // The assignment is durable BEFORE the physical send,
            // freezing the reviewer frontier as it was before this
            // dispatch: the LWR request range then covers everything
            // the reviewer produces for this checkpoint, including
            // the assignment prompt itself (REVIEW-016).
            do! appendAssignedIfNeeded journal managerSessionId checkpoint prepared enlisted

            // Send admission from durable dispatch evidence:
            // Accepted = the payload landed; Pending = outcome
            // undetermined, recovery owns it; Dispatchable = a
            // new claim is allowed.
            return!
                deliverOrConclude
                    sessions
                    journal
                    lifeId
                    writeId
                    enlisted
                    handleId
                    agentName
                    runtime
                    assignmentText
        }

    type private CheckpointAdmission =
        | CheckpointMissing
        | AlreadyConcluded
        | NotAccepted
        | Ready of MagicTodoProjection.CheckpointRecord

    let private admitCheckpoint
        (life: MagicTodoProjection.LifeMagicTodoState)
        (writeId: TodoWriteId)
        : CheckpointAdmission =
        match Map.tryFind (writeKey writeId) life.Checkpoints with
        | None -> CheckpointMissing
        | Some { Concluded = Some _ } -> AlreadyConcluded
        | Some checkpoint when not checkpoint.Accepted -> NotAccepted
        | Some checkpoint -> Ready checkpoint

    let private admitManagerLife
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        : Result<LifeProjection, string> =
        match currentLife journal managerSessionId with
        | None -> Error "open Manager Life is missing"
        | Some managerLife when managerLife.LifeId <> lifeId -> Error "Manager Life does not match TodoWrite"
        | Some managerLife -> Ok managerLife

    let private forkAndEnlistDedicated
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (dedicatedId: DedicatedReviewerId)
        (handleId: string)
        (agentName: string)
        (runtime: HostForkRuntime)
        : Task<Result<DedicatedTodoReviewerEnlisted, string>> =
        taskResult {
            let! _ =
                runtime.Fork(
                    handleId,
                    Role.Reviewer,
                    agentName,
                    ProviderProse.render
                        (ProviderProse.languageOf managerSessionId)
                        MagicTodoSurface.Path.ProcessReviewerPreamble
                        Map.empty,
                    None,
                    firstPrompt = true,
                    ownership = HandleOwnership.HostOwnedHidden,
                    deferSend = true
                )

            let! childId =
                runtime.TryChildSession handleId
                |> Result.requireSome "dedicated process reviewer session was not created"

            let enlisted =
                { ManagerLifeId = lifeId
                  DedicatedReviewerId = dedicatedId
                  ReviewerSessionId = childId }

            let! _ =
                AgentJournal.appendMagicTodo
                    (StreamId.Session managerSessionId)
                    None
                    (MagicTodoFact.DedicatedTodoReviewerEnlisted enlisted)
                    journal
                |> TaskValue.map (Result.mapError JournalAppendFailure.describe)

            return enlisted
        }

    let private enlistDedicatedReviewer
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        (dedicatedId: DedicatedReviewerId)
        (handleId: string)
        (agentName: string)
        (runtime: HostForkRuntime)
        : Task<Result<DedicatedTodoReviewerEnlisted, string>> =
        match life.Dedicated with
        | Some dedicated ->
            runtime.AdoptChild(handleId, dedicated.ReviewerSessionId)

            Task.FromResult(
                Ok
                    { ManagerLifeId = lifeId
                      DedicatedReviewerId = dedicated.DedicatedReviewerId
                      ReviewerSessionId = dedicated.ReviewerSessionId }
            )
        | None ->
            forkAndEnlistDedicated journal managerSessionId lifeId dedicatedId handleId agentName runtime

    let private ensureReviewForCheckpoint
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        (managerLife: LifeProjection)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        : Task<Result<unit, string>> =
        taskResult {
            let dedicatedId = MagicTodo.dedicatedReviewerId HostDigest.sha256Hex lifeId
            let handleId = agentIdOf dedicatedId
            let runtime = forkRuntime sessions snapshot journal managerSessionId
            let agentName = reviewerAgentName journal managerSessionId
            let! enlisted = enlistDedicatedReviewer journal managerSessionId lifeId life dedicatedId handleId agentName runtime

            do!
                ensureReusableReviewerWorkUnit
                    journal
                    managerSessionId
                    handleId
                    enlisted.ReviewerSessionId
                    agentName
                    checkpoint.Assignment.IsNone

            let prepared: TodoWritePrepared =
                { ManagerSessionId = managerSessionId
                  ManagerLifeId = lifeId
                  TodoWriteId = writeId
                  ToolCallId = checkpoint.ToolCallId
                  ToolPartOrdinal = checkpoint.ToolPartOrdinal
                  BaseTodoRef = checkpoint.BaseTodoRef
                  BaseTodoDigest = checkpoint.BaseTodoDigest
                  ProposedTodoRef = checkpoint.ProposedTodoRef
                  ProposedTodoDigest = checkpoint.ProposedTodoDigest
                  PlanCompleteDeclared = checkpoint.PlanCompleteDeclared
                  ProviderInputDigest = checkpoint.ProviderInputDigest
                  ReviewFrontier = checkpoint.ReviewFrontier
                  SemanticVersion = checkpoint.SemanticVersion }

            return!
                ensureAssignedAndDeliver
                    sessions
                    snapshot
                    gitTree
                    journal
                    managerSessionId
                    lifeId
                    writeId
                    life
                    managerLife
                    checkpoint
                    prepared
                    enlisted
                    handleId
                    agentName
                    runtime
        }

    let ensureReview
        (timerPort: ITimerPort)
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        : Task<Result<unit, string>> =
        taskResult {
            let projection = AgentJournal.snapshot journal

            let! life =
                MagicTodoProjection.tryLife lifeId projection.AgentProjections.MagicTodo
                |> Result.requireSome "Magic Todo life is missing"

            match admitCheckpoint life writeId with
            | CheckpointMissing -> return! Error "TodoWrite checkpoint is missing"
            | AlreadyConcluded -> return ()
            | NotAccepted -> return! Error "TodoWrite is not Accepted"
            | Ready checkpoint ->
                let! managerLife = admitManagerLife journal managerSessionId lifeId

                return!
                    ensureReviewForCheckpoint
                        sessions
                        snapshot
                        gitTree
                        journal
                        managerSessionId
                        lifeId
                        writeId
                        life
                        managerLife
                        checkpoint
        }

    type private AwaitConcludeDecision =
        | Done
        | Fail of reason: string
        | AwaitConsumable

    let private awaitConcludeDecision (outcome: TodoProcessReviewProgram.ConcludeOutcome) =
        match outcome with
        | TodoProcessReviewProgram.ConcludeOutcome.Concluded -> Done
        | TodoProcessReviewProgram.ConcludeOutcome.Failed reason -> Fail reason
        | TodoProcessReviewProgram.ConcludeOutcome.Pending _ -> AwaitConsumable

    let awaitConsumableReview
        (timerPort: ITimerPort)
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        : Task<Result<unit, string>> =
        taskResult {
            do! ensureReview timerPort sessions snapshot gitTree journal managerSessionId lifeId writeId
            let! outcome = TodoProcessReviewProgram.tryConclude journal lifeId writeId |> TaskResultCE.ofTask

            match awaitConcludeDecision outcome with
            | Done -> return ()
            | Fail reason -> return! Error reason
            | AwaitConsumable ->
                do! TodoProcessReviewProgram.awaitConsumableReview journal lifeId writeId
                return ()
        }

    let port
        (timerPort: ITimerPort)
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        : ProcessReviewPort =
        { EnsureReview = ensureReview timerPort sessions snapshot gitTree
          AwaitConsumableReview = awaitConsumableReview timerPort sessions snapshot gitTree }
