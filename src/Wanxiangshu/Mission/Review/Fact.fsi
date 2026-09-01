namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

module ReviewFact =
    val inline ReviewBarrierStarted:
        payload:
            {| ReviewerSessionId: SessionId
               ManagerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash |} ->
            AgentFact

    val inline ReviewVerdictRecorded:
        payload:
            {| ReviewerSessionId: SessionId
               ManagerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               ProviderRun: ProviderRunIdentity
               ToolCallId: ToolCallId
               Verdict: ReviewGuardVerdict |} ->
            AgentFact

    val inline ReviewAttemptClosed:
        payload:
            {| ReviewerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               ProviderRun: ProviderRunIdentity
               ToolCallId: ToolCallId
               FrozenFrontierSequence: int64 |} ->
            AgentFact

    val inline ConfirmedReviewWitness:
        payload:
            {| ManagerJobId: ManagerJobId option
               ManagerSessionId: SessionId
               ReviewerSessionId: SessionId
               WorktreeIdentity: WorktreeIdentity option
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               FirstProviderRun: ProviderRunIdentity
               FirstToolCallId: ToolCallId
               FirstPhysicalUserMessageId: PhysicalUserMessageId
               SecondProviderRun: ProviderRunIdentity
               SecondToolCallId: ToolCallId
               SecondPhysicalUserMessageId: PhysicalUserMessageId |} ->
            AgentFact
