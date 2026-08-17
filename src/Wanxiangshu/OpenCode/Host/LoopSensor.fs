namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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

    let reportAbortOutcome (sessionId: SessionId) (outcome: Result<unit, string>) =
        match outcome with
        | Ok() -> Diagnostic.emit "loop-kill" [ "session_id", SessionId.value sessionId; "result", "aborted" ]
        | Error reason ->
            Diagnostic.emit
                "loop-kill"
                [ "session_id", SessionId.value sessionId
                  "result", "abort-failed"
                  "provider_error", reason ]

    let abortAndReport (abortSession: SessionId -> Task<Result<unit, string>>) (sessionId: SessionId) : Task =
        task {
            try
                let! outcome = abortSession sessionId
                reportAbortOutcome sessionId outcome
            with ex ->
                Diagnostic.emit
                    "loop-kill"
                    [ "session_id", SessionId.value sessionId
                      "result", "abort-failed"
                      "provider_error", ex.Message ]
        }

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

    member private this.Kill (sessionId: SessionId) (weightedDistinctTokens: float option) (step: int) =
        if this.TryArm sessionId then
            let fields =
                [ "session_id", SessionId.value sessionId
                  "result", "armed"
                  "detector_step", string step ]
                @ (match weightedDistinctTokens with
                   | Some value -> [ "weighted_distinct_token_count", sprintf "%.4f" value ]
                   | None -> [])

            Diagnostic.emit "loop-kill" fields
            abortAndReport abortSession sessionId |> ignore
        else
            Diagnostic.emit "loop-kill" [ "session_id", SessionId.value sessionId; "result", "ignored-duplicate" ]

    member private this.Evaluate(delta: LoopEventCodec.TextDelta) : LoopDetector.Evaluation =
        lock gate (fun () ->
            let detector = this.DetectorFor delta.SessionId
            LoopDetector.pushText detector delta.Delta)

    member private this.KillIfLoop (delta: LoopEventCodec.TextDelta) (evaluation: LoopDetector.Evaluation) =
        if evaluation.IsLoop then
            this.Kill delta.SessionId (Some evaluation.WeightedDistinctTokenCount) evaluation.Step

    member private this.ObserveOwned(delta: LoopEventCodec.TextDelta) =
        match isOwned delta.SessionId, this.IsArmed delta.SessionId with
        | false, _
        | true, true -> ()
        | true, false -> this.KillIfLoop delta (this.Evaluate delta)

    /// Feed one Host raw event. Non-text-delta events are no-ops.
    member this.Observe(raw: obj) =
        match LoopEventCodec.tryDecodeTextDelta raw with
        | None -> ()
        | Some delta -> this.ObserveOwned delta
