namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Change
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Replica

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity

/// Review-facing tree capability. Infrastructure owns the physical Git adapter;
/// Application only sees the read contract.
[<Struct>]
type GitTreePort = { GetTreeHash: unit -> string }

/// One typed `judge` delivery from the physical provider execution that called it.
type ReviewJudgement =
    { ReviewerSessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Verdict: ReviewGuardVerdict }

/// One physical `judge` request handed to the Finality CE. The callbacks finish
/// that exact tool call; they do not encode a review stage or return a workflow opcode.
type ReviewJudgementDelivery =
    { Judgement: ReviewJudgement
      Accept: unit -> unit
      Challenge: unit -> unit
      Reject: unit -> unit }

/// One-shot physical rendezvous. Calling `AwaitJudgement` registers the waiter
/// immediately and returns its Task; CE source order decides when it is called.
type ReviewJudgementChannel =
    { AwaitJudgement: unit -> Task<Result<ReviewJudgementDelivery, string>>
      Dispose: unit -> unit }

type ReviewHostPort =
    { StartReview: unit -> Task<Result<unit, string>>
      AwaitJudgement: unit -> Task<Result<ReviewJudgementDelivery, string>>
      AwaitReviewer: unit -> Task<Result<unit, string>>
      NudgeMissingJudgement: unit -> Task<Result<PhysicalUserMessageId, string>> }

/// Physical continuation capability retained for process-review missing-judge
/// repair. Finality challenge sequencing belongs exclusively to ReviewBarrierWorkflow.
type ReviewerContinuationPort =
    { NudgeMissingVerdict: SessionId -> Task<Result<unit, string>> }

/// HOST-021 / TODO-006: Host-owned process-review ensure and lag-1 wait.
/// After starts EnsureReview without waiting for ConsumableReview; T(k+1) /
/// suicide drain uses AwaitConsumableReview (Journal change, no wall-clock poll).
type ProcessReviewPort =
    { EnsureReview: AgentJournal -> SessionId -> ManagerLifeId -> TodoWriteId -> Task<Result<unit, string>>
      AwaitConsumableReview: AgentJournal -> SessionId -> ManagerLifeId -> TodoWriteId -> Task<Result<unit, string>> }
