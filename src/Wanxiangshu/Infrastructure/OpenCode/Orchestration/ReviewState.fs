namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// REVIEW-007/008: what one reviewer session's guard says about the current tree.
module OrchestratorReviewRead =

    /// Keyed by the REVIEWER session, which is where REVIEW-003's facts land.
    ///
    /// `PendingConfirmation` means a first PERFECT landed and its challenge is
    /// outstanding, so the Orchestrator must nudge the same reviewer rather than fork
    /// a second review — forking again would open a new barrier and discard the first
    /// PERFECT.
    type ReviewStatus =
        | Confirmed
        | PendingConfirmation
        | NeedsReview
        | RevisionRequired

    /// Synchronous: this is one keyed projection lookup, no I/O.
    ///
    /// The tree is a parameter rather than a worktree path to hash here. The caller
    /// reads it once per barrier and uses the same value for the barrier fact and for
    /// this read; hashing again per call could observe a different tree mid-review and
    /// silently answer about a different one.
    let read (journal: AgentJournal option) (reviewerSessionId: SessionId) (tree: GitTreeHash) : ReviewStatus =
        let guard =
            journal
            |> Option.bind (fun durable ->
                AgentProjection.tryFind reviewerSessionId (AgentJournal.snapshot durable).AgentProjections)
            |> Option.bind (fun session -> session.ReviewGuard)

        match guard with
        | None -> NeedsReview
        | Some value ->
            // REVIEW-008: a witness for another tree stays auditable but is not
            // sufficient, so validity is asked against the tree in hand.
            // `satisfiesGuard` owns that question; the previous version compared
            // `LastGitTreeHash` inline in three branches, which is the same rule
            // spelled a fourth time.
            //
            // REVISE / pending-PERFECT are barrier-scoped: a historical Reviewer's
            // previous request must not short-circuit a fresh barrier (GLORY-045).
            if ReviewProjection.satisfiesGuard tree value then
                Confirmed
            elif
                ReviewWitness.isRevision value.Witness
                && value.LastGitTreeHash = Some tree
                && value.CurrentBarrierId.IsSome
            then
                RevisionRequired
            elif
                ReviewWitness.isPerfectPending value.Witness
                && value.LastGitTreeHash = Some tree
            then
                PendingConfirmation
            else
                NeedsReview
