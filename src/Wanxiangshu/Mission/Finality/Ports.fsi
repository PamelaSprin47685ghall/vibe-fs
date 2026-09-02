namespace Wanxiangshu.Mission.Finality

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review

/// Host capabilities Finality Application needs to enlist and drive reviewers
/// (rabbit §12.3). Adapters live in Infrastructure; this surface must not name
/// ToolRuntimeScope, ManagedAgent, OpenCode APIs, or directory registries.
type FinalityReviewerPort =
    { PrepareSession: FinalityReviewerRequest -> Task<Result<PreparedReviewer, string>>
      StartReview: EnlistedMember -> Task<Result<unit, string>>
      OpenJudgementChannel: SessionId -> Result<ReviewJudgementChannel, string>
      AwaitTerminal: ReviewerTerminalOccasion -> Task<Result<ProviderRunIdentity, string>>
      NudgeMissingJudgement:
          SessionId -> ReviewBarrierId -> ProviderRunIdentity -> Task<Result<PhysicalUserMessageId, string>>
      SendRevisionSteer: SessionId -> string -> Task<Result<unit, string>>
      AbortReviewer: SessionId -> Task }

module FinalityReviewerPort =

    val abortAll: port: FinalityReviewerPort -> members: EnlistedMember list -> Task

/// Read the Manager's current git tree for GLORY-058/059 equality checks.
type FinalityTreePort =
    { ReadManagerTree: SessionId -> Result<GitTreeHash, string> }
