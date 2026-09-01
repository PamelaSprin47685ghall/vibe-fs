namespace Wanxiangshu.Resources

open Wanxiangshu.Enforcer
open Wanxiangshu.Participant.Provider

module EnforcerCatalogResource =
    val composeBloggerSystemPromptFor:
        lang: ProviderLanguage -> baseInstructions: string list -> rules: EnforcerRule list -> string
    val composeBloggerSystemPrompt: rules: EnforcerRule list -> string
    val loadFor: lang: ProviderLanguage -> EnforcerRule list
    val load: unit -> EnforcerRule list
