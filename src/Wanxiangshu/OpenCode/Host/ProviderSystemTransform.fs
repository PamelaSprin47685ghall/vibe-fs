namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Resources

/// HOST-026 / PROMPT-017: project the session-bound ProviderLanguage onto the
/// Wanxiangshu-owned system-prompt segment without disturbing Host/AGENTS text.
module ProviderSystemTransform =

    /// Only active roles own a projected segment; retired identities are
    /// decoded for history but never rewrite a live system prompt.
    let private catalogPrompt (catalog: PromptCatalog) =
        function
        | Role.Manager -> Some catalog.ManagerSystemPrompt
        | Role.Orchestrator -> Some catalog.OrchestratorSystemPrompt
        | Role.Engineer -> Some catalog.EngineerSystemPrompt
        | Role.DevOps -> Some catalog.DevopsSystemPrompt
        | Role.Blogger -> Some catalog.BloggerSystemPrompt
        | Role.Coder
        | Role.Inspector
        | Role.Browser
        | Role.Inquiry
        | Role.Distiller -> None

    let private localizedRolePrompt lang role =
        match role with
        | Role.Blogger ->
            EnforcerCatalogResource.composeBloggerSystemPromptFor
                lang
                (PromptResources.instructionTextsForRole lang role)
                (RuntimeResources.enforcerRulesFor lang)
        | _ -> PromptResources.systemForRole lang role

    let private replaceOwnedSegment (oldPrompt: string) (nextPrompt: string) (system: string array) =
        let canonical (text: string) = if isNull text then "" else text.Trim()
        let expected = canonical oldPrompt

        system
        |> Array.map (fun text -> if canonical text = expected then nextPrompt else text)

    let private sessionTransformInput (input: obj) (output: obj) =
        if
            not (isNull input)
            && not (isNull output)
            && not (isNull input?sessionID)
            && not (String.IsNullOrWhiteSpace(string input?sessionID))
            && not (isNull output?system)
        then
            Some(string input?sessionID, unbox<string array> output?system)
        else
            None

    let private replaceBookkeeperSystem lang sessionText output system =
        if BookkeeperRuntime.isAttached sessionText then
            let oldPrompt = PromptResources.loadBookkeeperSystemFor ProviderLanguage.English
            let nextPrompt = PromptResources.loadBookkeeperSystemFor lang
            output?system <- replaceOwnedSegment oldPrompt nextPrompt system
            true
        else
            false

    let private replaceRoleSystem (role: SessionId -> Role option) sid lang output system =
        let refresh =
            role sid
            |> Option.bind (fun r ->
                catalogPrompt (RuntimeResources.current().Prompts) r
                |> Option.map (fun oldPrompt -> oldPrompt, localizedRolePrompt lang r))

        match refresh with
        | None -> ()
        | Some(oldPrompt, nextPrompt) -> output?system <- replaceOwnedSegment oldPrompt nextPrompt system

    let private transformSystem (role: SessionId -> Role option) sessionText output system =
        let sid = SessionId.create sessionText
        let lang = ProviderLanguageBinding.ensureRoot sid

        if replaceBookkeeperSystem lang sessionText output system then
            ()
        else
            replaceRoleSystem role sid lang output system

    let createWith (role: SessionId -> Role option) : obj -> obj -> Task<unit> =
        fun input output ->
            task {
                match sessionTransformInput input output with
                | None -> ()
                | Some(sessionText, system) -> transformSystem role sessionText output system
            }
