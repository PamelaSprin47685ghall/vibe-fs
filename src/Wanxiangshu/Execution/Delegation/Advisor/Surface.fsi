namespace Wanxiangshu.Execution.Delegation.Advisor

/// Plain JS messages, the shared declaration and host-owned interval history cross here.
[<RequireQualifiedAccess>]
module Surface =
    val plan: language: string -> delivered: string array -> request: obj -> obj
    val delivered: callerSessionId: string -> string array
    val record: callerSessionId: string -> anchors: string array -> unit
    val clear: callerSessionId: string -> unit
    val clearAll: unit -> unit
