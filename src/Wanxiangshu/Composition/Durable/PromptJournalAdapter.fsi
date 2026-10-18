namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Persistence.Journal

module PromptJournalAdapter =
    val create: journal: AgentJournal -> IPromptJournal
    val forJournal: journal: AgentJournal -> PromptDispatcher.Runtime
