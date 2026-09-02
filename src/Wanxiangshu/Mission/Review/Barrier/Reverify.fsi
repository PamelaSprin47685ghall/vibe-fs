namespace Wanxiangshu.Mission.Review.Barrier

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type ReviewBarrierOutcome =
    | Confirmed of reviewerSessionId: SessionId * barrierId: ReviewBarrierId * gitTreeHash: GitTreeHash
    | RevisionRequired of reviewerSessionId: SessionId * barrierId: ReviewBarrierId * gitTreeHash: GitTreeHash

type ReviewBarrierRequest =
    { ManagerSessionId: SessionId
      ManagerJobId: ManagerJobId option
      WorktreeIdentity: WorktreeIdentity option
      ReviewerSessionId: SessionId
      BarrierId: ReviewBarrierId
      GitTreeHash: GitTreeHash }

[<RequireQualifiedAccess>]
type ReviewBarrierFailure =
    | JournalUnavailable
    | CannotStartReviewer of string
    | CannotAwaitReviewer of string
    | CannotAwaitJudgement of string
    | CannotNudgeReviewer of string
    | CannotRecordJudgement of string
    | InvalidJudgement of string

/// Finality dual-PERFECT temporal owner. First/challenge/second exist only as CE
/// locals; durable review facts are outputs and are never read back to select a step.
module ReviewBarrierWorkflow =

    val reverify:
        journal: AgentJournal option ->
        host: ReviewHostPort ->
        request: ReviewBarrierRequest ->
            Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>>
