namespace Wanxiangshu.Composition.Durable

open System
open Wanxiangshu.Change
open Wanxiangshu.Context.Companion
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Participant.Provider.Attempt.Fallback

/// Durable routing vocabulary. Concrete fact families live with their semantic
/// owners; this module only joins them into the journal's outer dispatch.
module Fact =

    type RuntimeFact =
        | RuntimeStarted of
            {| RuntimeId: RuntimeId
               ProcessId: int
               StartedAt: DateTimeOffset |}

    /// One journal line for the agent domain: exactly one owned family.
    [<RequireQualifiedAccess>]
    type AgentFact =
        | Prompt of PromptFactCases
        | Fallback of FallbackFactCases
        | Review of ReviewFactCases
        | Execution of ExecutionFactCases
        | Orchestrator of OrchestratorFactCases
        | Companion of CompanionFactCases
        | Context of ContextFactCases
        | Host of HostFactCases
        | Fission of FissionFactCases
        | Delegation of DelegationFactCases

    module DelegationFact =
        let inline DelegatedToolEstimateReplaced payload =
            AgentFact.Delegation(DelegationFactCases.DelegatedToolEstimateReplaced payload)

        let inline DelegatedToolCallObserved payload =
            AgentFact.Delegation(DelegationFactCases.DelegatedToolCallObserved payload)

    module FissionFact =
        let inline FissionAdmitted payload =
            AgentFact.Fission(FissionFactCases.FissionAdmitted payload)

        let inline FissionLaneMaterialized payload =
            AgentFact.Fission(FissionFactCases.FissionLaneMaterialized payload)

        let inline FissionCompletionCaptured payload =
            AgentFact.Fission(FissionFactCases.FissionCompletionCaptured payload)

        let inline FissionCompletionDelivered payload =
            AgentFact.Fission(FissionFactCases.FissionCompletionDelivered payload)

        let inline FissionExternalAffinityBound payload =
            AgentFact.Fission(FissionFactCases.FissionExternalAffinityBound payload)

        let inline FissionConverged payload =
            AgentFact.Fission(FissionFactCases.FissionConverged payload)

        let inline FissionFailed payload =
            AgentFact.Fission(FissionFactCases.FissionFailed payload)

    module HostFact =
        let inline PairProgrammingGuidelineAnchored payload =
            AgentFact.Host(HostFactCases.PairProgrammingGuidelineAnchored payload)

        let inline TipGuidanceDelivered payload =
            AgentFact.Host(HostFactCases.TipGuidanceDelivered payload)

        let inline SessionStartedAtBound payload =
            AgentFact.Host(HostFactCases.SessionStartedAtBound payload)

    module PromptFact =
        let inline PluginPromptClaimed payload =
            AgentFact.Prompt(PromptFactCases.PluginPromptClaimed payload)

        let inline PluginPromptSubmitted payload =
            AgentFact.Prompt(PromptFactCases.PluginPromptSubmitted payload)

        let inline PluginPromptPhysicalAccepted payload =
            AgentFact.Prompt(PromptFactCases.PluginPromptPhysicalAccepted payload)

        let inline PluginPromptAbandoned payload =
            AgentFact.Prompt(PromptFactCases.PluginPromptAbandoned payload)

        let inline AuthorityRootAccepted payload =
            AgentFact.Prompt(PromptFactCases.AuthorityRootAccepted payload)

    module FallbackFact =
        let inline FallbackCursorAdvanced payload =
            AgentFact.Fallback(FallbackFactCases.FallbackCursorAdvanced payload)

        let inline FallbackExhausted payload =
            AgentFact.Fallback(FallbackFactCases.FallbackExhausted payload)

    module ReviewFact =
        let inline ReviewBarrierStarted payload =
            AgentFact.Review(ReviewFactCases.ReviewBarrierStarted payload)

        let inline ReviewVerdictRecorded payload =
            AgentFact.Review(ReviewFactCases.ReviewVerdictRecorded payload)

        let inline ReviewAttemptClosed payload =
            AgentFact.Review(ReviewFactCases.ReviewAttemptClosed payload)

        let inline PerfectChallengeIssued payload =
            AgentFact.Review(ReviewFactCases.PerfectChallengeIssued payload)

        let inline ProviderInputSealed payload =
            AgentFact.Review(ReviewFactCases.ProviderInputSealed payload)

        let inline ConfirmedReviewWitness payload =
            AgentFact.Review(ReviewFactCases.ConfirmedReviewWitness payload)

    module ExecutionFact =
        let inline HandleLinked payload =
            AgentFact.Execution(ExecutionFactCases.HandleLinked payload)

        let inline HandleCompleted payload =
            AgentFact.Execution(ExecutionFactCases.HandleCompleted payload)

        let inline HandleRetired payload =
            AgentFact.Execution(ExecutionFactCases.HandleRetired payload)

        let inline HandleAbandoned payload =
            AgentFact.Execution(ExecutionFactCases.HandleAbandoned payload)

        let inline HandleFalseCompletionRejected payload =
            AgentFact.Execution(ExecutionFactCases.HandleFalseCompletionRejected payload)

        let inline HandleFalseTerminalReported payload =
            AgentFact.Execution(ExecutionFactCases.HandleFalseTerminalReported payload)

        let inline ParentJoinCorrectionRequested payload =
            AgentFact.Execution(ExecutionFactCases.ParentJoinCorrectionRequested payload)

        let inline HostTurnObserved payload =
            AgentFact.Execution(ExecutionFactCases.HostTurnObserved payload)

    module OrchestratorFact =
        let inline ManagerJobCreated payload =
            AgentFact.Orchestrator(OrchestratorFactCases.ManagerJobCreated payload)

        let inline CandidateReady payload =
            AgentFact.Orchestrator(OrchestratorFactCases.CandidateReady payload)

        let inline ConflictDetected payload =
            AgentFact.Orchestrator(OrchestratorFactCases.ConflictDetected payload)

        let inline RebasedCandidateReady payload =
            AgentFact.Orchestrator(OrchestratorFactCases.RebasedCandidateReady payload)

        let inline PublishClaimed payload =
            AgentFact.Orchestrator(OrchestratorFactCases.PublishClaimed payload)

        let inline Published payload =
            AgentFact.Orchestrator(OrchestratorFactCases.Published payload)

        let inline JobFailed payload =
            AgentFact.Orchestrator(OrchestratorFactCases.JobFailed payload)

        let inline JobAbandoned payload =
            AgentFact.Orchestrator(OrchestratorFactCases.JobAbandoned payload)

        let inline WorktreeCreateRequested payload =
            AgentFact.Orchestrator(OrchestratorFactCases.WorktreeCreateRequested payload)

        let inline WorktreeCreated payload =
            AgentFact.Orchestrator(OrchestratorFactCases.WorktreeCreated payload)

    module CompanionFact =
        let inline CompanionBloggerLinked payload =
            AgentFact.Companion(CompanionFactCases.CompanionBloggerLinked payload)

        let inline CompanionBloggerClosed payload =
            AgentFact.Companion(CompanionFactCases.CompanionBloggerClosed payload)

        let inline OpeningPromptCaptured payload =
            AgentFact.Companion(CompanionFactCases.OpeningPromptCaptured payload)

        let inline XTracePartAppended payload =
            AgentFact.Companion(CompanionFactCases.XTracePartAppended payload)

        let inline TerminalOutputCaptured payload =
            AgentFact.Companion(CompanionFactCases.TerminalOutputCaptured payload)

    module ContextFact =
        let inline BlogObservationCommitted payload =
            AgentFact.Context(ContextFactCases.BlogObservationCommitted payload)

        let inline BlogObservationsSquashed payload =
            AgentFact.Context(ContextFactCases.BlogObservationsSquashed payload)

        let inline BloggerRequestMaterialized payload =
            AgentFact.Context(ContextFactCases.BloggerRequestMaterialized payload)

        let inline BloggerRequestAbandoned payload =
            AgentFact.Context(ContextFactCases.BloggerRequestAbandoned payload)

        let inline PrefixRebaseCommitted payload =
            AgentFact.Context(ContextFactCases.PrefixRebaseCommitted payload)

        let inline ContextReanchored payload =
            AgentFact.Context(ContextFactCases.ContextReanchored payload)

    type Fact =
        | Runtime of RuntimeFact
        | Agent of AgentFact
        | ManagerLifecycle of ManagerLifecycleFact
        /// Typed Magic Todo facts cross this earlier journal boundary as canonical bytes.
        | MagicTodo of payload: string
