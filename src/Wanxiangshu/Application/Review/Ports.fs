namespace Wanxiangshu.Review

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Domain.MagicTodo
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

/// HOST-021 / TODO-006: Host-owned process-review ensure and lag-1 wait.
/// After starts EnsureReview without waiting for ConsumableReview; T(k+1) /
/// suicide drain uses AwaitConsumableReview (Journal change, no wall-clock poll).
type ProcessReviewPort =
    { EnsureReview: AgentJournal -> SessionId -> ManagerLifeId -> TodoWriteId -> Task<Result<unit, string>>
      AwaitConsumableReview: AgentJournal -> SessionId -> ManagerLifeId -> TodoWriteId -> Task<Result<unit, string>> }
