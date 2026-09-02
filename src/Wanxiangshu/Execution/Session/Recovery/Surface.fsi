namespace Wanxiangshu.Execution.Session.Recovery

[<RequireQualifiedAccess>]
module RecoverySurface =
    val token: value: obj -> string
    val validateClosure: root: string -> nodes: obj array -> obj
    val missingMembers: permitMembers: string array -> currentMembers: string array -> string array
    val combine: names: string array -> string
    val handleFamily: branch: string -> obj
    val jobFamily: branch: string -> obj
    val authorize: root: string -> sequence: int -> results: obj array -> obj
    val receiptView: id: string -> sequence: int -> obj
