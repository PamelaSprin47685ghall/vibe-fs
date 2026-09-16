namespace Wanxiangshu.Ablation

[<RequireQualifiedAccess>]
module AblationGate =
    val deniedPath: string
    val toolDenied: toolName: string -> bool
    val filterToolPermissionMap: permissions: Map<string, bool> -> Map<string, bool>
    val filterKnownToolNames: names: string list -> string list
    val deniedFactPath: string
    val factDenied: factTag: string -> bool
