namespace Wanxiangshu.Infrastructure.Resources

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// Role Law from `resources/provider/role/<name>/{en.md,zh-CN.md}` with legacy `resources/prompts/` fallback.
type PromptCatalog =
    { ManagerSystemPrompt: string
      CoderSystemPrompt: string
      DevopsSystemPrompt: string
      InspectorSystemPrompt: string
      ReviewerSystemPrompt: string
      BrowserSystemPrompt: string
      InquirySystemPrompt: string
      OrchestratorSystemPrompt: string
      DistillerSystemPrompt: string
      BloggerSystemPrompt: string }

module PromptResources =

    let private roleSemanticPaths =
        [ "role/manager", "manager-system.md"
          "role/coder", "coder-system.md"
          "role/devops", "devops-system.md"
          "role/inspector", "inspector-system.md"
          "role/reviewer", "reviewer-system.md"
          "role/browser", "browser-system.md"
          "role/inquiry", "inquiry-system.md"
          "role/orchestrator", "orchestrator-system.md"
          "role/distiller", "distiller-system.md"
          "role/blogger", "blogger-system.md"
          "role/bookkeeper", "bookkeeper-system.md" ]

    let private loadPrompt (lang: ProviderLanguage) (semanticPath: string) (legacyFile: string) : string =
        match ProviderResources.tryReadText lang semanticPath with
        | Some text -> text
        | None -> PackageResources.readText(sprintf "prompts/%s" legacyFile).Trim()

    let private ensurePromptParity () : unit =
        for semanticPath, _ in roleSemanticPaths do
            ProviderResources.requireLanguagePair semanticPath

    /// InternalLeaf Bookkeeper is not a Role; keep it off PromptCatalog.
    let loadBookkeeperSystemFor (lang: ProviderLanguage) : string =
        loadPrompt lang "role/bookkeeper" "bookkeeper-system.md"

    let loadBookkeeperSystem () : string =
        loadBookkeeperSystemFor ProviderLanguage.English

    let loadForLanguage (lang: ProviderLanguage) : PromptCatalog =
        ensurePromptParity ()

        { ManagerSystemPrompt = loadPrompt lang "role/manager" "manager-system.md"
          CoderSystemPrompt = loadPrompt lang "role/coder" "coder-system.md"
          DevopsSystemPrompt = loadPrompt lang "role/devops" "devops-system.md"
          InspectorSystemPrompt = loadPrompt lang "role/inspector" "inspector-system.md"
          ReviewerSystemPrompt = loadPrompt lang "role/reviewer" "reviewer-system.md"
          BrowserSystemPrompt = loadPrompt lang "role/browser" "browser-system.md"
          InquirySystemPrompt = loadPrompt lang "role/inquiry" "inquiry-system.md"
          OrchestratorSystemPrompt = loadPrompt lang "role/orchestrator" "orchestrator-system.md"
          DistillerSystemPrompt = loadPrompt lang "role/distiller" "distiller-system.md"
          BloggerSystemPrompt = loadPrompt lang "role/blogger" "blogger-system.md" }

    /// Default English catalog (plugin init / ManagedAgentConfig).
    let load () : PromptCatalog =
        loadForLanguage ProviderLanguage.English

    /// HOST-026: read bound session language; default English when unbound.
    let loadForSession (sessionId: SessionId) : PromptCatalog =
        let lang =
            SessionProviderLanguage.tryGet sessionId
            |> Option.defaultValue ProviderLanguage.English

        loadForLanguage lang
