namespace Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type AgentTier =
    | Fast
    | Deep

/// DSL-class: Vocabulary — the fixed set of managed agent roles (one
/// vocabulary, no control-flow reading).
[<RequireQualifiedAccess>]
type Role =
    | Manager
    | Orchestrator
    | Coder
    | Inspector
    | Browser
    | Inquiry
    | Reviewer
    | DevOps
    | Distiller
    | Blogger

/// DSL-class: Vocabulary — the fixed tool-permission catalog keyed by Role.
[<RequireQualifiedAccess>]
type ToolPermission =
    | Fork
    | Join
    | Horizon
    /// Manager-only living-obligation checkpoint surface.
    | TodoWrite
    /// Same-participant multi-present execution consequence (eligible offices only).
    | Fission
    | Read
    | Write
    | Edit
    | Glob
    | Grep
    | Move
    | Remove
    | Inspect
    | Behavior
    | Exec
    | Pty
    | Network
    | Judge
    | Chronicle
    /// CASE-009: conditional Casebook read surface for Coder/Inspector.
    | Fetch
    /// GLORY-036: the Manager's own end-of-life tool (`suicide`).
    | Finality
    /// Coder-only honeypot: visible as `bash-honeypot`, never a real shell.
    | BashHoneypot
    /// AGENT-030: Inquiry-only Sphinx MCP wildcard (`sphinx_*`).
    | Sphinx

module Roles =

    let permissions (role: Role) : ToolPermission Set =
        match role with
        | Role.Manager ->
            set
                [ ToolPermission.Fork
                  ToolPermission.Join
                  ToolPermission.Horizon
                  ToolPermission.TodoWrite
                  ToolPermission.Fission
                  ToolPermission.Finality ]
        | Role.Orchestrator -> set [ ToolPermission.Fork; ToolPermission.Join; ToolPermission.Horizon ]
        | Role.Coder ->
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Move
                  ToolPermission.Remove
                  ToolPermission.BashHoneypot
                  ToolPermission.Inspect
                  ToolPermission.Fetch
                  ToolPermission.Fission ]
        | Role.Inspector ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Exec
                  ToolPermission.Fetch
                  ToolPermission.Fission ]
        | Role.Browser ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Network
                  ToolPermission.Fission ]
        | Role.Inquiry -> set [ ToolPermission.Inspect; ToolPermission.Sphinx; ToolPermission.Fission ]
        | Role.Reviewer ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Judge ]
        | Role.DevOps ->
            set
                [ ToolPermission.Pty
                  ToolPermission.Exec
                  ToolPermission.Join
                  ToolPermission.Horizon
                  ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspect
                  ToolPermission.Behavior ]
        | Role.Distiller -> Set.empty
        // ENFORCER-010: Blogger's tool set is exactly { chronicle }.
        | Role.Blogger -> set [ ToolPermission.Chronicle ]

    let isAllowed (role: Role) (permission: ToolPermission) : bool =
        permissions role |> Set.contains permission

    /// Canonical wire label for a role (lowercase, AGENT-001 vocabulary).
    let roleLabel (role: Role) : string =
        match role with
        | Role.Manager -> "manager"
        | Role.Orchestrator -> "orchestrator"
        | Role.Coder -> "coder"
        | Role.Inspector -> "inspector"
        | Role.DevOps -> "devops"
        | Role.Browser -> "browser"
        | Role.Inquiry -> "inquiry"
        | Role.Reviewer -> "reviewer"
        | Role.Distiller -> "distiller"
        | Role.Blogger -> "blogger"

    let tryParseRole (value: string) : Role option =
        match value.ToLowerInvariant() with
        | "manager" -> Some Role.Manager
        | "orchestrator" -> Some Role.Orchestrator
        | "coder" -> Some Role.Coder
        | "inspector" -> Some Role.Inspector
        | "devops" -> Some Role.DevOps
        | "browser" -> Some Role.Browser
        | "inquiry" -> Some Role.Inquiry
        | "reviewer" -> Some Role.Reviewer
        | "distiller" -> Some Role.Distiller
        | "blogger" -> Some Role.Blogger
        | _ -> None

    /// Wire spelling used in Host agent names (fast / deep).
    let wireTierLabel (tier: AgentTier) : string =
        match tier with
        | AgentTier.Fast -> "fast"
        | AgentTier.Deep -> "deep"

    let tryParseTier (value: string) : AgentTier option =
        match value.ToLowerInvariant() with
        | "fast" -> Some AgentTier.Fast
        | "deep" -> Some AgentTier.Deep
        | _ -> None

    /// AGENT-002 identity formula: `fast-coder`, `deep-distiller`, …
    let managedAgentName (tier: AgentTier) (role: Role) : string =
        sprintf "%s-%s" (wireTierLabel tier) (roleLabel role)

    /// AGENT-008 / ENF-006: Distiller and Blogger are private runtimes, not
    /// public fork / horizon vocabulary.
    let isInternal (role: Role) : bool =
        match role with
        | Role.Blogger
        | Role.Distiller -> true
        | _ -> false

/// Permissions catalog keyed by Role. Role Law / system prompt SSOT =
/// PromptCatalog via PromptResources — not this module. No Companion flag;
/// COMPANION-001 is answered from durable Session kind (HOST-008).
type RoleDefinition =
    { Role: Role
      Tools: ToolPermission Set }

module RoleDefinitions =

    let all: RoleDefinition list =
        [ Role.Manager
          Role.Coder
          Role.Inspector
          Role.DevOps
          Role.Browser
          Role.Inquiry
          Role.Reviewer
          Role.Orchestrator
          Role.Distiller
          Role.Blogger ]
        |> List.map (fun role ->
            { Role = role
              Tools = Roles.permissions role })
