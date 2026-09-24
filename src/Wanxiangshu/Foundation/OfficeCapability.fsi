namespace Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type ToolPermission =
    | Fork
    | Resume
    | Join
    | Horizon
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
    val managerReviewReadOnlyPermissions: ToolPermission Set
    val permissions: role: Role -> ToolPermission Set
    val isAllowed: role: Role -> permission: ToolPermission -> bool
    val permissionsForManagerFacts: facts: ManagerCapabilityFacts -> ToolPermission Set
    val isAllowedForManagerFacts: facts: ManagerCapabilityFacts -> permission: ToolPermission -> bool
