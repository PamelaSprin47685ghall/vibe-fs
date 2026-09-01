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
    | Judge
    | Chronicle
    | Fetch
    | Finality
    | BashHoneypot
    | Sphinx

[<RequireQualifiedAccess>]
module OfficeCapability =
    val permissions: role: Role -> ToolPermission Set
    val isAllowed: role: Role -> permission: ToolPermission -> bool
