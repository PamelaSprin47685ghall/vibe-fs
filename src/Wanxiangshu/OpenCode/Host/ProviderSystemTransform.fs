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

    [<Literal>]
    let replicaConstraintZh =
        "继续当前任务的只读查证，只使用当前可见且获准的工具。信息足够，或下一步需要写入、执行命令、向用户确认、给出结论或作出关键判断时，直接结束，不为继续调用而增加调查。对话里的 self_note 是先前对未来查证的展望，不是已经证实的结论，也不扩大权限。"

    [<Literal>]
    let replicaConstraintEn =
        "Continue the current task's read-only investigation using only the available, permitted tools. Stop when the evidence is sufficient or the next step requires a change, a command, user clarification, a conclusion, or a consequential judgment. Do not invent work to keep calling tools. A self_note in the conversation is an earlier outlook for investigation, not a verified conclusion or permission to do more."

    let replicaConstraintFor lang =
        match lang with
        | ProviderLanguage.SimplifiedChinese -> replicaConstraintZh
        | ProviderLanguage.English -> replicaConstraintEn

    let private canonical (text: string) = if isNull text then "" else text.Trim()

    let private applyReplicaConstraint (lang: ProviderLanguage) (output: obj) =
        let currentSystem = unbox<string array> output?system
        let expectedZh = canonical replicaConstraintZh
        let expectedEn = canonical replicaConstraintEn
        let nextConstraint = replicaConstraintFor lang

        let hasConstraint =
            currentSystem
            |> Array.exists (fun text ->
                let c = canonical text
                c = expectedZh || c = expectedEn)

        if hasConstraint then
            output?system <-
                currentSystem
                |> Array.map (fun text ->
                    let c = canonical text
                    if c = expectedZh || c = expectedEn then
                        nextConstraint
                    else
                        text)
        else
            output?system <- Array.append currentSystem [| nextConstraint |]

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

    let private activeRoles =
        [ Role.Manager; Role.Orchestrator; Role.Engineer; Role.DevOps; Role.Blogger ]

    let private chooseBookkeeperPrompt expectedEn expectedZh nextPrompt (text: string) =
        let c = canonical text

        if c = expectedEn || c = expectedZh then
            nextPrompt
        else
            text

    /// Wanxiangshu-owned role prompts may arrive as the canonical English or
    /// Chinese Role Law, or as either language's installed-bundle view. All four
    /// are the same semantic prompt, so all four repair to the bound language.
    let private chooseRolePrompt expectedEn expectedZh expectedCur expectedInstalled nextPrompt (text: string) =
        let c = canonical text

        if c = expectedEn || c = expectedZh || c = expectedCur || c = expectedInstalled then
            nextPrompt
        else
            text

    let private roleMatchesSystem (r: Role) (system: string array) : bool =
        let expectedEn =
            canonical (PromptResources.systemForRole ProviderLanguage.English r)

        let expectedZh =
            canonical (PromptResources.systemForRole ProviderLanguage.SimplifiedChinese r)

        system
        |> Array.exists (fun text ->
            let c = canonical text
            c = expectedEn || c = expectedZh)

    let private tryDeduceRole (role: SessionId -> Role option) (sid: SessionId) (system: string array) : Role option =
        match role sid with
        | Some r -> Some r
        | None -> activeRoles |> List.tryFind (fun r -> roleMatchesSystem r system)

    let private replaceBookkeeperSystem lang sessionText output system =
        let oldPromptEn = PromptResources.loadBookkeeperSystemFor ProviderLanguage.English

        let oldPromptZh =
            PromptResources.loadBookkeeperSystemFor ProviderLanguage.SimplifiedChinese

        let expectedEn = canonical oldPromptEn
        let expectedZh = canonical oldPromptZh

        let matchesBookkeeper =
            system
            |> Array.exists (fun text ->
                let c = canonical text
                c = expectedEn || c = expectedZh)

        if BookkeeperRuntime.isAttached sessionText || matchesBookkeeper then
            let nextPrompt = PromptResources.loadBookkeeperSystemFor lang
            output?system <- system |> Array.map (chooseBookkeeperPrompt expectedEn expectedZh nextPrompt)
            true
        else
            false

    let private replaceRoleSystem (role: SessionId -> Role option) sid lang output system =
        match tryDeduceRole role sid system with
        | None -> ()
        | Some r ->
            let oldPromptEn = PromptResources.systemForRole ProviderLanguage.English r
            let oldPromptZh = PromptResources.systemForRole ProviderLanguage.SimplifiedChinese r

            let currentPrompt =
                catalogPrompt (RuntimeResources.current().Prompts) r
                |> Option.defaultValue oldPromptEn

            // provider-language-008: the installed bundle's own view is also a
            // legitimate seed, so a config written under either language still
            // repairs. Byte comparison stays the only recognition rule — the
            // transform never invents or translates prose (provider-language-009).
            let installedPrompt =
                catalogPrompt (RuntimeResources.promptsFor lang) r
                |> Option.defaultValue oldPromptEn

            let nextPrompt = localizedRolePrompt lang r
            let expectedEn = canonical oldPromptEn
            let expectedZh = canonical oldPromptZh
            let expectedCur = canonical currentPrompt
            let expectedInstalled = canonical installedPrompt

            output?system <-
                system
                |> Array.map (chooseRolePrompt expectedEn expectedZh expectedCur expectedInstalled nextPrompt)

    let private transformSystem
        (role: SessionId -> Role option)
        (isReplica: SessionId -> bool)
        sessionText
        output
        system
        =
        let sid = SessionId.create sessionText
        let lang = ProviderLanguageBinding.ensureRoot sid

        if replaceBookkeeperSystem lang sessionText output system then
            ()
        else
            replaceRoleSystem role sid lang output system

        if isReplica sid then
            applyReplicaConstraint lang output

    let createWith (role: SessionId -> Role option) (isReplica: SessionId -> bool) : obj -> obj -> Task<unit> =
        fun input output ->
            task {
                match sessionTransformInput input output with
                | None -> ()
                | Some(sessionText, system) -> transformSystem role isReplica sessionText output system
            }
