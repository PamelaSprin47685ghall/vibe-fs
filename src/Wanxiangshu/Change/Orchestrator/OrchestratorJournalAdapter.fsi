namespace Wanxiangshu.Change

open Wanxiangshu.Persistence.Journal

module OrchestratorJournalAdapter =
    val forSweep: journal: AgentJournal -> OrchestratorSweepPort
    val forRelay: journal: AgentJournal -> OrchestratorRelayPort
    val forEngine: journal: AgentJournal -> OrchestratorJournalPort
