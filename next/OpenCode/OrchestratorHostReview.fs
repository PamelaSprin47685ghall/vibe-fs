namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// One review barrier, driven by the Orchestrator (REVIEW-008, REVIEW-009).
module OrchestratorHostReview =

    /// ORCH-009: post-rebase review is always deep-reviewer. Explicit tier, never
    /// inferred.
    let DeepReviewerAgent = ManagedAgent.nameOf AgentTier.Deep Role.Reviewer

    [<Literal>]
    let private OpeningPrompt =
        "Review the current worktree for correctness. Submit your verdict with the verdict tool."

    /// Emit `ReviewBarrierStarted` for a freshly forked reviewer.
    ///
    /// REVIEW-008 decision (package G): the barrier is emitted from the reviewer fork
    /// path, once the child session exists. It cannot be emitted earlier — the fact
    /// carries `ReviewerSessionId` and the fold keys `ReviewGuardProjection` by it, so
    /// a barrier opened before the fork has nothing to key. One reviewer session per
    /// barrier also makes REVIEW-008's "a fresh dual PERFECT" automatic: that session's
    /// guard starts empty, so no earlier witness can satisfy it.
    let private startBarrier
        (journal: AgentJournal option)
        (managerSessionId: SessionId)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (tree: GitTreeHash)
        : Result<unit, string> =
        match journal with
        | None -> Error "Review barrier requires an AgentJournal"
        | Some durable ->
            let fact =
                AgentFact.ReviewBarrierStarted
                    {| ReviewerSessionId = reviewerSessionId
                       ManagerSessionId = managerSessionId
                       BarrierId = barrierId
                       GitTreeHash = tree |}

            match AgentJournal.appendAgent (StreamId.Session reviewerSessionId) None fact durable with
            | Ok _ -> Ok()
            | Error failure -> Error(sprintf "%A" failure.Failure)

    /// Fork a reviewer, open its barrier, and wait for a confirmed dual PERFECT.
    ///
    /// `forkReviewer` returns the reviewer's Host session id; `awaitReviewer` waits for
    /// that run to reach terminal. They are separate because the barrier fact must be
    /// written between them — after the session exists, before any verdict arrives.
    ///
    /// A first PERFECT is answered by nudging the SAME reviewer session with
    /// `ReviewChallenge.Text`, whose digest REVIEW-003's seal is searched for. Re-forking
    /// would open a new barrier and throw the first PERFECT away.
    ///
    /// Exactly one confirmation round. The previous version had two nearly identical
    /// nudge-then-reread blocks — one for `PendingConfirmation`, one for `NeedsReview`
    /// with a different prompt — so a reviewer that produced no verdict at all got a
    /// second chance that REVIEW-003 does not grant.
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

            match! forkReviewer jobId worktree OpeningPrompt with
            | Error error -> return Error error
            | Ok reviewerSessionId ->
                match startBarrier journal managerSessionId reviewerSessionId barrierId tree with
                | Error error -> return Error error
                | Ok() ->
                    match! awaitReviewer jobId with
                    | Error error -> return Error error
                    | Ok() ->
                        match OrchestratorReviewRead.read journal reviewerSessionId tree with
                        | OrchestratorReviewRead.Confirmed -> return Ok()
                        | OrchestratorReviewRead.RevisionRequired -> return Error "Reviewer requested revision"
                        | OrchestratorReviewRead.NeedsReview -> return Error "Reviewer produced no verdict"
                        | OrchestratorReviewRead.PendingConfirmation ->
                            match! nudgeReviewer jobId ReviewChallenge.Text with
                            | Error error -> return Error error
                            | Ok() ->
                                match! awaitReviewer jobId with
                                | Error error -> return Error error
                                | Ok() ->
                                    match OrchestratorReviewRead.read journal reviewerSessionId tree with
                                    | OrchestratorReviewRead.Confirmed -> return Ok()
                                    | OrchestratorReviewRead.RevisionRequired ->
                                        return Error "Reviewer requested revision"
                                    // REVIEW-003 fail closed: the second PERFECT could not
                                    // be proven causal, so this barrier does not confirm.
                                    // Never fall back to same-root matching.
                                    | OrchestratorReviewRead.PendingConfirmation
                                    | OrchestratorReviewRead.NeedsReview ->
                                        return Error "Reviewer produced no confirmed verdict"
        }
