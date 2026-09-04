namespace Wanxiangshu.Resources

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

type PromptCatalog =
    { ManagerSystemPrompt: string
      CoderSystemPrompt: string
      DevopsSystemPrompt: string
      InspectorSystemPrompt: string
      BrowserSystemPrompt: string
      InquirySystemPrompt: string
      OrchestratorSystemPrompt: string
      DistillerSystemPrompt: string
      BloggerSystemPrompt: string }

module PromptResources =
    val instructionTextsForRole: lang: ProviderLanguage -> role: Role -> string list
    val systemForRole: lang: ProviderLanguage -> role: Role -> string
    val bookkeeperInstructionTextsFor: lang: ProviderLanguage -> string list
    val loadBookkeeperSystemFor: lang: ProviderLanguage -> string
    val loadBookkeeperSystem: unit -> string
    val loadForLanguage: lang: ProviderLanguage -> PromptCatalog
    val load: unit -> PromptCatalog
