namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Attention
open Wanxiangshu.Persistence.Journal

module AgentJournalPortAdapter =
    val forAttention: journal: AgentJournal -> AttentionJournalPort

    /// DELEG-029: durable composition is the only place that wraps delegation fact
    /// cases into the outer routing union and adapts the journal handle.
    val fromAgentJournal: journal: AgentJournal -> AgentJournalPort
