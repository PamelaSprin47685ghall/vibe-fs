namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt

/// JS-native execution-gate surface. ToolRegistry remains the owner of the
/// role predicate; callers provide labels and receive only a boolean decision.
module ToolRegistrySurface =

    [<Emit("$0===undefined||$0===null")>]
    let private isUndefined (value: obj) : bool = jsNative

    /// Unknown role and unknown tool both fail closed.
    let rolePredicate (toolName: string) (roleLabel: string) : bool =
        match Roles.tryParseRole roleLabel with
        | None -> false
        | Some role -> ToolRegistry.rolePredicate toolName "surface-session" role

    /// capability-enforcement-006: which authority the execute gate resolves for a tool. `office`
    /// needs the session's established public Role; `private-attachment` is an
    /// internal leaf admitted by owner-held evidence and never holds an office.
    let deliverDescription () : string =
        Wanxiangshu.Mission.Work.OpenCode.DeliverTool.description ()

    let admissionAuthority (toolName: string) : string =
        match ToolRegistry.tryAdmission toolName with
        | Some(ToolAdmission.OfficeRole _) -> "office"
        | Some(ToolAdmission.PrivateAttachment _) -> "private-attachment"
        | None -> "unknown"

    /// capability-enforcement-006: the internal-leaf decision for a session holding no public
    /// office profile. Office tools always answer false here.
    let privateAttachmentAdmits (toolName: string) (sessionId: string) : bool =
        ToolRegistry.privateAttachmentAdmits toolName sessionId

    let private requestKindOf (label: string) : ProviderRequestKind option =
        match label.ToLowerInvariant() with
        | "work-main" -> Some ProviderRequestKind.WorkMain
        | "interaction-repair" -> Some ProviderRequestKind.InteractionRepair
        | _ -> None


    /// Provider tool names projected from the same capability set as the gate.
    let capabilityToolNames (roleLabel: string) (requestKindLabel: string) : string array =
        match Roles.tryParseRole roleLabel, requestKindOf requestKindLabel with
        | Some role, Some requestKind ->
            PromptAuthority.toolCapabilitiesFor role requestKind
            |> Set.toList
            |> List.collect StaticTools.toolNames
            |> List.sort
            |> List.toArray
        | _ -> [||]

    /// The Plan programming surface, executed under one exact call. The
    /// provider run (`messageID`) and the tool call (`callID`) of that call
    /// are the fence the delivery carries; a delivery without both is only a
    /// staged view, never delivery evidence.
    let runJsPlan
        (gitCommonDir: string)
        (root: string)
        (workId: string)
        (program: string)
        (messageId: string)
        (callId: string)
        : System.Threading.Tasks.Task<string> =
        let prose =
            Wanxiangshu.Repository.Programming.Js.OpenCode.JsDescriptionAssets.load (
                ProviderLanguageBinding.readGlobalPreference ()
            )

        let fence =
            if isUndefined messageId || isUndefined callId then
                None
            elif String.IsNullOrWhiteSpace messageId || String.IsNullOrWhiteSpace callId then
                None
            else
                Some(
                    Identity.ProviderRunIdentity.create messageId,
                    Identity.ToolCallId.create callId
                )

        ToolRegistry.runPlanProgram prose gitCommonDir root workId program fence

    let jsPlanDescription (language: string) : string =
        let parsed = ProviderLanguage.parse language

        let prose =
            Wanxiangshu.Repository.Programming.Js.OpenCode.JsDescriptionAssets.load parsed

        Wanxiangshu.Repository.Programming.Js.JsToolGenerator.generate "plan" (OfficeCapability.permissions Role.Plan) prose
        |> Option.map (fun s -> s.Description)
        |> Option.defaultValue ""

    let permissionOfToolName (role: Role) (name: string) : ToolPermission option =
        StaticTools.permissionOfToolName role name
