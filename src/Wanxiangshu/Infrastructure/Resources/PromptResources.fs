namespace Wanxiangshu.Infrastructure.Resources

/// Role system prompts from resources/prompts/*-system.md.
/// Explicit load only — module import does not read files.
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

    let private loadPrompt (fileName: string) : string =
        PackageResources.readText(sprintf "prompts/%s" fileName).Trim()

    /// InternalLeaf Bookkeeper is not a Role; keep it off PromptCatalog.
    let loadBookkeeperSystem () : string = loadPrompt "bookkeeper-system.md"

    let load () : PromptCatalog =
        { ManagerSystemPrompt = loadPrompt "manager-system.md"
          CoderSystemPrompt = loadPrompt "coder-system.md"
          DevopsSystemPrompt = loadPrompt "devops-system.md"
          InspectorSystemPrompt = loadPrompt "inspector-system.md"
          ReviewerSystemPrompt = loadPrompt "reviewer-system.md"
          BrowserSystemPrompt = loadPrompt "browser-system.md"
          InquirySystemPrompt = loadPrompt "inquiry-system.md"
          OrchestratorSystemPrompt = loadPrompt "orchestrator-system.md"
          DistillerSystemPrompt = loadPrompt "distiller-system.md"
          BloggerSystemPrompt = loadPrompt "blogger-system.md" }
