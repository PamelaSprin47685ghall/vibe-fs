namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Requirement.Grounding
open Wanxiangshu.Persistence.Journal

module AgentJournalPortAdapter =
    val forAttention: journal: AgentJournal -> AttentionJournalPort
    val forDelegatedToolEstimate: journal: AgentJournal -> DelegatedToolEstimatePort
    val forSessionStartedAt: journal: AgentJournal -> SessionStartedAtPort
    val forProviderFailure: journal: AgentJournal -> ProviderFailureJournalPort
    val forRequirementGrounding: journal: AgentJournal -> RequirementGroundingPort

    /// DELEG-029: durable composition is the only place that wraps delegation fact
    /// cases into the outer routing union and adapts the journal handle.
    val fromAgentJournal: journal: AgentJournal -> AgentJournalPort
