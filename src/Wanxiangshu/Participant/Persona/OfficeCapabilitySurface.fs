namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

/// JS-native office-capability owner. The manager's forkable office law is
/// projected as labels so callers never decode Role unions or F# collections.
[<RequireQualifiedAccess>]
module OfficeCapabilitySurface =

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
        | "ReviewAssessment" -> Some ToolPermission.ReviewAssessment
        | "Chronicle" -> Some ToolPermission.Chronicle
        | "Fetch" -> Some ToolPermission.Fetch
        | "Finality" -> Some ToolPermission.Finality
        | "BashHoneypot" -> Some ToolPermission.BashHoneypot
        | "Sphinx" -> Some ToolPermission.Sphinx
        | _ -> None

    let private permissionLabel (permission: ToolPermission) : string =
        match permission with
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
        | ToolPermission.ReviewAssessment -> "ReviewAssessment"
        | ToolPermission.Chronicle -> "Chronicle"
        | ToolPermission.Fetch -> "Fetch"
        | ToolPermission.Finality -> "Finality"
        | ToolPermission.BashHoneypot -> "BashHoneypot"
        | ToolPermission.Sphinx -> "Sphinx"

    let private officeName role =
        match role with
        | Role.Coder -> "Coder"
        | Role.Inspector -> "Inspector"
        | Role.DevOps -> "DevOps"
        | Role.Browser -> "Browser"
        | Role.Inquiry -> "Inquiry"
        | _ -> failwith "OfficeCapabilitySurface: catalog contains a non-forkable role"

    /// OFF-002 / ARCH-017: the canonical manager fork office consequence set.
    let managerForkableOffices () : string array =
        ManagedAgentCatalog.managerForkableRoles |> List.map officeName |> List.toArray

    /// Sorted permission labels for a role. Unknown role fails closed.
    let permissions (roleLabel: string) : string array =
        match Roles.tryParseRole roleLabel with
        | None -> [||]
        | Some role ->
            OfficeCapability.permissions role
            |> Set.toList
            |> List.map permissionLabel
            |> List.sort
            |> List.toArray

    /// Unknown role or permission is denied.
    let isAllowed (roleLabel: string) (permissionLabel: string) : bool =
        match Roles.tryParseRole roleLabel, permissionOf permissionLabel with
        | Some role, Some permission -> OfficeCapability.isAllowed role permission
        | _ -> false
