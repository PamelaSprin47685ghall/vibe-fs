namespace Wanxiangshu.Foundation

type QuiescencePermit = interface end

namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type QuiescencePermitFailure =
    | WrongOwner
    | NoFreshIdle
    | AlreadyConsumed
    | Superseded
    | Revoked

type SessionQuiescenceGate =
    new: unit -> SessionQuiescenceGate
    member BeginProviderAttempt: sessionId: SessionId -> unit
    member ObservePhysicalUserMessage: sessionId: SessionId * physicalUserMessageId: PhysicalUserMessageId -> unit
    member ObserveIdle: sessionId: SessionId -> QuiescencePermit
    member TryConsume: permit: QuiescencePermit -> Result<unit, QuiescencePermitFailure>
    member TryRelease: permit: QuiescencePermit -> Result<unit, QuiescencePermitFailure>
    member RevokeCurrentAttempt: sessionId: SessionId -> unit
    member internal LivePermitCount: int
    member DropSession: sessionId: SessionId -> unit
