namespace Wanxiangshu.Change.Host

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Change
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

/// One review barrier, driven by the Orchestrator (REVIEW-008, REVIEW-009).
///
/// GLORY-042/044: the algorithm lives in Application `ReviewBarrierWorkflow`;
/// this module only adapts its typed outcome to the Orchestrator's
/// `Result<unit, string>` contract (REVISE maps to the existing
/// "Reviewer requested revision" error, keeping ORCH-009 publication semantics).
module OrchestratorHostReview =

    let DeepReviewerAgent = ManagedAgent.nameOf Role.Reviewer

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
        (awaitReviewer: ReviewerTerminalOccasion -> Task<Result<ProviderRunIdentity, string>>)
        (nudgeReviewer: SessionId -> ProviderRunIdentity -> Task<Result<PhysicalUserMessageId, string>>)
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
                let terminalOccasion =
                    { ReviewerSessionId = reviewerSessionId
                      BarrierId = barrierId }

                { StartReview = fun () -> startReviewer jobId
                  AwaitJudgement = channel.AwaitJudgement
                  AwaitReviewer = fun () -> awaitReviewer terminalOccasion
                  NudgeMissingJudgement = fun terminalProviderRun -> nudgeReviewer reviewerSessionId terminalProviderRun }

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
