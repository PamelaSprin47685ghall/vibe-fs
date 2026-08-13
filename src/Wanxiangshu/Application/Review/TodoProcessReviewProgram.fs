namespace Wanxiangshu.Review

open System
open Wanxiangshu.Domain
open Wanxiangshu.Domain.MagicTodo
open Wanxiangshu.Domain.MagicTodoFacts
open Wanxiangshu.Finality
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Same-snapshot ConsumableReview materialization (REVIEW-014/017, TODO-006/008).
/// Host ensureReview reenters this after VerdictKnown; it does not fork sessions.
module TodoProcessReviewProgram =

    [<RequireQualifiedAccess>]
    type ConcludeOutcome =
        | Concluded
        | Pending of reason: string
        | Failed of reason: string

    let private writeKey (writeId: TodoWriteId) = TodoWriteId.value writeId

    let private readObligations
        (journal: AgentJournal)
        (blobRef: BlobRef)
        (expected: BlobDigest)
        : Result<ObligationList, string> =
        match journal.Writer.BlobWriter.Read blobRef with
        | Error reason -> Error reason
        | Ok body when HostDigest.sha256Hex body <> BlobDigest.value expected -> Error "obligation blob digest mismatch"
        | Ok body -> MagicTodoObligationCodec.tryDecode body

    let private writeBlob (journal: AgentJournal) (body: string) : Result<BlobWriteReceipt, string> =
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
        trace.Parts
        |> List.tryFindBack (fun part -> part.Kind = "tool_call" && part.ToolName = Some "judge")
        |> Option.bind (fun part ->
            match part.ProviderRun, part.ToolCallId with
            | Some run, Some call -> Some(run, call)
            | _ -> None)

    /// Append TodoReviewConcluded when VerdictKnown ∧ ProcessReviewLWR record-ready
    /// share this snapshot. Pending is a wait signal, not a provider-visible reject.
    let tryConclude (journal: AgentJournal) (lifeId: ManagerLifeId) (writeId: TodoWriteId) : ConcludeOutcome =
        let snapshot = AgentJournal.snapshot journal

        match MagicTodoProjection.tryLife lifeId snapshot.AgentProjections.MagicTodo with
        | None -> ConcludeOutcome.Pending "life missing"
        | Some life ->
            match Map.tryFind (writeKey writeId) life.Checkpoints with
            | Some { Concluded = Some _ } -> ConcludeOutcome.Concluded
            | Some cp when cp.Accepted && cp.Assignment.IsSome ->
                let assignment = Option.get cp.Assignment

                let guard =
                    AgentProjection.tryFind assignment.ReviewerSessionId snapshot.AgentProjections
                    |> Option.bind (fun session -> session.ReviewGuard)

                match guard |> Option.bind processVerdict with
                | None -> ConcludeOutcome.Pending "verdict unknown"
                | Some verdict ->
                    let trace = reviewerTrace snapshot assignment.ReviewerSessionId
                    let endExclusive = { Sequence = XTraceProjection.head trace }

                    if endExclusive.Sequence <= assignment.ReviewWorkStartCursor.Sequence then
                        ConcludeOutcome.Pending "process-review LWR range empty"
                    else
                        let range =
                            { MagicTodoLwr.BoundedRange.StartInclusive = assignment.ReviewWorkStartCursor
                              MagicTodoLwr.BoundedRange.EndExclusive = endExclusive }

                        match
                            LifecycleWorkRecordProjection.lifecycleWorkRecordBounded
                                (Some journal)
                                assignment.ReviewerSessionId
                                range,
                            judgeIdentity trace
                        with
                        | Some report, Some(providerRun, toolCallId) when not (String.IsNullOrWhiteSpace report) ->
                            match
                                readObligations journal cp.BaseTodoRef cp.BaseTodoDigest,
                                readObligations journal cp.ProposedTodoRef cp.ProposedTodoDigest
                            with
                            | Error reason, _
                            | _, Error reason -> ConcludeOutcome.Failed reason
                            | Ok oldItems, Ok proposed ->
                                let settled = MagicTodo.settleObligations oldItems proposed verdict

                                match writeBlob journal report, writeBlob journal (MagicTodoObligationCodec.encode settled) with
                                | Error reason, _
                                | _, Error reason -> ConcludeOutcome.Failed reason
                                | Ok workRecord, Ok settledBlob ->
                                    let concluded =
                                        { ManagerLifeId = lifeId
                                          TodoWriteId = writeId
                                          TodoReviewId = assignment.TodoReviewId
                                          DedicatedReviewerId = assignment.DedicatedReviewerId
                                          ReviewerSessionId = assignment.ReviewerSessionId
                                          Verdict = verdict
                                          WorkRecordRef = workRecord.BlobRef
                                          WorkRecordDigest = workRecord.BlobDigest
                                          SettledTodoRef = settledBlob.BlobRef
                                          SettledTodoDigest = settledBlob.BlobDigest
                                          ReviewerRecordFrontier = endExclusive
                                          ProviderRunId = providerRun
                                          ToolCallId = toolCallId }

                                    match
                                        AgentJournal.appendMagicTodo
                                            (StreamId.Session assignment.ReviewerSessionId)
                                            (Some providerRun)
                                            (MagicTodoFact.TodoReviewConcluded concluded)
                                            journal
                                    with
                                    | Error failure -> ConcludeOutcome.Failed(JournalAppendFailure.describe failure)
                                    | Ok _ -> ConcludeOutcome.Concluded
                        | _ -> ConcludeOutcome.Pending "process-review LWR not record-ready"
            | _ -> ConcludeOutcome.Pending "assignment missing"
