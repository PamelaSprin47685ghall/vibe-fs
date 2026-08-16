namespace Wanxiangshu.Mission.Review

open Wanxiangshu.Foundation
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
    | PerfectChallengeIssued of
        {| BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash
           ReviewerSessionId: SessionId
           FirstProviderRun: ProviderRunIdentity
           FirstToolCallId: ToolCallId
           ChallengeTextVersion: int
           ChallengeContentDigest: SealDigest |}
    | ProviderInputSealed of
        {| SessionId: SessionId
           ProviderRun: ProviderRunIdentity
           PhysicalUserMessageId: PhysicalUserMessageId
           SealDigest: SealDigest
           CanonicalVersion: int
           IncludedToolResultDigests: SealDigest list |}
    | ConfirmedReviewWitness of
        {| ManagerJobId: ManagerJobId option
           ManagerSessionId: SessionId
           ReviewerSessionId: SessionId
           WorktreeIdentity: WorktreeIdentity option
           BarrierId: ReviewBarrierId
           GitTreeHash: GitTreeHash
           FirstProviderRun: ProviderRunIdentity
           FirstToolCallId: ToolCallId
           ChallengeResultDigest: SealDigest
           SecondProviderRun: ProviderRunIdentity
           SecondProviderInputDigest: SealDigest
           SecondToolCallId: ToolCallId |}
