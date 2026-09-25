namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module ManagerReviewTools =
    [<Literal>]
    let contractValue = "do-not-use-except-for-review"

    let requiredPermissions (toolName: string) : Set<ToolPermission> option =
        match toolName with
        | "js-manager" -> Some(set [ ToolPermission.Read; ToolPermission.Glob; ToolPermission.Grep ])
        | _ -> None

    let isReviewTool (toolName: string) : bool =
        match toolName with
        | "js-manager" -> true
        | _ -> false
