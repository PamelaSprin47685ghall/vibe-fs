namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation

module QuiescenceSurface =
    val create: unit -> SessionQuiescenceGate
    val beginAttempt: gate: ISessionQuiescenceGate -> sessionId: string -> unit

    val observePhysicalMessage:
        gate: ISessionQuiescenceGate -> sessionId: string -> physicalUserMessageId: string -> unit

    val observeIdle: gate: ISessionQuiescenceGate -> sessionId: string -> QuiescencePermit
    val tryConsume: gate: ISessionQuiescenceGate -> permit: QuiescencePermit -> obj
    val tryRelease: gate: ISessionQuiescenceGate -> permit: QuiescencePermit -> obj
    val livePermitCount: gate: SessionQuiescenceGate -> int
    val revoke: gate: ISessionQuiescenceGate -> sessionId: string -> unit
    val dropSession: gate: ISessionQuiescenceGate -> sessionId: string -> unit
