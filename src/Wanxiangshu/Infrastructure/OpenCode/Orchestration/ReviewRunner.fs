namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Review

/// One review barrier, driven by the Orchestrator (REVIEW-008, REVIEW-009).
///
/// GLORY-042/044: the algorithm lives in Application `ReviewBarrierWorkflow`;
/// this module only adapts its typed outcome to the Orchestrator's
/// `Result<unit, string>` contract (REVISE maps to the existing
/// "Reviewer requested revision" error, keeping ORCH-009 publication semantics).
module OrchestratorHostReview =

    /// ORCH-009: post-rebase review is always deep-reviewer. Explicit tier, never
    /// inferred.
    let DeepReviewerAgent = ManagedAgent.nameOf AgentTier.Deep Role.Reviewer

    [<Literal>]
    let private OpeningPrompt =
        "Review the current worktree for correctness. Submit your verdict with the verdict tool."

    /// Fork a reviewer, open its barrier, and wait for a confirmed dual PERFECT.
    ///
    /// `forkReviewer` returns the reviewer's Host session id; `awaitReviewer` waits for
    /// that run to reach terminal. They are separate because the barrier fact must be
    /// written between them — after the session exists, before any verdict arrives.
    let reverify
        (journal: AgentJournal option)
        (forkReviewer: ManagerJobId -> WorktreePath -> string -> Task<Result<SessionId, string>>)
        (awaitReviewer: ManagerJobId -> Task<Result<unit, string>>)
        (jobId: ManagerJobId)
        (managerSessionId: SessionId)
        (worktree: WorktreePath)
        (barrierId: ReviewBarrierId)
        : Task<Result<unit, string>> =
        task {
            // Read once, use for both the barrier fact and every guard read. ORCH-008
            // fail closed lives in GitTree: an unreadable tree yields a sentinel that
            // `satisfiesGuard` can never match, so it cannot pass as confirmed.
            let tree =
                GitTreeHash.create ((GitTree.create (WorktreePath.value worktree)).GetTreeHash())

            let host: ReviewHostPort =
                { ForkReviewer = fun () -> forkReviewer jobId worktree OpeningPrompt
                  AwaitReviewer = fun () -> awaitReviewer jobId }

            let! outcome = ReviewBarrierWorkflow.reverify journal host managerSessionId barrierId tree

            match outcome with
            | Ok(ReviewBarrierOutcome.Confirmed _) -> return Ok()
            | Ok(ReviewBarrierOutcome.RevisionRequired _) -> return Error "Reviewer requested revision"
            | Error failure ->
                let message =
                    match failure with
                    | ReviewBarrierFailure.CannotCreateReviewer reason -> sprintf "Cannot create reviewer: %s" reason
                    | ReviewBarrierFailure.CannotOpenBarrier reason -> sprintf "Cannot open review barrier: %s" reason
                    | ReviewBarrierFailure.CannotAwaitReviewer reason -> sprintf "Cannot await reviewer: %s" reason
                    | ReviewBarrierFailure.ReviewerProducedNoVerdict -> "Reviewer produced no verdict"
                    | ReviewBarrierFailure.ConfirmationUnproven -> "Reviewer produced no confirmed verdict"

                return Error message
        }
