namespace Wanxiangshu.Execution.Delegation.Advisor

/// Process-live successful material deliveries, isolated per caller's advisor.
module Cursor =
    val delivered: callerSessionId: string -> Set<string>
    val record: callerSessionId: string -> anchors: string list -> unit
    val clear: callerSessionId: string -> unit
    val clearAll: unit -> unit
