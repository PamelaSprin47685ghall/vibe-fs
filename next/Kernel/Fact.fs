namespace Wanxiangshu.Next.Kernel

open System
open Wanxiangshu.Next.Kernel.Identity

module Fact =

    type RuntimeFact =
        | RuntimeStarted of
            {| RuntimeId: RuntimeId
               ProcessId: int
               StartedAt: DateTimeOffset |}

    [<RequireQualifiedAccess>]
    type ReviewGuardVerdict =
        | Perfect
        | Revise

    [<RequireQualifiedAccess>]
    type AgentFact =
        | CompanionBaselineSet of
            {| SessionId: SessionId
               Projection: string |}
        | CompanionCheckpointReplaced of
            {| SessionId: SessionId
               Content: string |}
        | CompanionAdvanced of
            {| SessionId: SessionId
               Projection: string
               Content: string |}
        | CompanionReplacementActiveSet of {| SessionId: SessionId; Active: bool |}
        | AgentLinked of
            {| ParentId: SessionId
               ChildId: ChildId
               TargetAgent: string
               Role: string option |}
        | AgentUnlinked of
            {| ParentId: SessionId
               ChildId: ChildId |}
        | ReviewBarrierStarted of
            {| ManagerSessionId: SessionId
               BarrierKey: string |}
        | ReviewVerdictRecorded of
            {| ManagerSessionId: SessionId
               ReviewerSessionId: SessionId
               ToolCallId: string
               GitTreeHash: string
               Verdict: ReviewGuardVerdict |}
        | GuardPromptAccepted of
            {| TargetSessionId: SessionId
               GuardKey: string
               HostMessageId: string |}
        | FallbackFailureRecorded of
            {| SessionId: SessionId
               Reason: string
               AssistantMessageId: string
               ProviderAttempt: string |}
        | OrchestratorManagerJobCreated of
            {| ManagerId: string
               WorktreePath: string
               Branch: string
               Prompt: string |}
        | OrchestratorCandidateRegistered of
            {| ManagerId: string
               CandidateId: string
               Branch: string
               CommitHash: string |}
        | OrchestratorPublished of
            {| ManagerId: string
               CandidateId: string
               CommitHash: string |}
        | OrchestratorRejected of
            {| ManagerId: string
               CandidateId: string
               Reason: string |}
        // Barrier facts (durable publish-chain checkpoint facts). Each records the
        // commit identity it was confirmed against so a re-run can skip it only when
        // the current HEAD still matches — stale barriers never match a new HEAD.
        | OrchestratorPreRebaseReviewConfirmed of
            {| ManagerId: string
               CandidateId: string
               CommitHash: string |}
        | OrchestratorRebased of
            {| ManagerId: string
               CandidateId: string
               RebasedCommit: string |}
        | OrchestratorConflictDetected of
            {| ManagerId: string
               CandidateId: string
               Files: string list |}
        | OrchestratorPostRebaseReviewConfirmed of
            {| ManagerId: string
               CandidateId: string
               RebasedCommit: string |}
        | OrchestratorPublishClaimed of
            {| ManagerId: string
               CandidateId: string
               ExpectedTargetHead: string |}
        | DurableEffectRequested of
            {| EffectId: string
               SessionId: SessionId
               Target: string
               Payload: string |}
        | DurableEffectAccepted of
            {| EffectId: string
               SessionId: SessionId
               Result: string |}

    type Fact =
        | Runtime of RuntimeFact
        | Agent of AgentFact
