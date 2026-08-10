namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Session
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// LOOP-002 / LOOP-006: edge sensor over Host streaming deltas.
///
/// Owns per-session detectors and the process-local LoopKillArmed set.
/// Abort is fire-and-forget; the AABB bridge happens later on TurnAborted
/// when the armed mark is still present (OrdinaryTurnWorkflow).
type LoopSensor(isOwned: SessionId -> bool, abortSession: SessionId -> Task<Result<unit, string>>) =

    let gate = obj ()
    let detectors = Dictionary<string, LoopDetector.Detector>()
    let armed = HashSet<string>()

    let keyOf (sessionId: SessionId) = SessionId.value sessionId

    member _.IsArmed(sessionId: SessionId) =
        lock gate (fun () -> armed.Contains(keyOf sessionId))

    /// LOOP-006: claim the armed mark. True exactly once per session until
    /// ClearArmed / DropSession.
    member _.TryArm(sessionId: SessionId) =
        lock gate (fun () -> armed.Add(keyOf sessionId))

    member _.ClearArmed(sessionId: SessionId) =
        lock gate (fun () -> armed.Remove(keyOf sessionId) |> ignore)

    member _.DropSession(sessionId: SessionId) =
        lock gate (fun () ->
            let key = keyOf sessionId
            detectors.Remove key |> ignore
            armed.Remove key |> ignore)

    /// LOOP-005: discard detector state for the next attempt.
    /// Does NOT clear LoopKillArmed — LOOP-006 needs the mark to survive until
    /// TurnAborted is classified (SessionIdle resets the detector before reconcile).
    member _.ResetDetector(sessionId: SessionId) =
        lock gate (fun () -> detectors.[keyOf sessionId] <- LoopDetector.create ())

    member private this.DetectorFor(sessionId: SessionId) =
        let key = keyOf sessionId

        match detectors.TryGetValue key with
        | true, detector -> detector
        | false, _ ->
            let created = LoopDetector.create ()
            detectors.[key] <- created
            created

    member private this.Kill (sessionId: SessionId) (effective: float option) (step: int) =
        if this.TryArm sessionId then
            let fields =
                [ "session_id", SessionId.value sessionId
                  "result", "armed"
                  "detector_step", string step ]
                @ (match effective with
                   | Some value -> [ "effective_character_count", sprintf "%.4f" value ]
                   | None -> [])

            Diagnostic.emit "loop-kill" fields

            task {
                try
                    let! outcome = abortSession sessionId

                    match outcome with
                    | Ok() ->
                        Diagnostic.emit "loop-kill" [ "session_id", SessionId.value sessionId; "result", "aborted" ]
                    | Error reason ->
                        Diagnostic.emit
                            "loop-kill"
                            [ "session_id", SessionId.value sessionId
                              "result", "abort-failed"
                              "provider_error", reason ]
                with ex ->
                    Diagnostic.emit
                        "loop-kill"
                        [ "session_id", SessionId.value sessionId
                          "result", "abort-failed"
                          "provider_error", ex.Message ]
            }
            |> ignore
        else
            Diagnostic.emit "loop-kill" [ "session_id", SessionId.value sessionId; "result", "ignored-duplicate" ]

    /// Feed one Host raw event. Non-text-delta events are no-ops.
    member this.Observe(raw: obj) =
        match LoopEventCodec.tryDecodeTextDelta raw with
        | None -> ()
        | Some delta ->
            if not (isOwned delta.SessionId) then
                ()
            elif this.IsArmed delta.SessionId then
                ()
            else
                let evaluation =
                    lock gate (fun () ->
                        let detector = this.DetectorFor delta.SessionId
                        LoopDetector.pushText detector delta.Delta)

                if evaluation.IsLoop then
                    this.Kill delta.SessionId (Some evaluation.EffectiveCharacterCount) evaluation.Step
