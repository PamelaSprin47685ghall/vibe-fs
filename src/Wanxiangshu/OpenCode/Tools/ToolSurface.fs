namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

/// JS-native static contracts for capability-owned tools. Dynamic Host schemas
/// remain exercised through the real plugin; this surface exposes only the
/// owner-defined identity and catalog facts needed by semantic unit laws.
module ToolSurface =

    let toolSpecNames () : string array =
        StaticTools.knownToolNames |> List.toArray



    let bashHoneypotContract () : obj =
        box
            {| name = BashHoneypotTool.spec.Name
               description = BashHoneypotTool.spec.Description
               argumentNames = [||] |}

    let chronicleContract () : obj =
        box
            {| name = "chronicle"
               argumentNames = [| "entry"; "tip" |]
               tipCount = ChronicleTool.tipFieldNames () |> List.length |}

    let reviewToolNames () : string array =
        StaticTools.knownToolNames
        |> List.filter ManagerReviewTools.isReviewTool
        |> List.toArray

    let isReviewTool (toolName: string) : bool =
        ManagerReviewTools.isReviewTool toolName

    /// A tool name that carries no review contract has no permission mapping.
    let reviewToolPermissions (toolName: string) : string array =
        ManagerReviewTools.requiredPermissions toolName
        |> Option.map (
            Set.toList
            >> List.map OfficeCapability.permissionLabel
            >> List.sort
            >> List.toArray
        )
        |> Option.defaultValue [||]

    /// An unknown role is not a canonical role, so its rule set denies everything.
    let rolePermissionRules (roleLabel: string) : obj =
        match Roles.tryParseRole roleLabel with
        | Some role when Roles.all |> List.contains role -> StaticTools.permissionObj role
        | _ -> box {| ``*`` = "deny" |}
