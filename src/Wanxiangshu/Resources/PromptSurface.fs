namespace Wanxiangshu.Resources

open Wanxiangshu.Enforcer
open Wanxiangshu.Participant.Provider

/// JS-native owner boundary for the canonical provider prompt catalog and the
/// package runtime-resource bundle. Prompt composition remains owned by
/// PromptResources; this module only translates its records and localized
/// language values into plain JavaScript data (JS-SEMANTIC-SURFACE-003/005).
[<RequireQualifiedAccess>]
module PromptSurface =

    let private languageOf (raw: string) : ProviderLanguage =
        match raw with
        | "English" -> ProviderLanguage.English
        | "SimplifiedChinese"
        | "zh-CN" -> ProviderLanguage.SimplifiedChinese
        | _ -> ProviderLanguage.parse raw

    let private catalogToJs (catalog: PromptCatalog) : obj =
        box
            {| ManagerSystemPrompt = catalog.ManagerSystemPrompt
               CoderSystemPrompt = catalog.CoderSystemPrompt
               DevopsSystemPrompt = catalog.DevopsSystemPrompt
               InspectorSystemPrompt = catalog.InspectorSystemPrompt
               ReviewerSystemPrompt = catalog.ReviewerSystemPrompt
               BrowserSystemPrompt = catalog.BrowserSystemPrompt
               InquirySystemPrompt = catalog.InquirySystemPrompt
               OrchestratorSystemPrompt = catalog.OrchestratorSystemPrompt
               DistillerSystemPrompt = catalog.DistillerSystemPrompt
               BloggerSystemPrompt = catalog.BloggerSystemPrompt |}

    let private catalogValues (catalog: PromptCatalog) : string array =
        [| catalog.ManagerSystemPrompt
           catalog.CoderSystemPrompt
           catalog.DevopsSystemPrompt
           catalog.InspectorSystemPrompt
           catalog.ReviewerSystemPrompt
           catalog.BrowserSystemPrompt
           catalog.InquirySystemPrompt
           catalog.OrchestratorSystemPrompt
           catalog.DistillerSystemPrompt
           catalog.BloggerSystemPrompt |]

    let private ruleToJs (rule: EnforcerRule) : obj =
        box
            {| name = rule.Name
               enforcerText = rule.EnforcerText
               mainText = rule.MainText
               ruleId = rule.RuleId
               fieldName = rule.FieldName
               lexicalOrder = rule.LexicalOrder |}

    let private runtimeToJs (resources: RuntimeResources) : obj =
        box
            {| Prompts = catalogToJs resources.Prompts
               EnforcerRules = resources.EnforcerRules |> List.map ruleToJs |> List.toArray
               EnglishEnforcerRules = resources.EnglishEnforcerRules |> List.map ruleToJs |> List.toArray
               SimplifiedChineseEnforcerRules =
                resources.SimplifiedChineseEnforcerRules |> List.map ruleToJs |> List.toArray
               ProviderLanguageRootsReady = resources.ProviderLanguageRootsReady |}

    /// Canonical English ten-role prompt catalog.
    let load () : obj = PromptResources.load () |> catalogToJs

    /// Localized ten-role prompt catalog. Language crosses as a stable string.
    let loadForLanguage (language: string) : obj =
        PromptResources.loadForLanguage (languageOf language) |> catalogToJs

    /// The ten prompt bodies in the same public-role order as the catalog.
    let allForLanguage (language: string) : string array =
        PromptResources.loadForLanguage (languageOf language) |> catalogValues

    let loadBookkeeperSystem () : string = PromptResources.loadBookkeeperSystem ()

    let loadBookkeeperSystemFor (language: string) : string =
        PromptResources.loadBookkeeperSystemFor (languageOf language)

    /// Load the complete runtime package bundle as JS-native data.
    let runtimeLoad () : obj = RuntimeResources.load () |> runtimeToJs

    let runtimeLoadForLanguage (language: string) : obj =
        RuntimeResources.loadFor (languageOf language) |> runtimeToJs

    /// Match plugin initialization: install the package-owned bundle before
    /// consumers such as EnforcerTipGuidance resolve a localized rule.
    let runtimeInstallFromPackage () : unit =
        RuntimeResources.install (RuntimeResources.load ())

    let runtimeCurrent () : obj =
        RuntimeResources.current () |> runtimeToJs
