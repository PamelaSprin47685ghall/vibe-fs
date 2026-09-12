namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

[<RequireQualifiedAccess>]
module AttentionToolSurface =
    val create: toolModule: obj -> snapshot: (unit -> obj) -> append: (string -> string -> obj -> Task<bool>) -> obj

    val withoutJournal: toolModule: obj -> obj
    val execute: tools: obj -> name: string -> arguments: obj -> context: obj -> Task<string>
