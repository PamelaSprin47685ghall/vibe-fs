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
        /// A non-joinable session association (for example, a Companion Blogger).
        | AgentLinked of
            {| ParentId: SessionId
               ChildId: ChildId
               TargetAgent: string
               Role: string option |}
        /// A direct child created through this session's fork runtime. Only these
        /// links may be restored into that runtime's join mailbox.
        | AgentForked of
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
               ProviderRunId: string
               UserPromptText: string option
               UserMessageId: string option
               ToolCallId: string
               GitTreeHash: string
               Verdict: ReviewGuardVerdict |}
        /// A verified HumanRoot message identity. Content remains in the Host transcript.
        | HumanPromptAccepted of
            {| SessionId: SessionId
               SourceSessionId: SessionId
               MessageId: string |}
        /// A double-PERFECT reviewer reached its terminal idle turn.
        | ReviewConfirmedIdle of
            {| SessionId: SessionId
               ReviewerSessionId: SessionId
               AssistantMessageId: string |}
        | GuardPromptAccepted of
            {| TargetSessionId: SessionId
               GuardKey: string
               HostMessageId: string |}
        | FallbackFailureRecorded of
            {| SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               Reason: string
               AssistantMessageId: string
               ProviderAttempt: string |}
        | AuthorityRootAccepted of
            {| SessionId: SessionId
               LogicalRunId: string
               HostMessageId: string
               AuthorityKind: string
               SelectedAgent: string
               PeerAgent: string
               CanonicalRole: string
               SelectedTier: string |}
        | PluginPromptClaimed of
            {| PromptKey: string
               SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               ContinuationKind: string
               EffectiveAgent: string option |}
        | PluginPromptAccepted of
            {| PromptKey: string
               SessionId: SessionId
               HostMessageId: string |}
        | PluginPromptAbandoned of
            {| PromptKey: string
               SessionId: SessionId
               Reason: string |}
        | InteractionRepairClaimed of
            {| SessionId: SessionId
               LogicalRunId: string
               AuthorityRootUserMessageId: string
               TerminalAssistantMessageId: string
               RepairKind: string |}
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
        | CompanionEpochSwitched of
            {| SessionId: SessionId
               EpochId: string
               FrozenB: string
               CutoffMessageIndex: int
               CoveredPrefixDigest: string |}

    type Fact =
        | Runtime of RuntimeFact
        | Agent of AgentFact
