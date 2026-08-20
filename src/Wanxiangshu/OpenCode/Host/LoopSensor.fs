namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type DegenerationKind =
    | TooRepetitive
    | TooRandom

/// Host boundary outcome for an abort observed by reconciliation. A guarded
/// abort has already transferred recovery ownership to LoopSensor.
[<RequireQualifiedAccess>]
type AbortCause =
    | DegenerationGuard of DegenerationKind
    | External

/// Streaming degeneration owner. It owns the whole process-local protocol:
/// detect -> arm typed anomaly -> interrupt -> consume reconciled abort -> continue.
type LoopSensor
    (
        isOwned: SessionId -> bool,
        abortSession: SessionId -> Task<Result<unit, string>>,
        continueSession: SessionId -> DegenerationKind -> string option -> Task<Result<unit, string>>
    ) =

    let gate = obj ()
    /// DSL-cross-callback-proof: physical resource — bounded detector algorithm scratch per physical attempt
    // DSL-MUTABLE: resource — bounded detector state per physical attempt.
    let detectors = Dictionary<string, LoopDetector.Detector>()
    /// DSL-cross-callback-proof: physical single-flight — process-local anomaly ownership until reconcile
    // DSL-MUTABLE: single-flight — process-local anomaly ownership until reconcile.
    let armed = Dictionary<string, DegenerationKind>()

    let keyOf (sessionId: SessionId) = SessionId.value sessionId

    let kindName kind =
        match kind with
        | DegenerationKind.TooRepetitive -> "too-repetitive"
        | DegenerationKind.TooRandom -> "too-random"

    let reportPhysicalOutcome operation sessionId kind outcome =
        let baseFields =
            [ "session_id", SessionId.value sessionId
              "side", kindName kind
              "operation", operation ]

        match outcome with
        | Ok() -> Diagnostic.emit "degeneration-guard" (baseFields @ [ "result", "ok" ])
        | Error reason ->
            Diagnostic.emit "degeneration-guard" (baseFields @ [ "result", "failed"; "provider_error", reason ])

    let runAndReport operation physicalCall sessionId kind directory : Task =
        task {
            try
                let! outcome = physicalCall sessionId kind directory
                reportPhysicalOutcome operation sessionId kind outcome
            with ex ->
                reportPhysicalOutcome operation sessionId kind (Error ex.Message)
        }

    member private _.TryArm(sessionId: SessionId, kind: DegenerationKind) =
        lock gate (fun () ->
            let key = keyOf sessionId

            if armed.ContainsKey key then
                false
            else
                armed.[key] <- kind
                true)

    member private _.RollbackArm(sessionId: SessionId) =
        lock gate (fun () -> armed.Remove(keyOf sessionId) |> ignore)

    member private this.ApplyInterruptOutcome
        (sessionId: SessionId, kind: DegenerationKind, outcome: Result<unit, string>)
        =
        match outcome with
        | Ok() -> reportPhysicalOutcome "interrupt" sessionId kind outcome
        | Error _ ->
            this.RollbackArm sessionId
            reportPhysicalOutcome "interrupt" sessionId kind outcome

    member private this.RunInterrupt(sessionId: SessionId, kind: DegenerationKind) =
        task {
            try
                let! outcome = abortSession sessionId
                this.ApplyInterruptOutcome(sessionId, kind, outcome)
            with ex ->
                this.RollbackArm sessionId
                reportPhysicalOutcome "interrupt" sessionId kind (Error ex.Message)
        }

    member private this.Interrupt(sessionId: SessionId, kind: DegenerationKind, evaluation: LoopDetector.Evaluation) =
        if this.TryArm(sessionId, kind) then
            Diagnostic.emit
                "degeneration-guard"
                [ "session_id", SessionId.value sessionId
                  "side", kindName kind
                  "operation", "interrupt"
                  "result", "armed"
                  "detector_step", string evaluation.Step
                  "weighted_distinct_token_count", sprintf "%.4f" evaluation.WeightedDistinctTokenCount ]

            this.RunInterrupt(sessionId, kind) |> ignore

    member private this.DetectorFor(sessionId: SessionId) =
        let key = keyOf sessionId

        match detectors.TryGetValue key with
        | true, detector -> detector
        | false, _ ->
            let created = LoopDetector.create ()
            detectors.[key] <- created
            created

    member private this.Evaluate(delta: LoopEventCodec.TextDelta) : LoopDetector.Evaluation =
        lock gate (fun () ->
            let key = keyOf delta.SessionId
            let detector = this.DetectorFor delta.SessionId
            let updated, evaluation = LoopDetector.pushText detector delta.Delta
            detectors.[key] <- updated
            evaluation)

    member private this.InterruptForEvaluation(delta: LoopEventCodec.TextDelta, evaluation: LoopDetector.Evaluation) =
        match evaluation.State with
        | LoopDetector.State.Normal -> ()
        | LoopDetector.State.TooRepetitive ->
            this.Interrupt(delta.SessionId, DegenerationKind.TooRepetitive, evaluation)
        | LoopDetector.State.TooRandom -> this.Interrupt(delta.SessionId, DegenerationKind.TooRandom, evaluation)

    member private this.ObserveEligible(delta: LoopEventCodec.TextDelta) =
        let evaluation = this.Evaluate delta
        this.InterruptForEvaluation(delta, evaluation)

    member private this.ObserveOwned(delta: LoopEventCodec.TextDelta) =
        let alreadyArmed = lock gate (fun () -> armed.ContainsKey(keyOf delta.SessionId))

        if isOwned delta.SessionId && not alreadyArmed then
            this.ObserveEligible delta

    /// Raw stream edge. Non-text/reasoning events fail closed in LoopEventCodec.
    member this.Observe(raw: obj) =
        match LoopEventCodec.tryDecodeTextDelta raw with
        | None -> ()
        | Some delta -> this.ObserveOwned delta

    /// Reconciliation boundary: atomically transfer one armed anomaly into its
    /// exactly-once continuation and return a typed cause so downstream yields.
    member _.ConsumeAbortCause(sessionId: SessionId, directory: string option) : AbortCause =
        let consumed =
            lock gate (fun () ->
                let key = keyOf sessionId

                match armed.TryGetValue key with
                | true, kind ->
                    armed.Remove key |> ignore
                    Some kind
                | false, _ -> None)

        match consumed with
        | None -> AbortCause.External
        | Some kind ->
            runAndReport "continue" continueSession sessionId kind directory |> ignore
            AbortCause.DegenerationGuard kind

    member _.DropSession(sessionId: SessionId) =
        lock gate (fun () ->
            let key = keyOf sessionId
            detectors.Remove key |> ignore
            armed.Remove key |> ignore)

    /// Attempt boundary resets detector scratch but deliberately preserves an
    /// armed anomaly until TurnAborted reconciliation consumes its ownership.
    member _.ResetDetector(sessionId: SessionId) =
        lock gate (fun () -> detectors.[keyOf sessionId] <- LoopDetector.create ())

module LoopSensor =

    let kindName kind =
        match kind with
        | DegenerationKind.TooRepetitive -> "TooRepetitive"
        | DegenerationKind.TooRandom -> "TooRandom"

    let continuationPath kind =
        match kind with
        | DegenerationKind.TooRepetitive -> "runtime/degeneration-too-repetitive"
        | DegenerationKind.TooRandom -> "runtime/degeneration-too-random"

    let private interruptiblePredicate
        (ownedSessions: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        : SessionId -> bool =
        fun sessionId ->
            let key = SessionId.value sessionId
            ownedSessions.Contains key && sessionParents.ContainsKey key

    let create
        (ownedSessions: HashSet<string>)
        (sessionParents: Dictionary<string, string>)
        (abortSession: SessionId -> Task<Result<unit, string>>)
        (continueSession: SessionId -> DegenerationKind -> string option -> Task<Result<unit, string>>)
        =
        LoopSensor(interruptiblePredicate ownedSessions sessionParents, abortSession, continueSession)
