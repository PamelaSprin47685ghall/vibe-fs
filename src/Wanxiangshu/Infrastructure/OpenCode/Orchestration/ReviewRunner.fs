namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// One review barrier, driven by the Orchestrator (REVIEW-008, REVIEW-009).
///
/// GLORY-042/044: the algorithm now lives in the shared `HostReviewProgram`;
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
        (nudgeReviewer: ManagerJobId -> string -> Task<Result<unit, string>>)
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

            let! outcome =
                HostReviewProgram.reverify
                    journal
                    (fun () -> forkReviewer jobId worktree OpeningPrompt)
                    (fun () -> awaitReviewer jobId)
                    (fun () -> nudgeReviewer jobId ReviewChallenge.Prompt)
                    managerSessionId
                    barrierId
                    tree

            match outcome with
            | Ok(HostReviewProgram.HostReviewOutcome.Confirmed _) -> return Ok()
            | Ok(HostReviewProgram.HostReviewOutcome.RevisionRequired _) -> return Error "Reviewer requested revision"
            | Error failure ->
                let message =
                    match failure with
                    | HostReviewProgram.HostReviewFailure.CannotReadTree reason -> sprintf "Cannot read tree: %s" reason
                    | HostReviewProgram.HostReviewFailure.CannotCreateReviewer reason ->
                        sprintf "Cannot create reviewer: %s" reason
                    | HostReviewProgram.HostReviewFailure.CannotOpenBarrier reason ->
                        sprintf "Cannot open review barrier: %s" reason
                    | HostReviewProgram.HostReviewFailure.CannotSendPrompt reason ->
                        sprintf "Cannot nudge reviewer: %s" reason
                    | HostReviewProgram.HostReviewFailure.CannotAwaitReviewer reason ->
                        sprintf "Cannot await reviewer: %s" reason
                    | HostReviewProgram.HostReviewFailure.ReviewerProducedNoVerdict -> "Reviewer produced no verdict"
                    | HostReviewProgram.HostReviewFailure.ConfirmationUnproven ->
                        "Reviewer produced no confirmed verdict"
                    | HostReviewProgram.HostReviewFailure.WorkRecordUnavailable -> "Reviewer work record is unavailable"
                    | HostReviewProgram.HostReviewFailure.JournalFailure reason -> sprintf "Journal failure: %s" reason

                return Error message
        }
