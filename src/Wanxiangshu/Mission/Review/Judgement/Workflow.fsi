namespace Wanxiangshu.Mission.Review.Judgement

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// The single business owner of a reconciled Reviewer turn's continuation
/// (REVIEW-002/007).
///
/// `observe` is the story: durable `ReviewerEvidence` facts choose the branch;
/// physical delivery is an injected Review port. There is no stored State/Stage counter.
module ReviewerWorkflow =

    /// REVIEW-013 / FINALITY-011 race closure: a terminal judge may only abort
    /// the managed Reviewer after the Blogger producer that was already
    /// durable-open for that Reviewer has settled. Otherwise AbortSession can
    /// win the physical race against the Blogger's next transform and starve the
    /// record-ready Chronicle forever. The verdict remains durable immediately;
    /// this barrier owns only physical interrupt ordering.
    val awaitSubmittedRecordCapture:
        cancellation: CancellationToken ->
        journal: AgentJournal ->
        reviewerSessionId: SessionId ->
            Task<Result<unit, string>>

    /// Freeze the closure at its exclusive frontier before interrupting,
    /// or recover the same fact after a crash.
    ///
    /// Ok true  = closure already existed or was durably appended.
    /// Ok false = the matching durable tool_result is not present yet.
    val ensureSubmittedAttemptClosed:
        journal: AgentJournal -> reviewerSessionId: SessionId -> Task<Result<bool, string>>

    /// Physical terminal observer. Active Finality reviewers are owned by the
    /// direct ReviewBarrierWorkflow CE, so this function reports their turn.
    val observe:
        _continuationPort: ReviewerContinuationPort ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        turn: ReconciledTurn ->
        _reviewerKey: string ->
            Task
