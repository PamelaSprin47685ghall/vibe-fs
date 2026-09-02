namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Persistence.Journal

type AgentJournalCompanionPort =
    new: journal: AgentJournal -> AgentJournalCompanionPort
    interface ICompanionDurablePort
