namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// HOST-026 / PROMPT-017: project the session-bound ProviderLanguage onto the
/// Wanxiangshu-owned system-prompt segment without disturbing Host/AGENTS text.
module ProviderSystemTransform =

    let private roleFor (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            let projections = (AgentJournal.snapshot durable).AgentProjections

            PromptAuthorityLedger.activeProfile sessionId projections
            |> Option.orElseWith (fun () -> PromptAuthorityLedger.lastAuthorityProfile sessionId projections))
        |> Option.map (fun profile -> profile.CanonicalRole)

    let private catalogPrompt (catalog: PromptCatalog) =
        function
        | Role.Manager -> catalog.ManagerSystemPrompt
        | Role.Orchestrator -> catalog.OrchestratorSystemPrompt
        | Role.Coder -> catalog.CoderSystemPrompt
        | Role.Inspector -> catalog.InspectorSystemPrompt
        | Role.Browser -> catalog.BrowserSystemPrompt
        | Role.Inquiry -> catalog.InquirySystemPrompt
        | Role.Reviewer -> catalog.ReviewerSystemPrompt
        | Role.DevOps -> catalog.DevopsSystemPrompt
        | Role.Distiller -> catalog.DistillerSystemPrompt
        | Role.Blogger -> catalog.BloggerSystemPrompt

    let private localizedRolePrompt lang role =
        let basePrompt = PromptResources.systemForRole lang role

        match role with
        | Role.Blogger ->
            EnforcerCatalogResource.composeBloggerSystemPromptFor
                lang
                basePrompt
                (RuntimeResources.enforcerRulesFor lang)
        | _ -> basePrompt

    let private replaceOwnedSegment (oldPrompt: string) (nextPrompt: string) (system: string array) =
        let canonical (text: string) = if isNull text then "" else text.Trim()
        let expected = canonical oldPrompt

        system
        |> Array.map (fun text -> if canonical text = expected then nextPrompt else text)

    let create (journal: AgentJournal option) : obj -> obj -> Task<unit> =
        fun input output ->
            task {
                if not (isNull input) && not (isNull output) && not (isNull input?sessionID) then
                    let sessionText = string input?sessionID

                    if not (String.IsNullOrWhiteSpace sessionText) && not (isNull output?system) then
                        let sid = SessionId.create sessionText
                        let lang = ProviderLanguageBinding.ensureRoot sid
                        let system: string array = unbox output?system

                        if BookkeeperRuntime.isAttached sessionText then
                            let oldPrompt = PromptResources.loadBookkeeperSystemFor ProviderLanguage.English
                            let nextPrompt = PromptResources.loadBookkeeperSystemFor lang
                            output?system <- replaceOwnedSegment oldPrompt nextPrompt system
                        else
                            match roleFor journal sid with
                            | None -> ()
                            | Some role ->
                                let oldPrompt = catalogPrompt (RuntimeResources.current().Prompts) role
                                let nextPrompt = localizedRolePrompt lang role
                                output?system <- replaceOwnedSegment oldPrompt nextPrompt system
            }
