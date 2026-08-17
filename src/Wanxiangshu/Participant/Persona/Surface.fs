namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

/// JS-native identity and persona contract owned by Participant/Persona.
///
/// Roles, tiers, managed names, legacy rejection, and persona labels cross as
/// plain strings/booleans/arrays. F# Role and AgentTier values never cross the
/// boundary, so callers cannot depend on their emitted representation.
module PersonaSurface =

    let private roleOf (label: string) : Role option =
        if isNull label then None else Roles.tryParseRole label

    let private tierOf (label: string) : AgentTier option =
        if isNull label then None else Roles.tryParseTier label

    let allRoleLabels: string array =
        ManagedAgentCatalog.allRoles
        |> List.map ManagedAgentCatalog.roleLabel
        |> List.toArray

    let allPublicRoleLabels: string array =
        ManagedAgentCatalog.allPublicRoles
        |> List.map ManagedAgentCatalog.roleLabel
        |> List.toArray

    let allInternalRoleLabels: string array =
        ManagedAgentCatalog.allInternalRoles
        |> List.map ManagedAgentCatalog.roleLabel
        |> List.toArray

    let requiredNames: string array = ManagedAgentCatalog.requiredNames |> List.toArray

    let legacyNames: string array = ManagedAgentCatalog.legacyAgentNames |> Set.toArray

    let nameOf (tierLabel: string) (roleLabel: string) : string =
        match tierOf tierLabel, roleOf roleLabel with
        | Some tier, Some role -> ManagedAgentCatalog.nameOf tier role
        | _ -> ""

    let peerTierLabel (tierLabel: string) : string =
        match tierOf tierLabel with
        | Some AgentTier.Fast -> "Deep"
        | Some AgentTier.Deep -> "Fast"
        | None -> ""

    let peerName (name: string) : string =
        if isNull name then
            ""
        elif ManagedAgentCatalog.isBookkeeperName name then
            ManagedAgentCatalog.bookkeeperPeerName name |> Option.defaultValue ""
        else
            let separator = name.IndexOf('-')

            if separator <= 0 || separator = name.Length - 1 then
                ""
            else
                nameOf (peerTierLabel (name.Substring(0, separator))) (name.Substring(separator + 1))

    let isManagedName (name: string) : bool =
        if isNull name then
            false
        elif ManagedAgentCatalog.isBookkeeperName name then
            true
        else
            let separator = name.IndexOf('-')

            separator > 0
            && separator < name.Length - 1
            && nameOf (name.Substring(0, separator)) (name.Substring(separator + 1)) <> ""

    let isLegacyName (name: string) : bool =
        not (isNull name)
        && ManagedAgentCatalog.isLegacyAgentName (name.ToLowerInvariant())

    let roleName (roleLabel: string) : string =
        roleOf roleLabel
        |> Option.map ManagedAgentCatalog.roleLabel
        |> Option.defaultValue ""

    let persona (roleLabel: string) (tierLabel: string) : string =
        match tierOf tierLabel, roleOf roleLabel with
        | Some tier, Some role -> PersonaCatalog.persona role tier
        | _ -> ""

    let bookkeeperPersona (tierLabel: string) : string =
        match tierOf tierLabel with
        | Some tier -> PersonaCatalog.bookkeeperPersona tier
        | None -> ""

    let formatLegacyNameNotSupported (name: string) : string =
        ManagedAgentCatalog.formatLegacyNameNotSupported name

    let formatLegacyNameInConfig (name: string) : string =
        ManagedAgentCatalog.formatLegacyNameInConfig name
