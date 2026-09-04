namespace Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type ToolPermission =
    | Fork
    | Join
    | Horizon
    | TodoWrite
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
    | Fetch
    | Finality
    | BashHoneypot
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
    val permissions: role: Role -> ToolPermission Set
    val isAllowed: role: Role -> permission: ToolPermission -> bool
    val permissionsForPhase: role: Role -> phase: ManagerCapabilityPhase option -> ToolPermission Set
    val isAllowedForPhase: role: Role -> phase: ManagerCapabilityPhase option -> permission: ToolPermission -> bool
