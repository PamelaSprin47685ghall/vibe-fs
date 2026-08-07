namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Domain
open Wanxiangshu.Kernel

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

    let roleName = ManagedAgentCatalog.roleLabel
    let tierName = ManagedAgentCatalog.wireTierLabel
    let nameOf = ManagedAgentCatalog.nameOf

    let visibilityOf (role: Role) : AgentVisibility =
        match role with
        | Role.Blogger
        | Role.Executor
        | Role.Teacher -> AgentVisibility.Internal
        | _ -> AgentVisibility.Public

    let make (tier: AgentTier) (role: Role) : ManagedAgent =
        { Name = nameOf tier role
          Role = role
          Tier = tier
          Visibility = visibilityOf role }

    let allPublicRoles = ManagedAgentCatalog.allPublicRoles
    let allInternalRoles = ManagedAgentCatalog.allInternalRoles
    let allRoles = ManagedAgentCatalog.allRoles
    let requiredNames = ManagedAgentCatalog.requiredNames
    let publicForkableNames = ManagedAgentCatalog.publicForkableNames
    let orchestratorForkableNames = ManagedAgentCatalog.orchestratorForkableNames
    let inspectorToolNames = ManagedAgentCatalog.inspectorToolNames
    let coderToolNames = ManagedAgentCatalog.coderToolNames

    /// AGENT-002/003: the ONE parser lives in `Domain.PromptAuthority`. This adds
    /// only the visibility that `ManagedAgent` carries.
    let parse (value: string) : Result<ManagedAgent, ManagedAgentParseError> =
        match PromptAuthority.parseAgentNameTyped value with
        | Ok parsed ->
            Ok
                { Name = parsed.Name
                  Role = parsed.Role
                  Tier = parsed.Tier
                  Visibility = visibilityOf parsed.Role }
        | Error rejection ->
            match rejection with
            | PromptAuthority.AgentNameRejection.LegacyAgentName name ->
                Error(ManagedAgentParseError.LegacyAgentName name)
            | PromptAuthority.AgentNameRejection.UnknownManagedAgent name ->
                Error(ManagedAgentParseError.UnknownManagedAgent name)
            | PromptAuthority.AgentNameRejection.Malformed name -> Error(ManagedAgentParseError.Malformed name)

    let tryParse (value: string) : ManagedAgent option = parse value |> Result.toOption

    let peer (agent: ManagedAgent) : ManagedAgent =
        make (ManagedAgentCatalog.peerTier agent.Tier) agent.Role

    let isPublic (agent: ManagedAgent) =
        agent.Visibility = AgentVisibility.Public

    let isInternal (agent: ManagedAgent) =
        agent.Visibility = AgentVisibility.Internal

    let formatParseError (err: ManagedAgentParseError) : string =
        match err with
        | ManagedAgentParseError.LegacyAgentName name -> ManagedAgentCatalog.formatLegacyNameNotSupported name
        | ManagedAgentParseError.UnknownManagedAgent name ->
            // Best-effort suggestion for near-miss inspector typos.
            let suggestion =
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

            sprintf "Unknown managed agent '%s'.%s" name suggestion
        | ManagedAgentParseError.Malformed name ->
            sprintf "Malformed managed agent name '%s'. Expected fast-ROLE or deep-ROLE." name
