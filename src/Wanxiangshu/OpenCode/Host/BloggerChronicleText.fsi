namespace Wanxiangshu.OpenCode

open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module BloggerChronicleText =
    val maybeInject:
        journal: AgentJournal option ->
        projectionSessionIdOpt: string option ->
        language: ProviderLanguage ->
        outObj: obj ->
            unit
