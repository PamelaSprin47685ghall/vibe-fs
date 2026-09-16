namespace Wanxiangshu.Ablation

[<RequireQualifiedAccess>]
module AblationFactMap =
    val resetCache: unit -> unit
    val tryNode: factTag: string -> AblationNodeId option
    val allFacts: unit -> (string * AblationNodeId) list
