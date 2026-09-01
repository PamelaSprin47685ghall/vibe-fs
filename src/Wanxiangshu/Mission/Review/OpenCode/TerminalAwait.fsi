namespace Wanxiangshu.Mission.Review.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Physical Reviewer terminal semantics shared by Finality and Change review.
/// InterruptAttempt retires one Host attempt; it is deliberately not an Agent
/// terminal. The durable exact ReviewAttemptClosed fact authorizes that later
/// Host Abort to close the review occasion cleanly. Every other abort/failure
/// remains an error.
module ReviewerTerminalAwait =

    val tryDurablyClosedJudgementRun:
        journal: AgentJournal option ->
        reviewerSessionId: SessionId ->
        barrierId: ReviewBarrierId ->
            ProviderRunIdentity option

    val hasDurablyClosedJudgement:
        journal: AgentJournal option -> reviewerSessionId: SessionId -> barrierId: ReviewBarrierId -> bool

    val awaitFuture:
        journal: AgentJournal option ->
        sessions: ISessionHostPort ->
        occasion: ReviewerTerminalOccasion ->
        timeoutMs: int ->
            Task<Result<ProviderRunIdentity, string>>
