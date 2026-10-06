namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type ProviderAttemptSource =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      ProviderRun: ProviderRunIdentity }

type ContinuationAcceptanceObserver =
    { Notify: PhysicalUserMessageId -> unit
      AttachDisposable: System.IDisposable -> unit }

[<RequireQualifiedAccess>]
type DegenerationKind =
    | TooRepetitive
    | TooRandom

[<RequireQualifiedAccess>]
type AbortCause =
    | DegenerationGuard of DegenerationKind
    | External

type ILoopSensor =
    abstract Observe: raw: obj -> unit

    abstract ConsumeAbortCause:
        source: ProviderAttemptSource * directory: string option * observer: ContinuationAcceptanceObserver option ->
            System.Threading.Tasks.Task<AbortCause>

    abstract DropSession: sessionId: SessionId -> unit
    abstract ResetDetector: sessionId: SessionId -> unit

    abstract ActiveInterruptTask:
        sessionId: SessionId * expectedRun: ProviderRunIdentity -> System.Threading.Tasks.Task option

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
