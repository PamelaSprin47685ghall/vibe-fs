namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity

module OrchestratorReviewRead =
    /// Manager review-guard state for the current worktree.
    /// PendingConfirmation means a first PERFECT already landed and HostReviewGuard
    /// owns the confirmation nudge — Orchestrator must not re-fork a full review.
    type ReviewState =
        | Confirmed
        | PendingConfirmation
        | NeedsReview
        | RevisionRequired

    let read
        (journal: AgentJournal option)
        (reviewOwnerSessionId: SessionId)
        (worktree: string)
        : Task<Result<ReviewState, string>> =
        task {
            match journal with
            | None -> return Error "Orchestrator review requires a journal"
            | Some journal ->
                let tree = (GitTree.create worktree).GetTreeHash()
                let snapshot = AgentJournal.snapshot journal

                // reviewOwnerSessionId = durable parent of the reviewer for this
                // barrier (Orchestrator session when OrchestratorHost forks the
                // reviewer; Manager session for Manager-owned reviewers).
                match Map.tryFind reviewOwnerSessionId snapshot.AgentProjections.Sessions with
                | Some session ->
                    match session.ReviewGuard with
                    | Some guard when guard.LastGitTreeHash = Some(GitTreeHash.create tree) && guard.IsConfirmed ->
                        return Ok Confirmed
                    | Some guard when
                        guard.LastGitTreeHash = Some(GitTreeHash.create tree)
                        && guard.ConsecutivePerfects = 1
                        && not guard.IsConfirmed
                        ->
                        return Ok PendingConfirmation
                    | Some guard when guard.LastGitTreeHash = Some(GitTreeHash.create tree) ->
                        return Ok RevisionRequired
                    | _ -> return Ok NeedsReview
                | None -> return Ok NeedsReview
        }
