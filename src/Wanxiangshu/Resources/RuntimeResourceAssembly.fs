namespace Wanxiangshu.Resources

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider

module RuntimeResourceAssembly =

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
                    EnforcerCatalogResource.composeBloggerSystemPromptFor
                        lang
                        (PromptResources.instructionTextsForRole lang Role.Blogger)
                        rules }

        { Prompts = promptsWithRulebook
          EnforcerRules = rules
          EnglishEnforcerRules = englishRules
          SimplifiedChineseEnforcerRules = simplifiedChineseRules
          ProviderLanguageRootsReady = ProviderResources.languageRootsPresent () }

    let load () : RuntimeResources = loadFor ProviderLanguage.English
