namespace Wanxiangshu.Infrastructure.Resources

open System
open Wanxiangshu.Domain

/// Bundle of package runtime data loaded once at plugin init.
type RuntimeResources =
    { Prompts: PromptCatalog
      EnforcerRules: EnforcerRule list }

module RuntimeResources =

    // DSL-MUTABLE: resource — process-local installed runtime resources singleton
    let mutable private installed: RuntimeResources option = None

    let load () : RuntimeResources =
        let rules = EnforcerCatalogResource.load ()
        let prompts = PromptResources.load ()
        // Rulebook v2 Slice C: effective blogger system = base + all enforcer.md texts.
        // Derived only — never written back to resources/.
        let promptsWithRulebook =
            { prompts with
                BloggerSystemPrompt =
                    EnforcerCatalogResource.composeBloggerSystemPrompt prompts.BloggerSystemPrompt rules }

        { Prompts = promptsWithRulebook
          EnforcerRules = rules }

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
