namespace Wanxiangshu.Participant.Persona

open System
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
        ManagedAgentCatalog.allRoles |> List.map Roles.roleLabel |> List.toArray

    let allPublicRoleLabels: string array =
        ManagedAgentCatalog.allPublicRoles |> List.map Roles.roleLabel |> List.toArray

    let allInternalRoleLabels: string array =
        ManagedAgentCatalog.allInternalRoles |> List.map Roles.roleLabel |> List.toArray

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
        roleOf roleLabel |> Option.map Roles.roleLabel |> Option.defaultValue ""

    let persona (roleLabel: string) (tierLabel: string) : string =
        match tierOf tierLabel, roleOf roleLabel with
        | Some tier, Some role -> PersonaCatalog.persona role tier |> Persona.render
        | _ -> ""

    let bookkeeperPersona (tierLabel: string) : string =
        match tierOf tierLabel with
        | Some tier -> PersonaCatalog.bookkeeperPersona tier |> Persona.render
        | None -> ""

    let formatLegacyNameNotSupported (name: string) : string =
        ManagedAgentCatalog.formatLegacyNameNotSupported name

    let formatLegacyNameInConfig (name: string) : string =
        ManagedAgentCatalog.formatLegacyNameInConfig name

    let private originLabel origin =
        match origin with
        | PersonaOrigin.ResolvedAtRoot -> "ResolvedAtRoot"
        | PersonaOrigin.InheritedFromOwner -> "InheritedFromOwner"

    let private errorLabel error =
        match error with
        | ParticipantIdentityError.BlankParticipantName -> "BlankParticipantName"
        | ParticipantIdentityError.LegacyParticipantName _ -> "LegacyParticipantName"
        | ParticipantIdentityError.MalformedParticipantName _ -> "MalformedParticipantName"
        | ParticipantIdentityError.UnknownParticipantName _ -> "UnknownParticipantName"
        | ParticipantIdentityError.UnsupportedPersonaCatalogVersion _ -> "UnsupportedPersonaCatalogVersion"
        | ParticipantIdentityError.RoleMismatch _ -> "RoleMismatch"
        | ParticipantIdentityError.TierMismatch _ -> "TierMismatch"
        | ParticipantIdentityError.PeerMismatch _ -> "PeerMismatch"
        | ParticipantIdentityError.BlankPersona -> "BlankPersona"
        | ParticipantIdentityError.PersonaMismatch _ -> "PersonaMismatch"
        | ParticipantIdentityError.OriginMismatch _ -> "OriginMismatch"
        | ParticipantIdentityError.OwnerRequired -> "OwnerRequired"
        | ParticipantIdentityError.OwnerPersonaMismatch _ -> "OwnerPersonaMismatch"
        | ParticipantIdentityError.OwnerCatalogVersionMismatch _ -> "OwnerCatalogVersionMismatch"
        | ParticipantIdentityError.LegacyRoleMismatch _ -> "LegacyRoleMismatch"
        | ParticipantIdentityError.LegacyTierMismatch _ -> "LegacyTierMismatch"
        | ParticipantIdentityError.UnsupportedLegacyAuthorityKind _ -> "UnsupportedLegacyAuthorityKind"
        | ParticipantIdentityError.UnprovableLegacyAuthorityIdentity _ -> "UnprovableLegacyAuthorityIdentity"

    let private identityToJs evidence : obj =
        box
            {| name = ParticipantIdentity.selectedAgent evidence
               role = ParticipantIdentity.roleLabel evidence
               initialTier = ParticipantIdentity.initialTier evidence |> Roles.wireTierLabel
               peer = ParticipantIdentity.peerAgent evidence
               persona = ParticipantIdentity.persona evidence
               catalogVersion = ParticipantIdentity.personaCatalogVersion evidence
               origin = ParticipantIdentity.origin evidence |> originLabel |}

    let private identityResult result : obj =
        match result with
        | Ok evidence ->
            box
                {| ok = true
                   identity = identityToJs evidence
                   error = null |}
        | Error error ->
            box
                {| ok = false
                   identity = null
                   error = errorLabel error |}

    let private boundaryFailure error : obj =
        box
            {| ok = false
               identity = null
               error = error |}

    let resolveParticipantIdentityAtRoot (canonicalManagedName: string) : obj =
        ParticipantIdentity.resolveAtRoot canonicalManagedName |> identityResult

    let inheritParticipantIdentityFromOwner (canonicalManagedName: string) (ownerCanonicalManagedName: string) : obj =
        ParticipantIdentity.resolveAtRoot ownerCanonicalManagedName
        |> Result.bind (ParticipantIdentity.inheritFromOwner canonicalManagedName)
        |> identityResult

    let rehydrateParticipantIdentity
        (ownerCanonicalManagedName: string)
        (canonicalManagedName: string)
        (roleLabel: string)
        (initialTierLabel: string)
        (peerCanonicalManagedName: string)
        (personaName: string)
        (catalogVersion: int)
        (origin: string)
        : obj =
        let parsedRole =
            if roleLabel = "bookkeeper" then
                Some None
            else
                roleOf roleLabel |> Option.map Some

        let parsedTier = tierOf initialTierLabel

        let parsedOrigin =
            match origin with
            | "ResolvedAtRoot" -> Some PersonaOrigin.ResolvedAtRoot
            | "InheritedFromOwner" -> Some PersonaOrigin.InheritedFromOwner
            | _ -> None

        match parsedRole, parsedTier, parsedOrigin with
        | None, _, _ -> boundaryFailure "InvalidRoleLabel"
        | _, None, _ -> boundaryFailure "InvalidTierLabel"
        | _, _, None -> boundaryFailure "InvalidOriginLabel"
        | Some role, Some tier, Some personaOrigin ->
            let input =
                { SelectedAgent = canonicalManagedName
                  PeerAgent = peerCanonicalManagedName
                  Role = role
                  InitialTier = tier
                  Persona = personaName
                  PersonaCatalogVersion = catalogVersion
                  Origin = personaOrigin }

            if String.IsNullOrWhiteSpace ownerCanonicalManagedName then
                ParticipantIdentity.rehydrate None input |> identityResult
            else
                ParticipantIdentity.resolveAtRoot ownerCanonicalManagedName
                |> Result.bind (fun owner -> ParticipantIdentity.rehydrate (Some owner) input)
                |> identityResult
