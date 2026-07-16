module Wanxiangshu.Kernel.Reactive

// ──────────────────────────────────────────────
//  Reactive Edges — Dual-Track
//  Observable Interfaces
//
//  REF.md §4.3 "Reactive Edges":
//
//    IAsyncEnumerable<CommittedProgress>
//      — durable, replayable from NDJSON
//      — safe to use for business decisions
//      — emitted AFTER step 5 (commit)
//      — Law 2: never observable before persist
//
//    IAsyncEnumerable<EphemeralTelemetry>
//      — best-effort, latest-wins
//      — NEVER persisted to NDJSON
//      — MAY be merged/dropped/rate-limited
//      — MUST NOT drive domain state transitions
//      — NOT recoverable after crash
// ──────────────────────────────────────────────

open Wanxiangshu.Kernel.Subsession.Types

/// ── Committed Progress (Track A: Durable) ──
///
/// Fired after a successful commit (steps 5-7). Each progress
/// event corresponds to one committed SubsessionEvent.
///
/// Replayable from NDJSON: replaying the event log produces
/// the same sequence of CommittedProgress values.
///
/// Safe for: projection updates, nudge decisions,
///           UI state, external notifications.
type CommittedProgress =
    /// A Run dispatched at the host (TurnDispatchRequested persisted).
    | ProgressTurnDispatched of runId: RunId * turnId: TurnId * prompt: string
    /// Host accepted the dispatch (TurnStarted persisted).
    | ProgressTurnAccepted of turnId: TurnId * receipt: HostStartReceipt
    /// A turn finished (TurnFinished persisted).
    | ProgressTurnFinished of turnId: TurnId * outcome: TurnFinishOutcome
    /// A run completed (RunFinished persisted).
    | ProgressRunFinished of runId: RunId * result: RunResult
    /// Session poisoned (SessionPoisoned persisted).
    | ProgressSessionPoisoned of sessionId: SessionId * reason: PoisonReason
    /// Abort requested (AbortRequested persisted).
    | ProgressAbortRequested of runId: RunId * turnId: TurnId
    /// Physical session closed.
    | ProgressPhysicalSessionClosed of sessionId: SessionId

module CommittedProgress =
    /// Derive a CommittedProgress list from committed domain events.
    let fromEvents (events: SubsessionEvent list) : CommittedProgress list =
        events
        |> List.choose (function
            | TurnDispatchRequested data -> Some(ProgressTurnDispatched(data.RunId, data.TurnId, data.Prompt))
            | TurnStarted data -> Some(ProgressTurnAccepted(data.TurnId, data.Receipt))
            | TurnFinished(tid, outcome) -> Some(ProgressTurnFinished(tid, outcome))
            | RunFinished(runId, result) -> Some(ProgressRunFinished(runId, result))
            | SessionPoisoned(sid, reason) -> Some(ProgressSessionPoisoned(sid, reason))
            | AbortRequested(runId, turnId) -> Some(ProgressAbortRequested(runId, turnId))
            | PhysicalSessionClosed sid -> Some(ProgressPhysicalSessionClosed sid)
            | RunStarted _ -> None)

/// ── Ephemeral Telemetry (Track B: Best-Effort) ──
///
/// Fired for transient host interactions. NOT persisted to
/// NDJSON. MAY be merged, dropped, or rate-limited.
///
/// MUST NOT be used for business decisions or state
/// transitions. Latest-wins semantics.
///
/// Safe for: progress bars, debug UIs, logging, metrics.
type EphemeralTelemetry =
    /// About to call host.Dispatch.
    | TelemetryHostDispatchStart of turnId: TurnId
    /// host.Dispatch returned Ok(HostStartReceipt).
    | TelemetryHostDispatchOk of turnId: TurnId * receipt: HostStartReceipt
    /// host.Dispatch returned Error(DispatchFailure).
    | TelemetryHostDispatchError of turnId: TurnId * failure: string
    /// About to call host.Abort.
    | TelemetryHostAbortStart of turnId: TurnId
    /// host.Abort returned.
    | TelemetryHostAbortResult of turnId: TurnId * result: string
    /// About to call host.CancelPendingDispatch.
    | TelemetryCancelDispatch of turnId: TurnId
    /// About to call host.QueryDispatchStatus or QuerySessionQuiescence.
    | TelemetryHostQuery of query: string * id: string
    /// State transition (for debug observability).
    | TelemetryStateTransition of fromState: string * toState: string * viaCommand: string
    /// Timer resource acquired/released.
    | TelemetryTimerEvent of resourceId: string * action: string

/// ── Reactive Port ──
///
/// Optional callback interface that CommandProcessor and
/// EffectSupervisor can use to emit progress/telemetry
/// without coupling to any specific consumer.
///
/// Consumers MUST NOT block the commit pipeline.
type IReactivePort =
    /// Called after a successful commit (step 7).
    /// The CommittedProgress list is derived from the events
    /// that were just persisted.
    /// Implementation should be fire-and-forget (never await).
    abstract OnCommitted: progress: CommittedProgress list -> unit

    /// Called before/after host interactions.
    /// Implementation should be fire-and-forget.
    abstract OnTelemetry: telemetry: EphemeralTelemetry list -> unit

/// No-op reactive port — used when no consumer is registered.
/// All calls are no-ops. This is the default.
type NullReactivePort() =
    interface IReactivePort with
        member _.OnCommitted(_) = ()
        member _.OnTelemetry(_) = ()
