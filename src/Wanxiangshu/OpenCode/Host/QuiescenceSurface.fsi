namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module QuiescenceSurface =
    val create: unit -> SessionQuiescenceGate
    val beginAttempt: gate: SessionQuiescenceGate -> sessionId: string -> unit

    val observePhysicalMessage:
        gate: SessionQuiescenceGate -> sessionId: string -> physicalUserMessageId: string -> unit

    val observeIdle: gate: SessionQuiescenceGate -> sessionId: string -> QuiescencePermit
    val tryConsume: gate: SessionQuiescenceGate -> permit: QuiescencePermit -> obj
    val tryRelease: gate: SessionQuiescenceGate -> permit: QuiescencePermit -> obj
    val livePermitCount: gate: SessionQuiescenceGate -> int
    val revoke: gate: SessionQuiescenceGate -> sessionId: string -> unit
    val dropSession: gate: SessionQuiescenceGate -> sessionId: string -> unit
