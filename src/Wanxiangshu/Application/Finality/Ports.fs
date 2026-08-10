namespace Wanxiangshu.Finality

open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity

/// Host capabilities Finality Application needs to enlist and drive reviewers
/// (rabbit §12.3). Adapters live in Infrastructure; this surface must not name
/// ToolRuntimeScope, ManagedAgent, OpenCode APIs, or directory registries.
type FinalityReviewerPort =
    { PrepareSession: FinalityReviewerRequest -> Task<Result<PreparedReviewer, string>>
      StartReview: EnlistedMember -> Task<Result<unit, string>>
      AwaitTerminal: SessionId -> Task<Result<unit, string>>
      SendRevisionSteer: SessionId -> string -> Task<Result<unit, string>>
      AbortReviewer: SessionId -> unit }

/// Read the Manager's current git tree for GLORY-058/059 equality checks.
type FinalityTreePort =
    { ReadManagerTree: SessionId -> Result<GitTreeHash, string> }
