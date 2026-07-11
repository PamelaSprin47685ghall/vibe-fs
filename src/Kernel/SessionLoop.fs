module Wanxiangshu.Kernel.SessionLoop

open Wanxiangshu.Kernel.SessionGateDemand

/// Gate priority: FallbackContinue > TodoNudge > ReviewNudge > Resolve.
/// Resolve is the sole terminal action; drive halts immediately after emitting it.
type SessionGateMode =
    | FallbackContinue
    | TodoNudge
    | ReviewNudge
    | Settled

type GateAction =
    | FallbackContinue
    | TodoNudge
    | ReviewNudge
    | Resolve

let gateModeFromDemand (demand: SessionGateDemand) : SessionGateMode =
    match demand with
    | SessionGateDemand.FallbackContinue -> SessionGateMode.FallbackContinue
    | SessionGateDemand.TodoNudge -> SessionGateMode.TodoNudge
    | SessionGateDemand.ReviewNudge -> SessionGateMode.ReviewNudge
    | SessionGateDemand.Settled -> SessionGateMode.Settled

let decideFromMode (mode: SessionGateMode) : GateAction =
    match mode with
    | SessionGateMode.FallbackContinue -> GateAction.FallbackContinue
    | SessionGateMode.TodoNudge -> GateAction.TodoNudge
    | SessionGateMode.ReviewNudge -> GateAction.ReviewNudge
    | SessionGateMode.Settled -> GateAction.Resolve

let decide (mode: SessionGateMode) : GateAction = decideFromMode mode

let rec drive
    (transition: SessionGateMode -> GateAction -> SessionGateMode)
    (emit: GateAction -> unit)
    (mode: SessionGateMode)
    : unit =
    let action = decide mode
    emit action

    if action <> Resolve then
        let nextMode = transition mode action
        drive transition emit nextMode
