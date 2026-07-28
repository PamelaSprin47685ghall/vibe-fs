namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel

/// 0.5.0 Managed Agent identity: fast-ROLE / deep-ROLE.
/// Canonical Role stays unprefixed; Host Agent identity is always tier-prefixed.
[<RequireQualifiedAccess>]
type AgentTier =
    | Fast
    | Deep

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

    let private publicRoles =
        Map.ofList
            [ "orchestrator", Role.Orchestrator
              "manager", Role.Manager
              "coder", Role.Coder
              "inspector", Role.Inspector
              "devops", Role.DevOps
              "browser", Role.Browser
              "meditator", Role.Meditator
              "reviewer", Role.Reviewer ]

    let private internalRoles =
        Map.ofList [ "blogger", Role.Blogger; "executor", Role.Executor ]

    let private legacyNames =
        set
            [ "orchestrator"
              "manager"
              "build"
              "plan"
              "coder"
              "inspector"
              "devops"
              "browser"
              "meditator"
              "reviewer"
              "blogger"
              "executor"
              "fast"
              "deep" ]

    let roleName (role: Role) : string =
        match role with
        | Role.Orchestrator -> "orchestrator"
        | Role.Manager -> "manager"
        | Role.Coder -> "coder"
        | Role.Inspector -> "inspector"
        | Role.DevOps -> "devops"
        | Role.Browser -> "browser"
        | Role.Meditator -> "meditator"
        | Role.Reviewer -> "reviewer"
        | Role.Blogger -> "blogger"
        | Role.Executor -> "executor"

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

    let tryParse (value: string) : ManagedAgent option =
        if String.IsNullOrWhiteSpace value then
            None
        else
            let trimmed = value.Trim()
            let parts = trimmed.Split([| '-' |], 2)

            if parts.Length <> 2 then
                None
            else
                let tier =
                    match parts.[0] with
                    | "fast" -> Some AgentTier.Fast
                    | "deep" -> Some AgentTier.Deep
                    | _ -> None

                let roleNamePart = parts.[1]

                match tier, publicRoles.TryFind roleNamePart, internalRoles.TryFind roleNamePart with
                | Some tierValue, Some role, _ ->
                    Some
                        { Name = trimmed
                          Role = role
                          Tier = tierValue
                          Visibility = AgentVisibility.Public }
                | Some tierValue, _, Some role ->
                    Some
                        { Name = trimmed
                          Role = role
                          Tier = tierValue
                          Visibility = AgentVisibility.Internal }
                | _ -> None

    let parse (value: string) : Result<ManagedAgent, ManagedAgentParseError> =
        if String.IsNullOrWhiteSpace value then
            Error(ManagedAgentParseError.Malformed value)
        else
            let trimmed = value.Trim()

            match tryParse trimmed with
            | Some agent -> Ok agent
            | None ->
                let lower = trimmed.ToLowerInvariant()

                if
                    legacyNames.Contains lower
                    || lower.Contains '_'
                    || lower.EndsWith("-fast")
                    || lower.EndsWith("-deep")
                    || lower.StartsWith("fast_")
                    || lower.StartsWith("deep_")
                then
                    Error(ManagedAgentParseError.LegacyAgentName trimmed)
                else
                    Error(ManagedAgentParseError.UnknownManagedAgent trimmed)

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
