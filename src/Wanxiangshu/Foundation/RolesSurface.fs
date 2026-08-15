namespace Wanxiangshu.Foundation

/// JS-native semantic surface for the role/permission vocabulary
/// (P7 wave). Role and ToolPermission cross the boundary as strings — the
/// canonical labels owned by `Roles.roleLabel` (lowercase role names) and the
/// DU case names (capitalised permissions). Every DU value is translated here
/// at the owner boundary; a JS test never touches Fable representation
/// (JS-SEMANTIC-SURFACE-003/005).
module RolesSurface =

    let private roleOf (label: string) : Role option = Roles.tryParseRole label

    let private permissionOf (label: string) : ToolPermission option =
        match label with
        | "Fork" -> Some ToolPermission.Fork
        | "Join" -> Some ToolPermission.Join
        | "Horizon" -> Some ToolPermission.Horizon
        | "TodoWrite" -> Some ToolPermission.TodoWrite
        | "Fission" -> Some ToolPermission.Fission
        | "Read" -> Some ToolPermission.Read
        | "Write" -> Some ToolPermission.Write
        | "Edit" -> Some ToolPermission.Edit
        | "Glob" -> Some ToolPermission.Glob
        | "Grep" -> Some ToolPermission.Grep
        | "Move" -> Some ToolPermission.Move
        | "Remove" -> Some ToolPermission.Remove
        | "Inspect" -> Some ToolPermission.Inspect
        | "Behavior" -> Some ToolPermission.Behavior
        | "Exec" -> Some ToolPermission.Exec
        | "Pty" -> Some ToolPermission.Pty
        | "Network" -> Some ToolPermission.Network
        | "Judge" -> Some ToolPermission.Judge
        | "Chronicle" -> Some ToolPermission.Chronicle
        | "Fetch" -> Some ToolPermission.Fetch
        | "Finality" -> Some ToolPermission.Finality
        | "BashHoneypot" -> Some ToolPermission.BashHoneypot
        | "Sphinx" -> Some ToolPermission.Sphinx
        | _ -> None

    /// The ten canonical role labels, sorted.
    let allRoleLabels: string array =
        [ Role.Manager
          Role.Orchestrator
          Role.Coder
          Role.Inspector
          Role.DevOps
          Role.Browser
          Role.Inquiry
          Role.Reviewer
          Role.Distiller
          Role.Blogger ]
        |> List.map Roles.roleLabel
        |> List.sort
        |> List.toArray

    /// Sorted permission labels for a role. Unknown role → empty array
    /// (fail closed: no permissions).
    let permissions (roleLabel: string) : string array =
        match roleOf roleLabel with
        | None -> [||]
        | Some role ->
            Roles.permissions role
            |> Set.toList
            |> List.map (fun p ->
                match p with
                | ToolPermission.Fork -> "Fork"
                | ToolPermission.Join -> "Join"
                | ToolPermission.Horizon -> "Horizon"
                | ToolPermission.TodoWrite -> "TodoWrite"
                | ToolPermission.Fission -> "Fission"
                | ToolPermission.Read -> "Read"
                | ToolPermission.Write -> "Write"
                | ToolPermission.Edit -> "Edit"
                | ToolPermission.Glob -> "Glob"
                | ToolPermission.Grep -> "Grep"
                | ToolPermission.Move -> "Move"
                | ToolPermission.Remove -> "Remove"
                | ToolPermission.Inspect -> "Inspect"
                | ToolPermission.Behavior -> "Behavior"
                | ToolPermission.Exec -> "Exec"
                | ToolPermission.Pty -> "Pty"
                | ToolPermission.Network -> "Network"
                | ToolPermission.Judge -> "Judge"
                | ToolPermission.Chronicle -> "Chronicle"
                | ToolPermission.Fetch -> "Fetch"
                | ToolPermission.Finality -> "Finality"
                | ToolPermission.BashHoneypot -> "BashHoneypot"
                | ToolPermission.Sphinx -> "Sphinx")
            |> List.sort
            |> List.toArray

    /// Unknown role or permission → false (default deny).
    let isAllowed (roleLabel: string) (permissionLabel: string) : bool =
        match roleOf roleLabel, permissionOf permissionLabel with
        | Some role, Some permission -> Roles.isAllowed role permission
        | _ -> false
