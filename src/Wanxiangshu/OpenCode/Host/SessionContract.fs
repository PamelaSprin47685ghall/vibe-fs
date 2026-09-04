namespace Wanxiangshu.Foundation

/// Process-local side-effect admission handle (HOST-004).
///
/// The handle exposes no authority facts. Its file-private runtime evidence is
/// interpretable only by the issuing SessionQuiescenceGate. Callers may only
/// obtain the handle and pass it back to a gate.
///
/// NEVER written to the journal (HOST-007): a restart has no matching gate
/// registry or fresh attempt, so a crashed process cannot resume sending an
/// idle-derived continuation.
type QuiescencePermit = interface end

namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// Why a quiescence capability could not be consumed or released.
[<RequireQualifiedAccess>]
type QuiescencePermitFailure =
    | WrongOwner
    | NoFreshIdle
    | AlreadyConsumed
    | Superseded
    | Revoked

/// HOST-004: process-local side-effect admission capability.
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
