namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Participant.Provider.Attempt.Fallback
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
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Mission.Finality
open Wanxiangshu.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Same-snapshot ConsumableReview materialization (REVIEW-014/017, TODO-006/008).
/// Host ensureReview reenters this after VerdictKnown; it does not fork sessions.
module TodoProcessReviewProgram =

    [<RequireQualifiedAccess>]
    type ConcludeOutcome =
        | Concluded
        | Pending of reason: string
        | Failed of reason: string

    [<RequireQualifiedAccess>]
    type private ConcludePrerequisite =
        | AlreadyConcluded
        | Missing of reason: string
        | Assigned of checkpoint: MagicTodoProjection.CheckpointRecord * assignment: TodoProcessReviewAssigned

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let private writeBlob (journal: AgentJournal) (body: string) : Task<Result<BlobWriteReceipt, string>> =
        journal.WriteBlob body

    /// REVIEW-013/017: the verdict AND its matching closure, bound together so
    /// tryConclude stays a single flat decision. An attempt whose reconciled
    /// turn has not closed has no consumable frontier — consuming the session
    /// head instead would let a finished attempt's late-landing tail widen the
    /// record.
    let private verdictClosure
        (guard: ReviewGuardProjection)
        : (ProcessReviewVerdict * ReviewAttemptIdentity * ClosedAttempt) option =
        let verdict =
            if ReviewWitness.isRevision guard.Witness then
                ProcessReviewVerdict.Revise
            else
                ProcessReviewVerdict.Perfect

        ReviewProjection.latestObservedAttempt guard
        |> Option.bind (fun attempt ->
            ReviewProjection.closedAttemptOf attempt guard
            |> Option.map (fun closure -> verdict, attempt, closure))

    let private concludePrerequisiteOfCheckpoint (checkpoint: MagicTodoProjection.CheckpointRecord) =
        match
            MagicTodoProjection.conclusion checkpoint,
            MagicTodoProjection.assignment checkpoint,
            MagicTodoProjection.isAccepted checkpoint
        with
        | Some _, _, _ -> ConcludePrerequisite.AlreadyConcluded
        | None, Some assignment, true -> ConcludePrerequisite.Assigned(checkpoint, assignment)
        | _ -> ConcludePrerequisite.Missing "assignment missing"

    let private concludePrerequisite
        (life: MagicTodoProjection.LifeMagicTodoState)
        (writeId: TodoWriteId)
        : ConcludePrerequisite =
        match Map.tryFind (writeKey writeId) life.Checkpoints with
        | Some checkpoint -> concludePrerequisiteOfCheckpoint checkpoint
        | None -> ConcludePrerequisite.Missing "assignment missing"

    let private closedVerdict
        (snapshot: ProjectionSet)
        (assignment: TodoProcessReviewAssigned)
        : (ProcessReviewVerdict * ReviewAttemptIdentity * ClosedAttempt) option =
        AgentProjection.tryFind assignment.ReviewerSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.ReviewGuard)
        |> Option.bind verdictClosure

    let private readyReport (report: string option) =
        report |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))

    let private appendMagicTodoFact
        (journal: AgentJournal)
        (assignment: TodoProcessReviewAssigned)
        (attempt: ReviewAttemptIdentity)
        (concluded: TodoReviewConcluded)
        : Task<Result<unit, string>> =
        task {
            match!
                AgentJournal.appendMagicTodo
                    (StreamId.Session assignment.ReviewerSessionId)
                    (Some attempt.ProviderRun)
                    (MagicTodoFact.TodoReviewConcluded concluded)
                    journal
            with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    let private concludeFromPersist (persist: Task<Result<unit, string>>) : Task<ConcludeOutcome> =
        task {
            match! persist with
            | Ok() -> return ConcludeOutcome.Concluded
            | Error reason -> return ConcludeOutcome.Failed reason
        }

    let private appendConcluded
        (journal: AgentJournal)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        (assignment: TodoProcessReviewAssigned)
        (verdict: ProcessReviewVerdict)
        (attempt: ReviewAttemptIdentity)
        (endExclusive: XTraceCursor)
        (report: string)
        : Task<ConcludeOutcome> =
        taskResult {
            let! workRecord = writeBlob journal report

            let concluded =
                { ManagerLifeId = lifeId
                  TodoWriteId = writeId
                  TodoReviewId = assignment.TodoReviewId
                  DedicatedReviewerId = assignment.DedicatedReviewerId
                  ReviewerSessionId = assignment.ReviewerSessionId
                  Verdict = verdict
                  WorkRecordRef = workRecord.BlobRef
                  WorkRecordDigest = workRecord.BlobDigest
                  // Persisted wire compatibility only. CurrentObligations
                  // moved at TodoWriteAccepted and never rolls back on verdict.
                  SettledTodoRef = checkpoint.ProposedTodoRef
                  SettledTodoDigest = checkpoint.ProposedTodoDigest
                  ReviewerRecordFrontier = endExclusive
                  ProviderRunId = attempt.ProviderRun
                  ToolCallId = attempt.ToolCallId }

            do! appendMagicTodoFact journal assignment attempt concluded
            return ()
        }
        |> concludeFromPersist

    let private persistConclude
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        (assignment: TodoProcessReviewAssigned)
        (verdict: ProcessReviewVerdict)
        (attempt: ReviewAttemptIdentity)
        (closure: ClosedAttempt)
        : Task<ConcludeOutcome> =
        task {
            let endExclusive = closure.FrozenFrontier

            let range =
                { MagicTodoLwr.BoundedRange.StartInclusive = assignment.ReviewWorkStartCursor
                  MagicTodoLwr.BoundedRange.EndExclusive = endExclusive }

            let! report =
                LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedFromSnapshotForRun
                    journal
                    snapshot
                    assignment.ReviewerSessionId
                    range
                    attempt.ProviderRun

            match readyReport report with
            | None -> return ConcludeOutcome.Pending "process-review LWR not record-ready"
            | Some body ->
                return! appendConcluded journal lifeId writeId checkpoint assignment verdict attempt endExclusive body
        }

    let private concludeWithClosure
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        (assignment: TodoProcessReviewAssigned)
        (verdict: ProcessReviewVerdict)
        (attempt: ReviewAttemptIdentity)
        (closure: ClosedAttempt)
        : Task<ConcludeOutcome> =
        if closure.FrozenFrontier.Sequence <= assignment.ReviewWorkStartCursor.Sequence then
            Task.FromResult(ConcludeOutcome.Pending "process-review LWR range empty")
        else
            persistConclude journal snapshot lifeId writeId checkpoint assignment verdict attempt closure

    let private concludeAssigned
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        (assignment: TodoProcessReviewAssigned)
        : Task<ConcludeOutcome> =
        match closedVerdict snapshot assignment with
        | None -> Task.FromResult(ConcludeOutcome.Pending "process verdict not closed")
        | Some(verdict, attempt, closure) ->
            concludeWithClosure journal snapshot lifeId writeId checkpoint assignment verdict attempt closure

    let private concludeForLife
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (life: MagicTodoProjection.LifeMagicTodoState)
        : Task<ConcludeOutcome> =
        match concludePrerequisite life writeId with
        | ConcludePrerequisite.AlreadyConcluded -> Task.FromResult ConcludeOutcome.Concluded
        | ConcludePrerequisite.Missing reason -> Task.FromResult(ConcludeOutcome.Pending reason)
        | ConcludePrerequisite.Assigned(checkpoint, assignment) ->
            concludeAssigned journal snapshot lifeId writeId checkpoint assignment

    let private reviewerForRecovery
        (snapshot: ProjectionSet)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        : SessionId option =
        MagicTodoProjection.tryLife lifeId snapshot.AgentProjections.MagicTodo
        |> Option.bind (fun life ->
            match concludePrerequisite life writeId with
            | ConcludePrerequisite.Assigned(_, assignment) -> Some assignment.ReviewerSessionId
            | _ -> None)

    let private recoverSubmittedClosure
        (journal: AgentJournal)
        (reviewerSessionId: SessionId option)
        : Task<Result<unit, string>> =
        match reviewerSessionId with
        | None -> Task.FromResult(Ok())
        | Some reviewer ->
            taskResult {
                let! _ = ReviewerWorkflow.ensureSubmittedAttemptClosed journal reviewer
                return ()
            }

    let private concludeFreshSnapshot
        (journal: AgentJournal)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        : Task<ConcludeOutcome> =
        let snapshot = AgentJournal.snapshot journal

        match MagicTodoProjection.tryLife lifeId snapshot.AgentProjections.MagicTodo with
        | None -> Task.FromResult(ConcludeOutcome.Pending "life missing")
        | Some life -> concludeForLife journal snapshot lifeId writeId life

    let private concludeCurrentSnapshot
        (journal: AgentJournal)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (recovery: Result<unit, string>)
        : Task<ConcludeOutcome> =
        match recovery with
        | Error reason -> Task.FromResult(ConcludeOutcome.Failed reason)
        | Ok() -> concludeFreshSnapshot journal lifeId writeId

    /// Append TodoReviewConcluded when VerdictKnown ∧ matching ReviewAttemptClosed
    /// ∧ ProcessReviewLWR record-ready share this snapshot. Pending is a wait
    /// signal, not a provider-visible reject.
    let tryConclude (journal: AgentJournal) (lifeId: ManagerLifeId) (writeId: TodoWriteId) : Task<ConcludeOutcome> =
        task {
            let snapshot = AgentJournal.snapshot journal
            let reviewer = reviewerForRecovery snapshot lifeId writeId
            let! recovered = recoverSubmittedClosure journal reviewer
            return! concludeCurrentSnapshot journal lifeId writeId recovered
        }

    [<RequireQualifiedAccess>]
    type ProducerPresence =
        | Present
        | Absent of reason: string

    let private presenceForHandle (lifecycle: HandleLifecycle) (verdictKnown: bool) : ProducerPresence =
        match lifecycle, verdictKnown with
        | HandleLifecycle.Active, _
        | HandleLifecycle.CompletedAwaitingJoin _, _ -> ProducerPresence.Present
        | HandleLifecycle.Retired, true
        | HandleLifecycle.Abandoned _, true ->
            // The reviewer has durably spoken. The remaining producer
            // is Journal/XTrace/LWR record-ready convergence, not the Host
            // work-unit. Parent cancellation/process teardown must not erase
            // that durable producer or turn a missing closure into a fake
            // "before durable verdict" infrastructure failure.
            ProducerPresence.Present
        | HandleLifecycle.Abandoned _, false
        | HandleLifecycle.Retired, false -> ProducerPresence.Absent "reviewer handle ended before durable verdict"

    let private presenceForReviewer
        (snapshot: ProjectionSet)
        (assignment: TodoProcessReviewAssigned)
        (reviewer: SessionAgentProjection)
        : ProducerPresence =
        let verdictKnown =
            reviewer.ReviewGuard
            |> Option.bind ReviewProjection.latestObservedAttempt
            |> Option.isSome

        match Map.tryFind assignment.ReviewerSessionId snapshot.AgentProjections.HandleByChildSession with
        | Some record -> presenceForHandle record.Lifecycle verdictKnown
        | None -> ProducerPresence.Present

    let private presenceForAssignment
        (snapshot: ProjectionSet)
        (assignment: TodoProcessReviewAssigned)
        : ProducerPresence =
        match AgentProjection.tryFind assignment.ReviewerSessionId snapshot.AgentProjections with
        | None -> ProducerPresence.Absent "reviewer session missing"
        | Some reviewer -> presenceForReviewer snapshot assignment reviewer

    let private presenceForCheckpoint
        (snapshot: ProjectionSet)
        (checkpoint: MagicTodoProjection.CheckpointRecord)
        : ProducerPresence =
        match MagicTodoProjection.conclusion checkpoint, MagicTodoProjection.assignment checkpoint with
        | Some _, _ -> ProducerPresence.Present
        | None, Some assignment -> presenceForAssignment snapshot assignment
        | None, None -> ProducerPresence.Absent "assignment missing"

    let private presenceForLife
        (snapshot: ProjectionSet)
        (life: MagicTodoProjection.LifeMagicTodoState)
        (writeId: TodoWriteId)
        : ProducerPresence =
        match Map.tryFind (writeKey writeId) life.Checkpoints with
        | Some checkpoint -> presenceForCheckpoint snapshot checkpoint
        | _ -> ProducerPresence.Absent "assignment missing"

    /// REVIEW-017/018: Journal wait is legal only while a process-review producer exists.
    let producerPresence (journal: AgentJournal) (lifeId: ManagerLifeId) (writeId: TodoWriteId) : ProducerPresence =
        let snapshot = AgentJournal.snapshot journal

        match MagicTodoProjection.tryLife lifeId snapshot.AgentProjections.MagicTodo with
        | None -> ProducerPresence.Absent "life missing"
        | Some life -> presenceForLife snapshot life writeId

    /// REVIEW-017 / TODO-006: event-driven wait until ConsumableReview is durable.
    /// Wait only while a producer exists; otherwise fail closed (REVIEW-018).
    /// No total-review deadline — a live reviewer may take as long as it writes.
    let rec awaitConsumableReview
        (journal: AgentJournal)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        : Task<Result<unit, string>> =
        task {
            let revision = AgentJournal.revision journal

            match! tryConclude journal lifeId writeId with
            | ConcludeOutcome.Concluded -> return Ok()
            | ConcludeOutcome.Failed reason -> return Error reason
            | ConcludeOutcome.Pending _ -> return! awaitWhileProducerPresent journal lifeId writeId revision
        }

    and private awaitWhileProducerPresent
        (journal: AgentJournal)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        (revision: JournalRevision)
        : Task<Result<unit, string>> =
        match producerPresence journal lifeId writeId with
        | ProducerPresence.Absent detail -> Task.FromResult(Error("process review cannot progress: " + detail))
        | ProducerPresence.Present ->
            task {
                let! _ = AgentJournal.awaitChangeFrom revision journal
                return! awaitConsumableReview journal lifeId writeId
            }
