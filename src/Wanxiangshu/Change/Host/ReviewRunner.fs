namespace Wanxiangshu.Change.Host
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence

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
open Wanxiangshu.Mission.Review
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
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement

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
