namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Enforcer.InstitutionalLearning
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Requirement.Grounding
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Composition.Turn
open Wanxiangshu.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode

module AgentJournalPortAdapter =
    val forAttention: journal: AgentJournal -> AttentionJournalPort
    val forConcern: journal: AgentJournal -> ConcernJournalPort
    val forInstitutionalLearning: journal: AgentJournal -> InstitutionalLearningJournalPort
    val forDelegatedToolEstimate: journal: AgentJournal -> DelegatedToolEstimatePort
    val forSessionStartedAt: journal: AgentJournal -> SessionStartedAtPort
    val forProviderFailure: journal: AgentJournal -> ProviderFailureJournalPort
    val forRequirementGrounding: journal: AgentJournal -> RequirementGroundingPort
    val forSessionResume: journal: AgentJournal -> SessionResumeJournalPort
    val forWire: journal: AgentJournal -> WireJournalPort
    val forTurnObservation: journal: AgentJournal -> TurnObservationJournalPort
    val forTerminalPolicy: journal: AgentJournal -> TerminalPolicyPort
    val forHostJoinGuard: journal: AgentJournal -> HostJoinGuardJournalPort

    /// DELEG-029: durable composition is the only place that wraps delegation fact
    /// cases into the outer routing union and adapts the journal handle.
    val fromAgentJournal: journal: AgentJournal -> AgentJournalPort
