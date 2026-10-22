namespace Wanxiangshu.Execution.Delegation.Advisor

/// Already-resolved carry material and the host-owned delivery cursor cross here.
[<RequireQualifiedAccess>]
module Surface =
    val plan: language: string -> delivered: string array -> material: obj array -> obj
    val delivered: callerSessionId: string -> string array
    val record: callerSessionId: string -> anchors: string array -> unit
    val clear: callerSessionId: string -> unit
    val clearAll: unit -> unit
