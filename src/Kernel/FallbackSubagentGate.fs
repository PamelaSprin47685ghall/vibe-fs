module Wanxiangshu.Kernel.FallbackSubagentGate

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Kernel.SessionGateDemand
open Wanxiangshu.Kernel.SessionLoop

/// Single observation record for subagent gate derivation (replaces parallel bool maps at the boundary).
type FallbackGateObservation =
    { Lifecycle: FallbackLifecycle option
      Phase: FallbackPhase option
      Consumed: FallbackConsumedStatus option
      BusyCount: int
      ActiveGates: Set<FallbackSessionGateFlag> }

let private gateOn (obs: FallbackGateObservation) (flag: FallbackSessionGateFlag) : bool =
    Set.contains flag obs.ActiveGates

let private phaseNeedsFallbackGate (phase: FallbackPhase) : bool =
    match phase with
    | FallbackPhase.Retrying _
    | FallbackPhase.Scanning _
    | FallbackPhase.ScanningToolCallText
    | FallbackPhase.RecoveringToolCallText -> true
    | FallbackPhase.Exhausted
    | FallbackPhase.Idle -> false

let needFallbackContinue (obs: FallbackGateObservation) : bool =
    match obs.Lifecycle with
    | Some FallbackLifecycle.TaskComplete
    | Some FallbackLifecycle.Cancelled -> false
    | _ ->
        if gateOn obs FallbackSessionGateFlag.EventHandlingActive then
            true
        elif gateOn obs FallbackSessionGateFlag.AwaitingBusy then
            true
        elif gateOn obs FallbackSessionGateFlag.SubsessionPending then
            true
        elif obs.BusyCount > 0 then
            true
        elif gateOn obs FallbackSessionGateFlag.NudgeActive then
            false
        else
            match obs.Phase with
            | Some phase when phaseNeedsFallbackGate phase -> true
            | Some FallbackPhase.Idle ->
                match obs.Consumed with
                | Some FallbackConsumedStatus.ConsumedByHost -> obs.Lifecycle <> Some FallbackLifecycle.TaskComplete
                | _ -> false
            | Some FallbackPhase.Exhausted -> false
            | Some _ -> false
            | None -> false

let gateDemandFromObservation (obs: FallbackGateObservation) : SessionGateDemand =
    let signals =
        [ if needFallbackContinue obs then
              Some GateSignal.FallbackContinue
          else
              None
          if gateOn obs FallbackSessionGateFlag.NudgeActive then
              Some GateSignal.TodoNudge
          else
              None ]
        |> List.choose id

    resolveFromSignals signals

let gateModeFromObservation (obs: FallbackGateObservation) : SessionGateMode =
    gateModeFromDemand (gateDemandFromObservation obs)

let terminalObservation (obs: FallbackGateObservation) : bool =
    match obs.Lifecycle with
    | Some FallbackLifecycle.TaskComplete
    | Some FallbackLifecycle.Cancelled -> true
    | _ ->
        match obs.Phase with
        | Some FallbackPhase.Exhausted -> true
        | _ -> obs.Consumed = Some FallbackConsumedStatus.PropagatedToOuter

let isSubagentSettledFromObservation (sessionID: string) (obs: FallbackGateObservation) : bool =
    if sessionID = "" then
        false
    elif
        obs.Lifecycle = Some FallbackLifecycle.TaskComplete
        || obs.Lifecycle = Some FallbackLifecycle.Cancelled
    then
        true
    elif not (terminalObservation obs) then
        false
    else
        decide (gateModeFromObservation obs) = Resolve
