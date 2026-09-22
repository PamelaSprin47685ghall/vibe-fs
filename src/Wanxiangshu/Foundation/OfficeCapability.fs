namespace Wanxiangshu.Foundation

/// DSL-class: Vocabulary — the fixed tool-permission catalog keyed by Role.
[<RequireQualifiedAccess>]
type ToolPermission =
    | Fork
    | Resume
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
    | Exec
    | Pty
    | ReviewAssessment
    | Chronicle
    /// CASE-009: conditional Casebook read surface for Engineer.
    | Fetch
    /// GLORY-036: the Manager's own end-of-life tool (`suicide`).
    | Finality
    /// Engineer honeypot: visible as `bash-honeypot`, never a real shell.
    | BashHoneypot
    /// Program-owned inquiry through the native sphinx(question) tool.
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
                  ToolPermission.Resume
                  ToolPermission.Join
                  ToolPermission.Horizon
                  ToolPermission.TodoWrite
                  ToolPermission.ReviewAssessment
                  ToolPermission.Finality
                  ToolPermission.Sphinx ]
        | Role.Orchestrator ->
            set
                [ ToolPermission.Fork
                  ToolPermission.Join
                  ToolPermission.Horizon
                  ToolPermission.Sphinx ]
        | Role.Engineer ->
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Move
                  ToolPermission.Remove
                  ToolPermission.BashHoneypot
                  ToolPermission.Fetch
                  ToolPermission.Fission
                  ToolPermission.Sphinx ]
        | Role.Coder -> Set.empty
        | Role.Inspector -> Set.empty
        | Role.Browser -> Set.empty
        | Role.Inquiry -> Set.empty
        | Role.DevOps ->
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Move
                  ToolPermission.Remove
                  ToolPermission.Exec
                  ToolPermission.Pty
                  ToolPermission.Join
                  ToolPermission.Horizon ]
        | Role.Distiller -> Set.empty
        // ENFORCER-010: Blogger's tool set is exactly { chronicle }.
        | Role.Blogger -> set [ ToolPermission.Chronicle ]

    let isAllowed (role: Role) (permission: ToolPermission) : bool =
        if Roles.all |> List.contains role then
            permissions role |> Set.contains permission
        else
            false

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
        elif facts.HasAssessment then
            permissions Role.Manager |> Set.remove ToolPermission.ReviewAssessment
        else
            permissions Role.Manager

    let isAllowedForManagerFacts (facts: ManagerCapabilityFacts) (permission: ToolPermission) : bool =
        permissionsForManagerFacts facts |> Set.contains permission
