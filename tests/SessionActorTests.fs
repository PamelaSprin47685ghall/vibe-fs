module Wanxiangshu.Tests.SessionActorTests

open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Session.SessionFact
open Wanxiangshu.Runtime.Session.SessionActor
open Wanxiangshu.Runtime.Session.SessionActorRegistry
open Wanxiangshu.Runtime.Session.SessionActorState
open Wanxiangshu.Runtime.Session.SessionFactDecode
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Fable.Core.JsInterop

let private sleep (ms: int) : JS.Promise<unit> =
    Promise.create (fun resolve _ -> JS.setTimeout (fun () -> resolve ()) ms |> ignore)

/// Concurrent facts on one actor must not interleave handler bodies.
let concurrentFactsSerialize () =
    promise {
        SessionActorRegistry.Clear()
        let actor = SessionActorRegistry.GetOrCreate "ws-serialize" "sess-1"
        let order = ResizeArray<string>()

        actor.ReplaceHandler(fun _ fact ->
            promise {
                match fact with
                | SessionFact.SessionBusyObserved _ ->
                    order.Add "busy-start"
                    do! sleep 30
                    order.Add "busy-end"
                | SessionFact.SessionIdleObserved _ -> order.Add "idle"
                | SessionFact.SessionErrorObserved _ -> order.Add "error"
                | _ -> order.Add "other"
            })

        let p1 = actor.Post(SessionFact.SessionBusyObserved(createObj []))
        let p2 = actor.Post(SessionFact.SessionIdleObserved(createObj []))
        let p3 = actor.Post(SessionFact.SessionErrorObserved(createObj []))
        let! a1 = p1
        let! a2 = p2
        let! a3 = p3

        check "busy accepted" (a1 = FactAdmission.Accept)
        check "idle accepted" (a2 = FactAdmission.Accept)
        check "error accepted" (a3 = FactAdmission.Accept)

        let got = order |> Seq.toList

        equal "serialized order" [ "busy-start"; "busy-end"; "idle"; "error" ] got

        let snap = actor.Snapshot()
        equal "accepted count" 3 snap.AcceptedCount
        equal "dropped count" 0 snap.DroppedCount
        SessionActorRegistry.Clear()
    }

/// Effect result captured before cancel must be dropped after generation bump.
let effectResultAfterCancelDropped () =
    promise {
        SessionActorRegistry.Clear()
        let actor = SessionActorRegistry.GetOrCreate "ws-gate" "sess-2"
        let mutable applied = 0

        actor.ReplaceHandler(fun _ fact ->
            promise {
                match fact with
                | SessionFact.DispatchTransportReturned _ -> applied <- applied + 1
                | SessionFact.AbortReturned _ -> applied <- applied + 1
                | SessionFact.TimeoutElapsed _ -> applied <- applied + 1
                | SessionFact.RecoveryResult _ -> applied <- applied + 1
                | _ -> ()
            })

        do! actor.SetActiveDispatch(Some "dispatch-1")
        do! actor.SetOwner SessionOwner.Fallback

        let identity =
            actor.CaptureEffectIdentity(dispatchId = "dispatch-1", owner = SessionOwner.Fallback)

        // Simulate cancel / human turn superseding the in-flight effect epoch.
        let! _ = actor.BumpGeneration()
        do! actor.SetActiveDispatch None
        do! actor.SetOwner SessionOwner.NoOwner

        let! d1 = actor.Post(SessionFact.DispatchTransportReturned(identity, true, None))

        let! d2 = actor.Post(SessionFact.AbortReturned(identity, true))
        let! d3 = actor.Post(SessionFact.TimeoutElapsed(identity, "prompt"))
        let! d4 = actor.Post(SessionFact.RecoveryResult(identity, false, "stale"))

        check "transport dropped stale gen" (d1 = FactAdmission.DropStaleGeneration)
        check "abort dropped stale gen" (d2 = FactAdmission.DropStaleGeneration)
        check "timeout dropped stale gen" (d3 = FactAdmission.DropStaleGeneration)
        check "recovery dropped stale gen" (d4 = FactAdmission.DropStaleGeneration)
        equal "handler never applied" 0 applied

        // Fresh identity after cancel is admitted.
        do! actor.SetActiveDispatch(Some "dispatch-2")
        do! actor.SetOwner SessionOwner.Nudge

        let fresh =
            actor.CaptureEffectIdentity(dispatchId = "dispatch-2", owner = SessionOwner.Nudge)

        let! ok = actor.Post(SessionFact.DispatchTransportReturned(fresh, true, None))

        check "fresh transport accepted" (ok = FactAdmission.Accept)
        equal "handler applied once" 1 applied
        SessionActorRegistry.Clear()
    }

