namespace Wanxiangshu.OpenCode

open Wanxiangshu.Participant.Provider
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

module BloggerChronicleText =
    val maybeInject:
        journal: AgentJournal option ->
        projectionSessionIdOpt: string option ->
        physicalUserMessageId: PhysicalUserMessageId option ->
        language: ProviderLanguage ->
        outObj: obj ->
            unit
