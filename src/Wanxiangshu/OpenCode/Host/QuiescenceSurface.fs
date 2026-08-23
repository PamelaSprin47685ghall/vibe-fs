namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HOST-004 quiescence gate: opaque capability surface (P4 pilot).
///
/// `gate` and `permit` are opaque handles: a JS test obtains them, passes them
/// back, and never inspects them (JS-SEMANTIC-SURFACE-005). Session ids cross
/// as plain strings; the gate itself stays a class with private maps.
module QuiescenceSurface =

    let create () : SessionQuiescenceGate = SessionQuiescenceGate()

    let beginAttempt (gate: SessionQuiescenceGate) (sessionId: string) : unit =
        gate.BeginProviderAttempt(SessionId.create sessionId)

    let observePhysicalMessage
        (gate: SessionQuiescenceGate)
        (sessionId: string)
        (physicalUserMessageId: string)
        : unit =
        gate.ObservePhysicalUserMessage(SessionId.create sessionId, PhysicalUserMessageId.create physicalUserMessageId)

    let observeIdle (gate: SessionQuiescenceGate) (sessionId: string) : QuiescencePermit =
        gate.ObserveIdle(SessionId.create sessionId)

    let tryConsume (gate: SessionQuiescenceGate) (permit: QuiescencePermit) : bool = gate.TryConsume permit

    let tryRelease (gate: SessionQuiescenceGate) (permit: QuiescencePermit) : bool = gate.TryRelease permit

    let revoke (gate: SessionQuiescenceGate) (sessionId: string) : unit =
        gate.RevokeCurrentAttempt(SessionId.create sessionId)

    let dropSession (gate: SessionQuiescenceGate) (sessionId: string) : unit =
        gate.DropSession(SessionId.create sessionId)
