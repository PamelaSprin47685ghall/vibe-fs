namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
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
        task {
            match! journal.Writer.BlobWriter.Read blobRef with
            | Error reason -> return Error reason
            | Ok body when HostDigest.sha256Hex body <> BlobDigest.value expected ->
                return Error "obligation blob digest mismatch"
            | Ok body -> return MagicTodoObligationCodec.tryDecode body
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

    let private captureManagerSnapshot
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        : Task<Result<unit, string>> =
        match snapshot with
        | None -> Task.FromResult(Ok())
        | Some port ->
            task {
                match! port.GetMessages managerSessionId with
                | Error reason -> return Error("snapshot unavailable: " + reason)
                | Ok messages -> return! XTraceCapture.captureSessionMessages (Some journal) managerSessionId messages
            }

    let private treeHash (gitTree: GitTreePort option) (reviewId: TodoReviewId) =
        let fallback =
            GitTreeHash.create (HostDigest.sha256Hex (TodoReviewId.value reviewId))

        match gitTree with
        | None -> fallback
        | Some port ->
            try
                let value = port.GetTreeHash().Trim()

                if String.IsNullOrWhiteSpace value then
                    fallback
                else
                    GitTreeHash.create value
            with _ex ->
                fallback

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
        let snapshot = AgentJournal.snapshot journal

        if not allowRelink then
            Task.FromResult(Ok())
        else
            match Map.tryFind reviewerSessionId snapshot.AgentProjections.HandleByChildSession with
            | Some record ->
                match record.Lifecycle with
                | HandleLifecycle.Active -> Task.FromResult(Ok())
                | HandleLifecycle.Abandoned _ -> Task.FromResult(Error "dedicated reviewer work-unit is abandoned")
                | HandleLifecycle.CompletedAwaitingJoin _
                | HandleLifecycle.Retired ->
                    // HandleController.linkNamed is the sole HandleLinked writer.
                    HandleController.linkNamed
                        (Some journal)
                        managerSessionId
                        agentId
                        reviewerSessionId
                        record.TargetAgent
                        record.Byname
                        Role.Reviewer
                        HandleOwnership.HostOwnedHidden
            | None ->
                HandleController.linkNamed
                    (Some journal)
                    managerSessionId
                    agentId
                    reviewerSessionId
                    fallbackAgentName
                    fallbackAgentName
                    Role.Reviewer
                    HandleOwnership.HostOwnedHidden

    let private appendAssigned
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (prepared: TodoWritePrepared)
        (enlisted: DedicatedTodoReviewerEnlisted)
        (reviewWorkStart: XTraceCursor)
        : Task<Result<unit, string>> =
        task {
            let assigned =
                MagicTodoAfter.planEnsureReview HostDigest.sha256Hex prepared enlisted reviewWorkStart

            match!
                AgentJournal.appendMagicTodo
                    (StreamId.Session managerSessionId)
                    None
                    (MagicTodoFact.TodoProcessReviewAssigned assigned)
                    journal
            with
            | Error failure -> return Error(JournalAppendFailure.describe failure)
            | Ok _ -> return Ok()
        }

    let private concludeReview (journal: AgentJournal) (lifeId: ManagerLifeId) (writeId: TodoWriteId) =
        task {
            match! TodoProcessReviewProgram.tryConclude journal lifeId writeId with
            | TodoProcessReviewProgram.ConcludeOutcome.Failed reason -> return Error reason
            | _ -> return Ok()
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
        task {
            let reviewId = MagicTodo.todoReviewId HostDigest.sha256Hex lifeId writeId

            // The barrier opens exactly once per assignment; reentry with the
            // assignment already durable finds it open (startBarrier no-ops on
            // the same id).
            let! barrierResult =
                match checkpoint.Assignment with
                | Some _ -> Task.FromResult(Ok())
                | None ->
                    ReviewBarrier.openBarrier
                        (Some journal)
                        managerSessionId
                        enlisted.ReviewerSessionId
                        (ReviewBarrierId.create (TodoReviewId.value reviewId))
                        (treeHash gitTree reviewId)

            match barrierResult with
            | Error reason -> return Error reason
            | Ok() ->
                let! oldItemsResult = readObligations journal checkpoint.BaseTodoRef checkpoint.BaseTodoDigest

                let! proposedResult = readObligations journal checkpoint.ProposedTodoRef checkpoint.ProposedTodoDigest

                match oldItemsResult, proposedResult with
                | Error reason, _
                | _, Error reason -> return Error reason
                | Ok oldItems, Ok proposed ->
                    match! captureManagerSnapshot snapshot journal managerSessionId with
                    | Error reason -> return Error reason
                    | Ok() ->
                        let! opening = openingRaw journal managerLife

                        let! checkpointLwr =
                            managerCheckpointLwr journal managerSessionId managerLife checkpoint.ReviewFrontier

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
                        let! assignmentDurable =
                            match checkpoint.Assignment with
                            | Some _ -> Task.FromResult(Ok())
                            | None ->
                                appendAssigned
                                    journal
                                    managerSessionId
                                    prepared
                                    enlisted
                                    { Sequence = reviewerHead journal enlisted.ReviewerSessionId }

                        match assignmentDurable with
                        | Error reason -> return Error reason
                        | Ok() ->
                            // Send admission from durable dispatch evidence:
                            // Accepted = the payload landed; Pending = outcome
                            // undetermined, recovery owns it; Dispatchable = a
                            // new claim is allowed.
                            let payloadDigest = HostDigest.sha256Hex assignmentText

                            match
                                PromptAuthorityLedger.dispatchStatusFor
                                    enlisted.ReviewerSessionId
                                    payloadDigest
                                    (AgentJournal.snapshot journal).AgentProjections
                            with
                            | PromptAuthorityLedger.DispatchStatus.Accepted _
                            | PromptAuthorityLedger.DispatchStatus.Pending ->
                                return! concludeReview journal lifeId writeId
                            | PromptAuthorityLedger.DispatchStatus.Dispatchable ->
                                let hasActiveProfile =
                                    PromptAuthorityLedger.activeProfile
                                        enlisted.ReviewerSessionId
                                        (AgentJournal.snapshot journal).AgentProjections
                                    |> Option.isSome

                                let delivery = MagicTodoAfter.assignmentDelivery hasActiveProfile

                                let assignmentDirectory = directoryOf enlisted.ReviewerSessionId

                                let sendAgent =
                                    runtime.BoundManagedAgent(handleId, enlisted.ReviewerSessionId)
                                    |> Option.defaultValue agentName

                                let! sent =
                                    match delivery with
                                    | MagicTodoAfter.AssignmentDelivery.OwnerRoot ->
                                        runtime.DiscardDeferredFirstPrompt handleId

                                        task {
                                            match!
                                                HostForkAgentOwner.sendFirstPrompt
                                                    sessions
                                                    (Some journal)
                                                    enlisted.ReviewerSessionId
                                                    sendAgent
                                                    assignmentDirectory
                                                    assignmentText
                                            with
                                            | Ok _ -> return Ok()
                                            | Error reason -> return Error reason
                                        }
                                    | MagicTodoAfter.AssignmentDelivery.Continuation ->
                                        task {
                                            match!
                                                HostSessionNudge.sendContinuation
                                                    sessions
                                                    enlisted.ReviewerSessionId
                                                    assignmentText
                                                    PromptAuthority.ContinuationKind.ReviewerGuard
                                                    assignmentDirectory
                                                    (Some journal)
                                            with
                                            | Ok _ -> return Ok()
                                            | Error reason -> return Error reason
                                        }

                                match sent with
                                | Error reason -> return Error reason
                                | Ok() -> return! concludeReview journal lifeId writeId
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
        task {
            let projection = AgentJournal.snapshot journal

            match MagicTodoProjection.tryLife lifeId projection.AgentProjections.MagicTodo with
            | None -> return Error "Magic Todo life is missing"
            | Some life ->
                match Map.tryFind (writeKey writeId) life.Checkpoints with
                | None -> return Error "TodoWrite checkpoint is missing"
                | Some { Concluded = Some _ } -> return Ok()
                | Some checkpoint when not checkpoint.Accepted -> return Error "TodoWrite is not Accepted"
                | Some checkpoint ->
                    match currentLife journal managerSessionId with
                    | None -> return Error "open Manager Life is missing"
                    | Some managerLife when managerLife.LifeId <> lifeId ->
                        return Error "Manager Life does not match TodoWrite"
                    | Some managerLife ->
                        let dedicatedId = MagicTodo.dedicatedReviewerId HostDigest.sha256Hex lifeId
                        let handleId = agentIdOf dedicatedId
                        let runtime = forkRuntime sessions snapshot journal managerSessionId
                        let agentName = reviewerAgentName journal managerSessionId

                        let! enlistedResult =
                            task {
                                match life.Dedicated with
                                | Some dedicated ->
                                    runtime.AdoptChild(handleId, dedicated.ReviewerSessionId)

                                    return
                                        Ok
                                            { ManagerLifeId = lifeId
                                              DedicatedReviewerId = dedicated.DedicatedReviewerId
                                              ReviewerSessionId = dedicated.ReviewerSessionId }
                                | None ->
                                    match!
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
                                    with
                                    | Error reason -> return Error reason
                                    | Ok _ ->
                                        match runtime.TryChildSession handleId with
                                        | None -> return Error "dedicated process reviewer session was not created"
                                        | Some childId ->
                                            let enlisted =
                                                { ManagerLifeId = lifeId
                                                  DedicatedReviewerId = dedicatedId
                                                  ReviewerSessionId = childId }

                                            match!
                                                AgentJournal.appendMagicTodo
                                                    (StreamId.Session managerSessionId)
                                                    None
                                                    (MagicTodoFact.DedicatedTodoReviewerEnlisted enlisted)
                                                    journal
                                            with
                                            | Error failure -> return Error(JournalAppendFailure.describe failure)
                                            | Ok _ -> return Ok enlisted
                            }

                        match enlistedResult with
                        | Error reason -> return Error reason
                        | Ok enlisted ->
                            let! reusableResult =
                                ensureReusableReviewerWorkUnit
                                    journal
                                    managerSessionId
                                    handleId
                                    enlisted.ReviewerSessionId
                                    agentName
                                    checkpoint.Assignment.IsNone

                            match reusableResult with
                            | Error reason -> return Error reason
                            | Ok() ->
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
        task {
            match! ensureReview timerPort sessions snapshot gitTree journal managerSessionId lifeId writeId with
            | Error reason -> return Error reason
            | Ok() ->
                match! TodoProcessReviewProgram.tryConclude journal lifeId writeId with
                | TodoProcessReviewProgram.ConcludeOutcome.Concluded -> return Ok()
                | TodoProcessReviewProgram.ConcludeOutcome.Failed reason -> return Error reason
                | TodoProcessReviewProgram.ConcludeOutcome.Pending _ ->
                    match! TodoProcessReviewProgram.awaitConsumableReview journal lifeId writeId with
                    | Ok() -> return Ok()
                    | Error reason -> return Error reason
        }

    let port
        (timerPort: ITimerPort)
        (sessions: ISessionHostPort)
        (snapshot: ISessionSnapshotPort option)
        (gitTree: GitTreePort option)
        : ProcessReviewPort =
        { EnsureReview = ensureReview timerPort sessions snapshot gitTree
          AwaitConsumableReview = awaitConsumableReview timerPort sessions snapshot gitTree }
