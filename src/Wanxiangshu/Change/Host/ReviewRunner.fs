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
open FsToolkit.ErrorHandling
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

    let private openingPrompt (managerSessionId: SessionId) =
        ProviderProse.render (ProviderProse.languageOf managerSessionId) HostReviewPrompt.Opening Map.empty

    let private describeBarrierFailure =
        function
        | ReviewBarrierFailure.JournalUnavailable -> "Review journal is unavailable"
        | ReviewBarrierFailure.CannotStartReviewer reason -> sprintf "Cannot start reviewer: %s" reason
        | ReviewBarrierFailure.CannotAwaitReviewer reason -> sprintf "Cannot await reviewer: %s" reason
        | ReviewBarrierFailure.CannotAwaitJudgement reason -> sprintf "Cannot await reviewer judgement: %s" reason
        | ReviewBarrierFailure.CannotNudgeReviewer reason -> sprintf "Cannot nudge reviewer: %s" reason
        | ReviewBarrierFailure.CannotRecordJudgement reason -> sprintf "Cannot record reviewer judgement: %s" reason
        | ReviewBarrierFailure.InvalidJudgement reason -> sprintf "Invalid reviewer judgement: %s" reason

    let private runBarrierWithChannel
        (durable: AgentJournal)
        (channel: ReviewJudgementChannel)
        (host: ReviewHostPort)
        (request: ReviewBarrierRequest)
        : Task<Result<ReviewBarrierOutcome, string>> =
        taskResult {
            try
                return!
                    ReviewBarrierWorkflow.reverify (Some durable) host request
                    |> TaskResult.mapError describeBarrierFailure
            finally
                channel.Dispose()
        }

    let reverify
        (journal: AgentJournal option)
        (forkReviewer: ManagerJobId -> WorktreePath -> string -> Task<Result<SessionId, string>>)
        (startReviewer: ManagerJobId -> Task<Result<unit, string>>)
        (awaitReviewer: ManagerJobId -> Task<Result<unit, string>>)
        (nudgeReviewer: SessionId -> Task<Result<PhysicalUserMessageId, string>>)
        (jobId: ManagerJobId)
        (managerSessionId: SessionId)
        (worktree: WorktreePath)
        (barrierId: ReviewBarrierId)
        : Task<Result<unit, string>> =
        taskResult {
            let! durable = journal |> Result.requireSome "Review journal is unavailable"

            let tree =
                GitTreeHash.create ((GitTree.create (WorktreePath.value worktree)).GetTreeHash())

            let! reviewerSessionId = forkReviewer jobId worktree (openingPrompt managerSessionId)

            do! ReviewBarrier.openBarrier (Some durable) managerSessionId reviewerSessionId barrierId tree

            let! channel =
                ReviewJudgementInbox.acquire reviewerSessionId
                |> Result.mapError (sprintf "Cannot await reviewer judgement: %s")

            let host: ReviewHostPort =
                { StartReview = fun () -> startReviewer jobId
                  AwaitJudgement = channel.AwaitJudgement
                  AwaitReviewer = fun () -> awaitReviewer jobId
                  NudgeMissingJudgement = fun () -> nudgeReviewer reviewerSessionId }

            let request =
                { ManagerSessionId = managerSessionId
                  ManagerJobId = Some jobId
                  WorktreeIdentity = Some(WorktreeCommands.identityOf jobId)
                  ReviewerSessionId = reviewerSessionId
                  BarrierId = barrierId
                  GitTreeHash = tree }

            let! outcome = runBarrierWithChannel durable channel host request

            return!
                match outcome with
                | ReviewBarrierOutcome.Confirmed _ -> Ok()
                | ReviewBarrierOutcome.RevisionRequired _ -> Error "Reviewer requested revision"
        }
