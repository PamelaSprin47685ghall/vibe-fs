namespace Wanxiangshu.Resources

open Wanxiangshu.Enforcer
open Wanxiangshu.Participant.Provider

type RuntimeResources =
    { Prompts: PromptCatalog
      EnforcerRules: EnforcerRule list
      EnglishEnforcerRules: EnforcerRule list
      SimplifiedChineseEnforcerRules: EnforcerRule list
      ProviderLanguageRootsReady: bool }

module RuntimeResources =
    val install: resources: RuntimeResources -> unit
    val current: unit -> RuntimeResources
    val enforcerRulesFor: lang: ProviderLanguage -> EnforcerRule list
