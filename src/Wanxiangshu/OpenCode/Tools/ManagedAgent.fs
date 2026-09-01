namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Persona

/// Managed Agent identity: fast-ROLE / deep-ROLE.
/// Canonical Role stays unprefixed; Host Agent identity is always tier-prefixed.
/// Identity tables live in `ManagedAgentCatalog` (AGENT-001…004).
[<RequireQualifiedAccess>]
type AgentVisibility =
    | Public
    | Internal

type ManagedAgent =
    { Name: string
      Role: Role
      Tier: AgentTier
      Visibility: AgentVisibility }

[<RequireQualifiedAccess>]
type ManagedAgentParseError =
    | UnknownManagedAgent of string
    | LegacyAgentName of string
    | Malformed of string

module ManagedAgent =

    let roleName = Roles.roleLabel
    let tierName = Roles.wireTierLabel
    let nameOf = ManagedAgentCatalog.nameOf

    let visibilityOf (role: Role) : AgentVisibility =
        if Roles.isInternal role then
            AgentVisibility.Internal
        else
            AgentVisibility.Public

    let make (tier: AgentTier) (role: Role) : ManagedAgent =
        { Name = nameOf tier role
          Role = role
          Tier = tier
          Visibility = visibilityOf role }

    let allPublicRoles = ManagedAgentCatalog.allPublicRoles
    let allInternalRoles = ManagedAgentCatalog.allInternalRoles
    let allRoles = ManagedAgentCatalog.allRoles
    let requiredNames = ManagedAgentCatalog.requiredNames
    let managerForkableNames = ManagedAgentCatalog.managerForkableNames
    let orchestratorForkableNames = ManagedAgentCatalog.orchestratorForkableNames
    let inspectorToolNames = ManagedAgentCatalog.inspectorToolNames
    let coderToolNames = ManagedAgentCatalog.coderToolNames

    let private mapIdentityError value error =
        match error with
        | ParticipantIdentityError.LegacyParticipantName name -> ManagedAgentParseError.LegacyAgentName name
        | ParticipantIdentityError.UnknownParticipantName name -> ManagedAgentParseError.UnknownManagedAgent name
        | ParticipantIdentityError.BlankParticipantName -> ManagedAgentParseError.Malformed value
        | ParticipantIdentityError.MalformedParticipantName name -> ManagedAgentParseError.Malformed name
        | ParticipantIdentityError.UnsupportedPersonaCatalogVersion _
        | ParticipantIdentityError.RoleMismatch _
        | ParticipantIdentityError.TierMismatch _
        | ParticipantIdentityError.PeerMismatch _
        | ParticipantIdentityError.BlankPersona
        | ParticipantIdentityError.PersonaMismatch _
        | ParticipantIdentityError.OriginMismatch _
        | ParticipantIdentityError.OwnerRequired
        | ParticipantIdentityError.OwnerPersonaMismatch _
        | ParticipantIdentityError.OwnerCatalogVersionMismatch _
        | ParticipantIdentityError.LegacyRoleMismatch _
        | ParticipantIdentityError.LegacyTierMismatch _
        | ParticipantIdentityError.UnsupportedLegacyAuthorityKind _
        | ParticipantIdentityError.UnprovableLegacyAuthorityIdentity _ -> ManagedAgentParseError.Malformed value

    let private classifyPublicRole value =
        function
        | Some role -> Ok role
        | None -> Error(ManagedAgentParseError.UnknownManagedAgent value)

    let private admitPublicRole value identity =
        ParticipantIdentity.role identity
        |> classifyPublicRole value
        |> Result.map (fun role ->
            { Name = ParticipantIdentity.selectedAgent identity
              Role = role
              Tier = ParticipantIdentity.initialTier identity
              Visibility = visibilityOf role })

    let parse (value: string) : Result<ManagedAgent, ManagedAgentParseError> =
        ParticipantIdentity.resolveAtRoot value
        |> Result.mapError (mapIdentityError value)
        |> Result.bind (admitPublicRole value)

    let tryParse (value: string) : ManagedAgent option = parse value |> Result.toOption

    let peer (agent: ManagedAgent) : ManagedAgent =
        make (ManagedAgentCatalog.peerTier agent.Tier) agent.Role

    let isInternal (agent: ManagedAgent) =
        agent.Visibility = AgentVisibility.Internal

    let private unknownAgentSuggestion (name: string) =
        if name.IndexOf("inspect", StringComparison.OrdinalIgnoreCase) >= 0 then
            " Use 'fast-inspector' or 'deep-inspector'."
        elif name.IndexOf("review", StringComparison.OrdinalIgnoreCase) >= 0 then
            " Use 'fast-reviewer' or 'deep-reviewer'."
        elif name.IndexOf("manager", StringComparison.OrdinalIgnoreCase) >= 0 then
            " Use 'fast-manager' or 'deep-manager'."
        elif name.IndexOf("coder", StringComparison.OrdinalIgnoreCase) >= 0 then
            " Use 'fast-coder' or 'deep-coder'."
        else
            " Use an explicit fast-* or deep-* managed agent name."

    let formatParseError (err: ManagedAgentParseError) : string =
        match err with
        | ManagedAgentParseError.LegacyAgentName name -> ManagedAgentCatalog.formatLegacyNameNotSupported name
        | ManagedAgentParseError.UnknownManagedAgent name ->
            sprintf "Unknown managed agent '%s'.%s" name (unknownAgentSuggestion name)
        | ManagedAgentParseError.Malformed name ->
            sprintf "Malformed managed agent name '%s'. Expected fast-ROLE or deep-ROLE." name
