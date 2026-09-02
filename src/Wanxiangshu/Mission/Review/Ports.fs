namespace Wanxiangshu.Mission.Review

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review

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

/// One physical review occasion whose Host terminal is being awaited.
/// Reviewer sessions are intentionally reusable, so a session id alone cannot
/// authorize an Abort as the clean terminal of whichever barrier happens to be
/// current later. The barrier identity travels with the physical wait.
[<Struct>]
type ReviewerTerminalOccasion =
    { ReviewerSessionId: SessionId
      BarrierId: ReviewBarrierId }

type ReviewHostPort =
    { StartReview: unit -> Task<Result<unit, string>>
      AwaitJudgement: unit -> Task<Result<ReviewJudgementDelivery, string>>
      AwaitReviewer: unit -> Task<Result<ProviderRunIdentity, string>>
      NudgeMissingJudgement: ProviderRunIdentity -> Task<Result<PhysicalUserMessageId, string>> }

/// Physical continuation capability retained for missing-judge repair.
/// Finality challenge sequencing belongs exclusively to ReviewBarrierWorkflow.
type ReviewerContinuationPort =
    { NudgeMissingVerdict: SessionId -> ProviderRunIdentity -> Task<Result<unit, string>> }
