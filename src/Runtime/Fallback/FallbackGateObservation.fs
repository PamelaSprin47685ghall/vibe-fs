module Wanxiangshu.Runtime.Fallback.FallbackGateObservation

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Kernel.FallbackSubagentGate
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions

let observe (runtime: FallbackRuntimeStore) (sessionID: string) : FallbackGateObservation =
    let phase, lifecycle =
        match runtime.TryGetState sessionID with
        | Some st -> Some st.Phase, Some st.Lifecycle
        | None -> None, None

    let consumed =
        match runtime.GetConsumed sessionID with
        | Some true -> Some FallbackConsumedStatus.ConsumedByHost
        | Some false -> Some FallbackConsumedStatus.PropagatedToOuter
        | None -> None

    let owner = runtime.GetSessionOwner sessionID

    let terminalOrigin =
        let isNudgeOwner = owner = SessionOwner.Nudge
        let isFallbackOwner = owner = SessionOwner.Fallback
        let isCompactionOwner = owner = SessionOwner.Compaction

        if isCompactionOwner then
            Some(TerminalOrigin.CompactionSummaryCompleted)
        elif isNudgeOwner then
            Some(TerminalOrigin.NudgeCompleted)
        elif isFallbackOwner then
            Some(TerminalOrigin.FallbackContinuationCompleted)
        else
            None

    { Lifecycle = lifecycle
      Phase = phase
      Consumed = consumed
      BusyCount = runtime.GetBusyCount sessionID
      ActiveGates = getActiveGates (runtime.GetSession sessionID)
      TerminalOrigin = terminalOrigin }
