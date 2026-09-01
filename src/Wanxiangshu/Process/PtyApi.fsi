namespace Wanxiangshu.Process

module Pty =
    val bytes: text: string -> byte array
    val newId: unit -> PtyId
    val registerParentAbort: parentId: string -> abort: (unit -> unit) -> int
    val unregisterParentAbort: parentId: string -> token: int -> unit
    val abortParent: parentId: string -> unit
