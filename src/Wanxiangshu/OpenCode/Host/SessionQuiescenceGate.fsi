namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type SessionQuiescenceGate =
    new: unit -> SessionQuiescenceGate
    interface ISessionQuiescenceGate
    member BeginProviderAttempt: sessionId: SessionId -> unit
    member ObservePhysicalUserMessage: sessionId: SessionId * physicalUserMessageId: PhysicalUserMessageId -> unit
    member ObserveIdle: sessionId: SessionId -> QuiescencePermit
    member CaptureCurrentAttempt: sessionId: SessionId -> QuiescencePermit
    member ObserveIdleFor: observation: QuiescencePermit -> QuiescencePermit option
    member TryConsume: permit: QuiescencePermit -> Result<unit, QuiescencePermitFailure>
    member TryRelease: permit: QuiescencePermit -> Result<unit, QuiescencePermitFailure>
    member RevokeCurrentAttempt: sessionId: SessionId -> unit
    member internal LivePermitCount: int
    member DropSession: sessionId: SessionId -> unit
