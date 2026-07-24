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
        | CompanionReplacementActiveSet of {| SessionId: SessionId; Active: bool |}
        | AgentLinked of
            {| ParentId: SessionId
               ChildId: ChildId
               TargetAgent: string |}
        | AgentUnlinked of
            {| ParentId: SessionId
               ChildId: ChildId |}
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
               Reason: string |}
        | OrchestratorManagerJobCreated of
            {| ManagerId: string
               WorktreePath: string
               Branch: string |}
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
