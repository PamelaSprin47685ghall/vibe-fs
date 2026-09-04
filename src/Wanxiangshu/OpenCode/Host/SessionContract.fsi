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

type ISessionQuiescenceGate =
    abstract BeginProviderAttempt: sessionId: SessionId -> unit
    abstract BeginToolExecution: sessionId: SessionId -> unit
    abstract EndToolExecution: sessionId: SessionId -> unit
    abstract ObservePhysicalUserMessage: sessionId: SessionId * physicalUserMessageId: PhysicalUserMessageId -> unit
    abstract ObserveIdle: sessionId: SessionId -> QuiescencePermit
    abstract TryConsume: permit: QuiescencePermit -> Result<unit, QuiescencePermitFailure>
    abstract TryRelease: permit: QuiescencePermit -> Result<unit, QuiescencePermitFailure>
    abstract RevokeCurrentAttempt: sessionId: SessionId -> unit
    abstract DropSession: sessionId: SessionId -> unit
