namespace Wanxiangshu.OpenCode

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Provider.Attempt

/// JS-native execution-gate surface. ToolRegistry remains the owner of the
/// role predicate; callers provide labels and receive only a boolean decision.
module ToolRegistrySurface =

    /// Unknown role and unknown tool both fail closed.
    let rolePredicate (toolName: string) (roleLabel: string) : bool =
        match Roles.tryParseRole roleLabel with
        | None -> false
        | Some role -> ToolRegistry.rolePredicate toolName None "surface-session" role

    /// ENF-006: which authority the execute gate resolves for a tool. `office`
    /// needs the session's established public Role; `private-attachment` is an
    /// internal leaf admitted by owner-held evidence and never holds an office.
    let admissionAuthority (toolName: string) : string =
        match ToolRegistry.tryAdmission toolName None with
        | Some(ToolAdmission.OfficeRole _) -> "office"
        | Some(ToolAdmission.PrivateAttachment _) -> "private-attachment"
        | None -> "unknown"

    /// ENF-006: the internal-leaf decision for a session holding no public
    /// office profile. Office tools always answer false here.
    let privateAttachmentAdmits (toolName: string) (sessionId: string) : bool =
        ToolRegistry.privateAttachmentAdmits toolName None sessionId

    let private requestKindOf (label: string) : ProviderRequestKind option =
        match label.ToLowerInvariant() with
        | "work-main" -> Some ProviderRequestKind.WorkMain
        | "blogger-main" -> Some ProviderRequestKind.BloggerMain
        | "blogger-squash" -> Some ProviderRequestKind.BloggerSquash
        | "interaction-repair" -> Some ProviderRequestKind.InteractionRepair
        | "strength-replica" -> Some ProviderRequestKind.StrengthReplica
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
