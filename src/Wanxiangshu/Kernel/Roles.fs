namespace Wanxiangshu.Kernel

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
    /// Manager-only one-line present-context split surface.
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
                  ToolPermission.Fetch ]
        | Role.Inspector ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Exec
                  ToolPermission.Fetch ]
        | Role.Browser ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Network ]
        | Role.Inquiry -> set [ ToolPermission.Inspect; ToolPermission.Sphinx ]
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

    let forRole (role: Role) : RoleDefinition option =
        all |> List.tryFind (fun def -> def.Role = role)
