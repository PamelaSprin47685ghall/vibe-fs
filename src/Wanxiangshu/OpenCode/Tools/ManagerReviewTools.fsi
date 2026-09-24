namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module ManagerReviewTools =
    [<Literal>]
    val contractValue: string = "do-not-use-except-for-review"

    val requiredPermissions: toolName: string -> Set<ToolPermission> option
    val isReviewTool: toolName: string -> bool
