namespace Wanxiangshu.Foundation

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
    | ReviewAssessment
    | Chronicle
    /// CASE-009: conditional Casebook read surface for Coder/Inspector.
    | Fetch
    /// GLORY-036: the Manager's own end-of-life tool (`suicide`).
    | Finality
    /// Coder-only honeypot: visible as `bash-honeypot`, never a real shell.
    | BashHoneypot
    /// AGENT-030: Inquiry-only Sphinx MCP wildcard (`sphinx_*`).
    | Sphinx

[<RequireQualifiedAccess>]
type ManagerCapabilityPhase =
    | AuditPending
    | WorkOwned
    | PerfectAwaitingRetirement
    | RetirementCleanupBlocked
    | Retired

[<RequireQualifiedAccess>]
module OfficeCapability =

    let permissions (role: Role) : ToolPermission Set =
        match role with
        | Role.Manager ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.ReviewAssessment
                  ToolPermission.Fork
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

    let private managerPermissions phase =
        match phase with
        | ManagerCapabilityPhase.AuditPending ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Join
                  ToolPermission.ReviewAssessment
                  ToolPermission.Finality ]
        | ManagerCapabilityPhase.WorkOwned ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Fork
                  ToolPermission.Join
                  ToolPermission.Horizon
                  ToolPermission.TodoWrite
                  ToolPermission.Fission
                  ToolPermission.Finality ]
        | ManagerCapabilityPhase.PerfectAwaitingRetirement ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Join
                  ToolPermission.Finality ]
        | ManagerCapabilityPhase.RetirementCleanupBlocked ->
            set [ ToolPermission.Read; ToolPermission.Join; ToolPermission.Finality ]
        | ManagerCapabilityPhase.Retired -> Set.empty

    let permissionsForPhase role phase =
        match role, phase with
        | Role.Manager, Some managerPhase -> managerPermissions managerPhase
        | Role.Manager, None -> managerPermissions ManagerCapabilityPhase.AuditPending
        | _ -> permissions role

    let isAllowedForPhase role phase permission =
        permissionsForPhase role phase |> Set.contains permission
