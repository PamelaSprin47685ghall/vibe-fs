namespace Wanxiangshu.Next.Kernel

[<RequireQualifiedAccess>]
type Role =
    | Manager
    | Coder
    | Inspector
    | Browser
    | Meditator
    | Reviewer

[<RequireQualifiedAccess>]
type ToolPermission =
    | Fork
    | Join
    | List
    | Pty
    | Inspector
    | Blogger
    | Exec
    | Read
    | Network
    | Glob
    | Grep

module Roles =

    let permissions (role: Role) : ToolPermission Set =
        match role with
        | Role.Manager ->
            set
                [ ToolPermission.Fork
                  ToolPermission.Join
                  ToolPermission.List
                  ToolPermission.Pty ]
        | Role.Coder -> set [ ToolPermission.Inspector; ToolPermission.Blogger ]
        | Role.Inspector -> set [ ToolPermission.Exec ]
        | Role.Browser -> set [ ToolPermission.Read; ToolPermission.Network ]
        | Role.Meditator
        | Role.Reviewer ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector ]

    let isAllowed (role: Role) (permission: ToolPermission) : bool =
        permissions role |> Set.contains permission
