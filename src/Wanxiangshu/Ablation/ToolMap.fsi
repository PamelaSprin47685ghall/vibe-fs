namespace Wanxiangshu.Ablation

[<RequireQualifiedAccess>]
module AblationToolMap =
    val resetCache: unit -> unit
    val tryNode: toolName: string -> AblationNodeId option
