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

    let private failureName =
        function
        | QuiescencePermitFailure.WrongOwner -> "WrongOwner"
        | QuiescencePermitFailure.NoFreshIdle -> "NoFreshIdle"
        | QuiescencePermitFailure.AlreadyConsumed -> "AlreadyConsumed"
        | QuiescencePermitFailure.Superseded -> "Superseded"
        | QuiescencePermitFailure.Revoked -> "Revoked"

    let private resultView =
        function
        | Ok() ->
            box
                {| accepted = true
                   failure = (null: string) |}
        | Error failure ->
            box
                {| accepted = false
                   failure = failureName failure |}

    let tryConsume (gate: SessionQuiescenceGate) (permit: QuiescencePermit) : obj =
        gate.TryConsume permit |> resultView

    let tryRelease (gate: SessionQuiescenceGate) (permit: QuiescencePermit) : obj =
        gate.TryRelease permit |> resultView

    let livePermitCount (gate: SessionQuiescenceGate) : int = gate.LivePermitCount

    let revoke (gate: SessionQuiescenceGate) (sessionId: string) : unit =
        gate.RevokeCurrentAttempt(SessionId.create sessionId)

    let dropSession (gate: SessionQuiescenceGate) (sessionId: string) : unit =
        gate.DropSession(SessionId.create sessionId)
