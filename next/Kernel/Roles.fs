namespace Wanxiangshu.Next.Kernel

[<RequireQualifiedAccess>]
type Role =
    | Manager
    | Orchestrator
    | Coder
    | Inspector
    | Browser
    | Meditator
    | Reviewer
    | Executor
    | Blogger

[<RequireQualifiedAccess>]
type ToolPermission =
    | Fork
    | Join
    | List
    | Read
    | Write
    | Edit
    | Glob
    | Grep
    | Inspector
    | Exec
    | Network
    | Verdict

module Roles =

    let permissions (role: Role) : ToolPermission Set =
        match role with
        | Role.Manager -> set [ ToolPermission.Fork; ToolPermission.Join; ToolPermission.List ]
        | Role.Orchestrator -> set [ ToolPermission.Fork; ToolPermission.Join ]
        | Role.Coder ->
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Inspector ]
        | Role.Inspector -> set [ ToolPermission.Exec ]
        | Role.Browser -> set [ ToolPermission.Read; ToolPermission.Network ]
        | Role.Meditator ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector ]
        | Role.Reviewer ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector
                  ToolPermission.Verdict ]
        | Role.Executor
        | Role.Blogger -> Set.empty

    let isAllowed (role: Role) (permission: ToolPermission) : bool =
        permissions role |> Set.contains permission