/// Wrong dispatch id on current generation is dropped.
let wrongDispatchIdDropped () =
    promise {
        SessionActorRegistry.Clear()
        let actor = SessionActorRegistry.GetOrCreate "ws-gate" "sess-2-wrong"
        let mutable applied = 0

        actor.ReplaceHandler(fun _ fact ->
            promise {
                match fact with
                | SessionFact.AbortReturned _ -> applied <- applied + 1
                | _ -> ()
            })

        do! actor.SetActiveDispatch(Some "dispatch-2")
        do! actor.SetOwner SessionOwner.Nudge

        let fresh =
            actor.CaptureEffectIdentity(dispatchId = "dispatch-2", owner = SessionOwner.Nudge)

        let wrongDispatch =
            { fresh with
                ExpectedDispatchId = Some "dispatch-other" }

        let! bad = actor.Post(SessionFact.AbortReturned(wrongDispatch, false))

        check "wrong dispatch dropped" (bad = FactAdmission.DropDispatchMismatch)
        equal "still zero apply" 0 applied
        SessionActorRegistry.Clear()
    }

/// SessionClosed admits once; subsequent facts drop.
let sessionClosedDropsLaterFacts () =
    promise {
        SessionActorRegistry.Clear()
        let actor = SessionActorRegistry.GetOrCreate "ws-close" "sess-3"
        let mutable count = 0

        actor.ReplaceHandler(fun _ _ -> promise { count <- count + 1 })

        let! c1 = actor.Post SessionFact.SessionClosed
        let! c2 = actor.Post(SessionFact.SessionIdleObserved(createObj []))

        check "closed accepted" (c1 = FactAdmission.Accept)
        check "post-close dropped" (c2 = FactAdmission.DropClosed)
        check "actor closed" actor.IsClosed
        equal "handler once" 1 count
        SessionActorRegistry.Clear()
    }

/// Decode path maps host envelopes into standard facts part 1.
let decodeHostEnvelopeFacts_part1 () =
    let busyInput =
        createObj
            [ "event"
              ==> createObj
                      [ "type" ==> "session.status"
                        "properties"
                        ==> createObj [ "sessionID" ==> "s-decode"; "status" ==> createObj [ "type" ==> "busy" ] ] ] ]

    match tryFromHostInput busyInput with
    | Some("s-decode", SessionFact.SessionBusyObserved _) -> check "busy decode" true
    | other -> check ("busy decode unexpected: " + string other) false

/// Decode path maps host envelopes into standard facts part 2.
let decodeHostEnvelopeFacts_part2 () =
    let idleInput =
        createObj
            [ "event"
              ==> createObj
                      [ "type" ==> "session.idle"
                        "properties" ==> createObj [ "sessionID" ==> "s-decode" ] ] ]

    match tryFromHostInput idleInput with
    | Some("s-decode", SessionFact.TerminalObserved _) -> check "idle decode" true
    | other -> check ("idle decode unexpected: " + string other) false

    let closedInput =
        createObj
            [ "event"
              ==> createObj
                      [ "type" ==> "session.deleted"
                        "properties" ==> createObj [ "info" ==> createObj [ "id" ==> "s-decode" ] ] ] ]

    match tryFromHostInput closedInput with
    | Some("s-decode", SessionFact.SessionClosed) -> check "closed decode" true
    | other -> check ("closed decode unexpected: " + string other) false

let pureAdmissionRules () =
    let baseSnap = SessionActorSnapshot.empty

    let openFact = SessionFact.SessionIdleObserved(createObj [])
    check "open accepts idle" (FactAdmission.decide baseSnap openFact = FactAdmission.Accept)

    let closedSnap = SessionActorTransition.markClosed baseSnap
    check "closed drops idle" (FactAdmission.decide closedSnap openFact = FactAdmission.DropClosed)

    let identity: EffectIdentity =
        { ExpectedGeneration = 0
          ExpectedDispatchId = Some "d1"
          ExpectedOwner = Some SessionOwner.Fallback }

    let effect = SessionFact.DispatchTransportReturned(identity, true, None)
    let ownerSnap = SessionActorTransition.setOwner SessionOwner.Nudge baseSnap

    check "owner mismatch" (FactAdmission.decide ownerSnap effect = FactAdmission.DropOwnerMismatch)

    let dispatchSnap =
        baseSnap
        |> SessionActorTransition.setOwner SessionOwner.Fallback
        |> SessionActorTransition.setActiveDispatch (Some "d1")

    check "matching effect accepts" (FactAdmission.decide dispatchSnap effect = FactAdmission.Accept)

let run () =
    promise {
        do! concurrentFactsSerialize ()
        do! effectResultAfterCancelDropped ()
        do! wrongDispatchIdDropped ()
        do! sessionClosedDropsLaterFacts ()
        decodeHostEnvelopeFacts_part1 ()
        decodeHostEnvelopeFacts_part2 ()
        pureAdmissionRules ()
    }
