namespace Wanxiangshu.Mission.Finality
open Wanxiangshu.Change
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

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
