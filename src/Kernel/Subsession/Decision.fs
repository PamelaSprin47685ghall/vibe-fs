module Wanxiangshu.Kernel.Subsession.Decision

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types

let private activeTurnId (turn: ActiveTurn) : TurnId =
    match turn with
    | NotYetStarted p -> p.TurnId
    | Started s -> s.Plan.TurnId

let private tryExtractActiveForReconcile (s: SubsessionState) : (RunContext * ActiveTurn) option =
    match s with
    | Dispatching(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
    | CancellingDispatch(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
    | ReconcilingUnknownDispatch(ctx, plan, _, _) -> Some(ctx, NotYetStarted plan)
    | ClosingUnknownDispatch(ctx, plan, _) -> Some(ctx, NotYetStarted plan)
    | Running(ctx, started, _) -> Some(ctx, Started started)
    | Draining(ctx, started, _, _) -> Some(ctx, Started started)
    | IssuingAbort(ctx, turn, _, _) -> Some(ctx, turn)
    | AwaitingAbortSettle(ctx, turn, _) -> Some(ctx, turn)
    | ReconcilingAbortSettle(ctx, turn, _) -> Some(ctx, turn)
    | Available _
    | Poisoned _ -> None

let decide (state: SubsessionState) (cmd: Command) : Result<DecisionResult, DecisionError> =
    match cmd with
    | StartRun req -> DecisionStart.decide state req
    | EvidenceUpdated _ -> DecisionObserve.decide state cmd
    | SessionIdleObserved ->
        match state with
        | ReconcilingUnknownDispatch _
        | ClosingUnknownDispatch _ -> Cancellation.decide state cmd
        | _ -> DecisionObserve.decide state cmd
    | TurnErrorObserved _ ->
        match state with
        | ReconcilingUnknownDispatch _
        | ClosingUnknownDispatch _ -> Cancellation.decide state cmd
        | _ -> DecisionObserve.decide state cmd
    | _ -> Cancellation.decide state cmd

/// Given an active subsession state discovered on restart, produce the Decision
/// that must be persisted to NDJSON so the run is durably closed.
/// Returns None only for Available/Poisoned (nothing to reconcile).
let reconcile (state: SubsessionState) : Decision option =
    match state with
    | Available _
    | Poisoned _ -> None
    | _ ->
        match tryExtractActiveForReconcile state with
        | None -> None
        | Some(ctx, turn) ->
            let tid = activeTurnId turn
            let poisoned = Poisoned SessionStateUnknownAfterRestart
            let result = Failed(InfrastructureFailure "session state unknown after restart")

            let events =
                [ SessionPoisoned(ctx.SessionId, SessionStateUnknownAfterRestart)
                  TurnFinished(tid, TurnInfrastructureFailed "session state unknown after restart")
                  RunFinished(ctx.RunId, result) ]

            let effects = [ CompleteCaller(ctx.RunId, result) ]

            Some
                { NextState = poisoned
                  Events = events
                  Effects = effects }
