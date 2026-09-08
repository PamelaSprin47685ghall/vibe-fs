namespace Wanxiangshu.Foundation

type QuiescencePermit = interface end

namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type DegenerationKind =
    | TooRepetitive
    | TooRandom

[<RequireQualifiedAccess>]
type AbortCause =
    | DegenerationGuard of DegenerationKind
    | External

[<RequireQualifiedAccess>]
type QuiescencePermitFailure =
    | WrongOwner
    | NoFreshIdle
    | AlreadyConsumed
    | Superseded
    | Revoked

type ILoopSensor =
    abstract Observe: raw: obj -> unit
    abstract ConsumeAbortCause: sessionId: SessionId * expectedRun: ProviderRunIdentity * directory: string option -> AbortCause
    abstract DropSession: sessionId: SessionId -> unit
    abstract ResetDetector: sessionId: SessionId -> unit
    abstract ActiveInterruptTask: sessionId: SessionId * expectedRun: ProviderRunIdentity -> System.Threading.Tasks.Task option

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
