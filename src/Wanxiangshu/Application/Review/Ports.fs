namespace Wanxiangshu.Review

open System.Threading.Tasks
open Wanxiangshu.Kernel.Identity

/// Review-facing tree capability. Infrastructure owns the physical Git adapter;
/// Application only sees the read contract.
[<Struct>]
type GitTreePort = { GetTreeHash: unit -> string }

/// Physical reviewer capabilities used by one review barrier drive.
type ReviewHostPort =
    { ForkReviewer: unit -> Task<Result<SessionId, string>>
      AwaitReviewer: unit -> Task<Result<unit, string>> }

/// Physical continuation capability for ReviewerWorkflow. Application owns the
/// semantic decision of when a verdict/challenge must be ensured; Infrastructure
/// owns transport dedupe and Host delivery.
type ReviewerContinuationPort =
    { NudgeMissingVerdict: SessionId -> ProviderRunIdentity -> Task<Result<unit, string>>
      SendPerfectChallenge: SessionId -> ProviderRunIdentity -> Task<Result<unit, string>> }
