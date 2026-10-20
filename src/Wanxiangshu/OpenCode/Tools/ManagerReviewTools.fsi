namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module ManagerReviewTools =


    val requiredPermissions: toolName: string -> Set<ToolPermission> option
    val isReviewTool: toolName: string -> bool
