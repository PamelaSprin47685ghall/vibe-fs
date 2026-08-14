namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Participant.Provider.Attempt.Fallback
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

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let private writeBlob (journal: AgentJournal) (body: string) : Task<Result<BlobWriteReceipt, string>> =
        journal.WriteBlob body

    let private reviewerTrace (snapshot: ProjectionSet) (reviewerSessionId: SessionId) =
        AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.XTrace)
        |> Option.defaultValue XTraceProjection.empty

    let private processVerdict (guard: ReviewGuardProjection) : ProcessReviewVerdict option =
        if List.isEmpty guard.ObservedAttemptKeys then
            None
        elif ReviewWitness.isRevision guard.Witness then
            Some ProcessReviewVerdict.Revise
        else
            Some ProcessReviewVerdict.Perfect

    let private judgeIdentity (trace: XTraceProjectionState) =
        XTraceProjection.parts trace
        |> List.tryFindBack (fun part -> part.Kind = "tool_call" && part.ToolName = Some "judge")
        |> Option.bind (fun part ->
            match part.ProviderRun, part.ToolCallId with
            | Some run, Some call -> Some(run, call)
            | _ -> None)

    /// Append TodoReviewConcluded when VerdictKnown ∧ ProcessReviewLWR record-ready
    /// share this snapshot. Pending is a wait signal, not a provider-visible reject.
    let tryConclude (journal: AgentJournal) (lifeId: ManagerLifeId) (writeId: TodoWriteId) : Task<ConcludeOutcome> =
        task {
            let snapshot = AgentJournal.snapshot journal

            match MagicTodoProjection.tryLife lifeId snapshot.AgentProjections.MagicTodo with
            | None -> return ConcludeOutcome.Pending "life missing"
            | Some life ->
                match Map.tryFind (writeKey writeId) life.Checkpoints with
                | Some { Concluded = Some _ } -> return ConcludeOutcome.Concluded
                | Some cp when cp.Accepted && cp.Assignment.IsSome ->
                    let assignment = Option.get cp.Assignment

                    let guard =
                        AgentProjection.tryFind assignment.ReviewerSessionId snapshot.AgentProjections
                        |> Option.bind (fun session -> session.ReviewGuard)

                    match guard |> Option.bind processVerdict with
                    | None -> return ConcludeOutcome.Pending "verdict unknown"
                    | Some verdict ->
                        let trace = reviewerTrace snapshot assignment.ReviewerSessionId
                        let endExclusive = { Sequence = XTraceProjection.head trace }

                        if endExclusive.Sequence <= assignment.ReviewWorkStartCursor.Sequence then
                            return ConcludeOutcome.Pending "process-review LWR range empty"
                        else
                            let range =
                                { MagicTodoLwr.BoundedRange.StartInclusive = assignment.ReviewWorkStartCursor
                                  MagicTodoLwr.BoundedRange.EndExclusive = endExclusive }

                            let! report =
                                LifecycleWorkRecordProjection.lifecycleWorkRecordBounded
                                    (Some journal)
                                    assignment.ReviewerSessionId
                                    range

                            match report, judgeIdentity trace with
                            | Some report, Some(providerRun, toolCallId) when not (String.IsNullOrWhiteSpace report) ->
                                match! writeBlob journal report with
                                | Error reason -> return ConcludeOutcome.Failed reason
                                | Ok workRecord ->
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
                                          SettledTodoRef = cp.ProposedTodoRef
                                          SettledTodoDigest = cp.ProposedTodoDigest
                                          ReviewerRecordFrontier = endExclusive
                                          ProviderRunId = providerRun
                                          ToolCallId = toolCallId }

                                    match!
                                        AgentJournal.appendMagicTodo
                                            (StreamId.Session assignment.ReviewerSessionId)
                                            (Some providerRun)
                                            (MagicTodoFact.TodoReviewConcluded concluded)
                                            journal
                                    with
                                    | Error failure ->
                                        return ConcludeOutcome.Failed(JournalAppendFailure.describe failure)
                                    | Ok _ -> return ConcludeOutcome.Concluded
                            | _ -> return ConcludeOutcome.Pending "process-review LWR not record-ready"
                | _ -> return ConcludeOutcome.Pending "assignment missing"
        }

    [<RequireQualifiedAccess>]
    type ProducerPresence =
        | Present
        | Absent of reason: string

    /// REVIEW-017/018: Journal wait is legal only while a process-review producer exists.
    let producerPresence (journal: AgentJournal) (lifeId: ManagerLifeId) (writeId: TodoWriteId) : ProducerPresence =
        let snapshot = AgentJournal.snapshot journal

        match MagicTodoProjection.tryLife lifeId snapshot.AgentProjections.MagicTodo with
        | None -> ProducerPresence.Absent "life missing"
        | Some life ->
            match Map.tryFind (writeKey writeId) life.Checkpoints with
            | Some { Concluded = Some _ } -> ProducerPresence.Present
            | Some checkpoint when checkpoint.Assignment.IsSome ->
                let assignment = Option.get checkpoint.Assignment

                match AgentProjection.tryFind assignment.ReviewerSessionId snapshot.AgentProjections with
                | None -> ProducerPresence.Absent "reviewer session missing"
                | Some reviewer ->
                    let verdictKnown =
                        reviewer.ReviewGuard |> Option.bind processVerdict |> Option.isSome

                    match Map.tryFind assignment.ReviewerSessionId snapshot.AgentProjections.HandleByChildSession with
                    | Some record ->
                        match record.Lifecycle with
                        | HandleLifecycle.Active
                        | HandleLifecycle.CompletedAwaitingJoin _ -> ProducerPresence.Present
                        | HandleLifecycle.Retired when verdictKnown ->
                            // The reviewer has durably spoken. The remaining producer
                            // is Journal/XTrace/LWR record-ready convergence, not the
                            // retired Host work-unit.
                            ProducerPresence.Present
                        | HandleLifecycle.Abandoned _
                        | HandleLifecycle.Retired ->
                            ProducerPresence.Absent "reviewer handle ended before durable verdict"
                    | None -> ProducerPresence.Present
            | _ -> ProducerPresence.Absent "assignment missing"

    /// REVIEW-017 / TODO-006: event-driven wait until ConsumableReview is durable.
    /// Wait only while a producer exists; otherwise fail closed (REVIEW-018).
    /// No total-review deadline — a live reviewer may take as long as it writes.
    let rec awaitConsumableReview
        (journal: AgentJournal)
        (lifeId: ManagerLifeId)
        (writeId: TodoWriteId)
        : Task<Result<unit, string>> =
        task {
            match! tryConclude journal lifeId writeId with
            | ConcludeOutcome.Concluded -> return Ok()
            | ConcludeOutcome.Failed reason -> return Error reason
            | ConcludeOutcome.Pending _ ->
                match producerPresence journal lifeId writeId with
                | ProducerPresence.Absent detail -> return Error("process review cannot progress: " + detail)
                | ProducerPresence.Present ->
                    let revision = AgentJournal.revision journal
                    let! _ = AgentJournal.awaitChangeFrom revision journal
                    return! awaitConsumableReview journal lifeId writeId
        }
