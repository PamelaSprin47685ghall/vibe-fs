namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel

/// 0.5.0 Managed Agent identity: fast-ROLE / deep-ROLE.
/// Canonical Role stays unprefixed; Host Agent identity is always tier-prefixed.
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

    /// The wire spelling of a Role.
    ///
    /// Delegates to the one labeller. It used to be a second ten-case match with the
    /// same strings, so the two could disagree while both compiled — and a durable
    /// `CanonicalRole` written through one would then fail to parse through the other.
    let roleName (role: Role) : string =
        Wanxiangshu.Next.Domain.PromptAuthority.roleLabel role

    let tierName (tier: AgentTier) : string =
        match tier with
        | AgentTier.Fast -> "fast"
        | AgentTier.Deep -> "deep"

    let visibilityOf (role: Role) : AgentVisibility =
        match role with
        | Role.Blogger
        | Role.Executor -> AgentVisibility.Internal
        | _ -> AgentVisibility.Public

    let nameOf (tier: AgentTier) (role: Role) : string =
        sprintf "%s-%s" (tierName tier) (roleName role)

    let make (tier: AgentTier) (role: Role) : ManagedAgent =
        { Name = nameOf tier role
          Role = role
          Tier = tier
          Visibility = visibilityOf role }

    let allPublicRoles =
        [ Role.Orchestrator
          Role.Manager
          Role.Coder
          Role.Inspector
          Role.DevOps
          Role.Browser
          Role.Meditator
          Role.Reviewer ]

    let allInternalRoles = [ Role.Blogger; Role.Executor ]

    let allRoles = allPublicRoles @ allInternalRoles

    /// The required 20 Managed Agent names for 0.5.0 config gate.
    let requiredNames: string list =
        allRoles
        |> List.collect (fun role -> [ nameOf AgentTier.Fast role; nameOf AgentTier.Deep role ])

    let publicForkableNames: string list =
        [ Role.Coder
          Role.Inspector
          Role.DevOps
          Role.Browser
          Role.Meditator
          Role.Reviewer ]
        |> List.collect (fun role -> [ nameOf AgentTier.Fast role; nameOf AgentTier.Deep role ])

    let orchestratorForkableNames: string list =
        [ nameOf AgentTier.Fast Role.Manager; nameOf AgentTier.Deep Role.Manager ]

    let inspectorToolNames: string list =
        [ nameOf AgentTier.Fast Role.Inspector; nameOf AgentTier.Deep Role.Inspector ]

    let coderToolNames: string list =
        [ nameOf AgentTier.Fast Role.Coder; nameOf AgentTier.Deep Role.Coder ]

    /// AGENT-002/003: the ONE parser lives in `Domain.PromptAuthority`. This adds
    /// only the visibility that `ManagedAgent` carries.
    ///
    /// It used to be a second implementation: its own legacy-rejection list, its own
    /// role tables, its own tier table, its own peer derivation. Nothing kept the two
    /// in step, so a role added to one would be rejected by the other.
    let parse (value: string) : Result<ManagedAgent, ManagedAgentParseError> =
        match Wanxiangshu.Next.Domain.PromptAuthority.parseAgentNameTyped value with
        | Ok parsed ->
            Ok
                { Name = parsed.Name
                  Role = parsed.Role
                  Tier = parsed.Tier
                  Visibility = visibilityOf parsed.Role }
        | Error rejection ->
            match rejection with
            | Wanxiangshu.Next.Domain.PromptAuthority.AgentNameRejection.LegacyAgentName name ->
                Error(ManagedAgentParseError.LegacyAgentName name)
            | Wanxiangshu.Next.Domain.PromptAuthority.AgentNameRejection.UnknownManagedAgent name ->
                Error(ManagedAgentParseError.UnknownManagedAgent name)
            | Wanxiangshu.Next.Domain.PromptAuthority.AgentNameRejection.Malformed name ->
                Error(ManagedAgentParseError.Malformed name)

    let tryParse (value: string) : ManagedAgent option = parse value |> Result.toOption

    let peer (agent: ManagedAgent) : ManagedAgent =
        let tier =
            match agent.Tier with
            | AgentTier.Fast -> AgentTier.Deep
            | AgentTier.Deep -> AgentTier.Fast

        { agent with
            Name = nameOf tier agent.Role
            Tier = tier }

    let isPublic (agent: ManagedAgent) =
        agent.Visibility = AgentVisibility.Public

    let isInternal (agent: ManagedAgent) =
        agent.Visibility = AgentVisibility.Internal

    let formatParseError (err: ManagedAgentParseError) : string =
        match err with
        | ManagedAgentParseError.LegacyAgentName name ->
            sprintf
                "Legacy agent name '%s' is not supported in Wanxiangshu 0.5.0. Use explicit fast-* or deep-* managed agent names."
                name
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
