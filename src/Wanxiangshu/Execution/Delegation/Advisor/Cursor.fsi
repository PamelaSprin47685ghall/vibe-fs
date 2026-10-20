namespace Wanxiangshu.Execution.Delegation.Advisor

/// Process-live delivered-anchor cursor for `js.advisor`, keyed by caller
/// session id: what one caller session's advisor has already been shown, so a
/// follow-up consult appends only the delta. A restart resets it.
module Cursor =
    val delivered: callerSessionId: string -> Set<string>

    /// Record the anchors one consult delivered; the cursor only grows.
    val record: callerSessionId: string -> anchors: string list -> unit
    val clear: callerSessionId: string -> unit
    val clearAll: unit -> unit
