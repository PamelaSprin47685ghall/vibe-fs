namespace Wanxiangshu.Infrastructure.Resources

open System
open Wanxiangshu.Domain

/// Bundle of package runtime data loaded once at plugin init.
type RuntimeResources =
    { Prompts: PromptCatalog
      /// Rulebook selected for this RuntimeResources view (legacy/internal callers).
      EnforcerRules: EnforcerRule list
      EnglishEnforcerRules: EnforcerRule list
      SimplifiedChineseEnforcerRules: EnforcerRule list
      /// Phase 2: bilingual provider tree roots present (`resources/provider/{en,zh-CN}`).
      ProviderLanguageRootsReady: bool }

module RuntimeResources =

    // DSL-MUTABLE: resource — process-local installed runtime resources singleton
    let mutable private installed: RuntimeResources option = None

    let loadFor (lang: ProviderLanguage) : RuntimeResources =
        // PROMPT-017: preload both complete Rulebook locales once. Runtime session
        // projection can then select by immutable ProviderLanguage without request-time I/O.
        let englishRules = EnforcerCatalogResource.loadFor ProviderLanguage.English
        let simplifiedChineseRules =
            EnforcerCatalogResource.loadFor ProviderLanguage.SimplifiedChinese

        let rules =
            match lang with
            | ProviderLanguage.English -> englishRules
            | ProviderLanguage.SimplifiedChinese -> simplifiedChineseRules

        let prompts = PromptResources.loadForLanguage lang

        let promptsWithRulebook =
            { prompts with
                BloggerSystemPrompt =
                    EnforcerCatalogResource.composeBloggerSystemPromptFor lang prompts.BloggerSystemPrompt rules }

        { Prompts = promptsWithRulebook
          EnforcerRules = rules
          EnglishEnforcerRules = englishRules
          SimplifiedChineseEnforcerRules = simplifiedChineseRules
          ProviderLanguageRootsReady = ProviderResources.languageRootsPresent () }

    let load () : RuntimeResources = loadFor ProviderLanguage.English

    /// Single install site: plugin constructor before any consumer runs.
    let install (resources: RuntimeResources) : unit = installed <- Some resources

    let current () : RuntimeResources =
        match installed with
        | Some resources -> resources
        | None ->
            raise (
                InvalidOperationException(
                    "RuntimeResources not installed; call RuntimeResources.install (RuntimeResources.load ()) at plugin init"
                )
            )

    let enforcerRulesFor (lang: ProviderLanguage) : EnforcerRule list =
        let resources = current ()

        match lang with
        | ProviderLanguage.English -> resources.EnglishEnforcerRules
        | ProviderLanguage.SimplifiedChinese -> resources.SimplifiedChineseEnforcerRules
