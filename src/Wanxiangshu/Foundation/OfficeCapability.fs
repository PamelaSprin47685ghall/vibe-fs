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
type ManagerCapabilityFacts =
    { HasActiveIncumbency: bool
      HasAssessment: bool
      HasValidBoundCertificate: bool
      CleanupBlockerDigest: string option }

[<RequireQualifiedAccess>]
module OfficeCapability =

    let permissions (role: Role) : ToolPermission Set =
        match role with
        | Role.Manager ->
            set
                [ ToolPermission.Fork
                  ToolPermission.Join
                  ToolPermission.Horizon
                  ToolPermission.TodoWrite
                  ToolPermission.Fission
                  ToolPermission.ReviewAssessment
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

    /// Manager gate over exact RoadView facts. No phase enum crosses this
    /// boundary: retired/no-active grants nothing, a cleanup blocker confines
    /// to the Join+Finality finish window, a valid bound certificate confines
    /// to the same finish window, and all other active facts keep the full
    /// Manager set. A mismatched or stale certificate leaves
    /// HasValidBoundCertificate false, so it never confines to (or grants)
    /// the finish window.
    let permissionsForManagerFacts (facts: ManagerCapabilityFacts) : ToolPermission Set =
        if not facts.HasActiveIncumbency then
            Set.empty
        elif facts.CleanupBlockerDigest.IsSome then
            set [ ToolPermission.Join; ToolPermission.Finality ]
        elif facts.HasValidBoundCertificate then
            set [ ToolPermission.Join; ToolPermission.Finality ]
        else
            permissions Role.Manager

    let isAllowedForManagerFacts (facts: ManagerCapabilityFacts) (permission: ToolPermission) : bool =
        permissionsForManagerFacts facts |> Set.contains permission
