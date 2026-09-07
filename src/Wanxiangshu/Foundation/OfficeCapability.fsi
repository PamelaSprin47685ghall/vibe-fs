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
type ManagerCapabilityFacts =
    { HasActiveIncumbency: bool
      HasAssessment: bool
      HasValidBoundCertificate: bool
      CleanupBlockerDigest: string option }

[<RequireQualifiedAccess>]
module OfficeCapability =
    val permissions: role: Role -> ToolPermission Set
    val isAllowed: role: Role -> permission: ToolPermission -> bool
    val permissionsForManagerFacts: facts: ManagerCapabilityFacts -> ToolPermission Set
    val isAllowedForManagerFacts: facts: ManagerCapabilityFacts -> permission: ToolPermission -> bool
