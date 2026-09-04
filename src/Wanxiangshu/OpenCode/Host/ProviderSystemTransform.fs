namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Resources
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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
        | Role.DevOps -> catalog.DevopsSystemPrompt
        | Role.Distiller -> catalog.DistillerSystemPrompt
        | Role.Blogger -> catalog.BloggerSystemPrompt

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

    let private replaceRoleSystem journal sid lang output system =
        match roleFor journal sid with
        | None -> ()
        | Some role ->
            let oldPrompt = catalogPrompt (RuntimeResources.current().Prompts) role
            let nextPrompt = localizedRolePrompt lang role
            output?system <- replaceOwnedSegment oldPrompt nextPrompt system

    let private transformSystem journal sessionText output system =
        let sid = SessionId.create sessionText
        let lang = ProviderLanguageBinding.ensureRoot sid

        if replaceBookkeeperSystem lang sessionText output system then
            ()
        else
            replaceRoleSystem journal sid lang output system

    let create (journal: AgentJournal option) : obj -> obj -> Task<unit> =
        fun input output ->
            task {
                match sessionTransformInput input output with
                | None -> ()
                | Some(sessionText, system) -> transformSystem journal sessionText output system
            }
