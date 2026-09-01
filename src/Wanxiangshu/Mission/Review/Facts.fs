namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Foundation.Identity

/// REVIEW-001: the judge tool accepts exactly these verdicts.
[<RequireQualifiedAccess>]
type ReviewGuardVerdict =
    | Perfect
    | Revise

/// Durable review facts owned by the review boundary.
type ReviewFactCases =
    | ReviewBarrierStarted of
        {| ReviewerSessionId: SessionId
           ManagerSessionId: SessionId
           BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash |}
    | ReviewVerdictRecorded of
        {| ReviewerSessionId: SessionId
           ManagerSessionId: SessionId
           BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash
           ProviderRun: ProviderRunIdentity
           ToolCallId: ToolCallId
           Verdict: ReviewGuardVerdict |}
    | ReviewAttemptClosed of
        {| ReviewerSessionId: SessionId
           BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash
           ProviderRun: ProviderRunIdentity
           ToolCallId: ToolCallId
           FrozenFrontierSequence: int64 |}
    | ConfirmedReviewWitness of
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
           SecondPhysicalUserMessageId: PhysicalUserMessageId |}
