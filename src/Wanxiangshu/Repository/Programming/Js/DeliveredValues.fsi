namespace Wanxiangshu.Repository.Programming.Js

/// Process-live cell holding the value a program handed over through
/// `js.deliver(value)`. The completion path reads it to build the child's
/// terminal record (replacing the XTrace-materialized LWR for subagents);
/// an ordinary `return` leaves the cell untouched.
module JsDeliveredValues =
    val record: sessionId: string -> value: obj -> unit
    val take: sessionId: string -> obj
    val peek: sessionId: string -> obj
    val render: value: obj -> string option
    val tryTakeRendered: sessionId: string -> string option
    val tryPeekRendered: sessionId: string -> string option
    val clear: sessionId: string -> unit
    val clearAll: unit -> unit
