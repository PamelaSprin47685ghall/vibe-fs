namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal

/// One review barrier, driven by the Orchestrator (REVIEW-008, REVIEW-009).
///
/// GLORY-042/044: the algorithm lives in Application `ReviewBarrierWorkflow`;
/// this module only adapts its typed outcome to the Orchestrator's
/// `Result<unit, string>` contract (REVISE maps to the existing
/// "Reviewer requested revision" error, keeping ORCH-009 publication semantics).
module OrchestratorHostReview =

    /// ORCH-009: post-rebase review is always deep-reviewer. Explicit tier, never
    /// inferred.
    val DeepReviewerAgent: string

    val reverify:
        journal: AgentJournal option ->
        forkReviewer: (ManagerJobId -> WorktreePath -> string -> Task<Result<SessionId, string>>) ->
        startReviewer: (ManagerJobId -> Task<Result<unit, string>>) ->
        awaitReviewer: (ReviewerTerminalOccasion -> Task<Result<ProviderRunIdentity, string>>) ->
        nudgeReviewer: (SessionId -> ProviderRunIdentity -> Task<Result<PhysicalUserMessageId, string>>) ->
        jobId: ManagerJobId ->
        managerSessionId: SessionId ->
        worktree: WorktreePath ->
        barrierId: ReviewBarrierId ->
            Task<Result<unit, string>>
