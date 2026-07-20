module Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.SessionRuntime

// ----- Property transitions -----

let selectChain chain (s: FallbackSessionRuntime) = { s with Chain = chain }

let recordAgentName name (s: FallbackSessionRuntime) = { s with AgentName = name }

let selectModel model (s: FallbackSessionRuntime) = { s with Model = Some model }

let clearModel (s: FallbackSessionRuntime) = { s with Model = None }

let markBusy count (s: FallbackSessionRuntime) = { s with BusyCount = count }

let recordConsumed value (s: FallbackSessionRuntime) = { s with Consumed = Some value }

let clearConsumption (s: FallbackSessionRuntime) = { s with Consumed = None }

let setTerminalConsumed (value: bool) (s: FallbackSessionRuntime) = { s with TerminalConsumed = value }

let isTerminalConsumed (s: FallbackSessionRuntime) : bool = s.TerminalConsumed

let transferOwnership owner (s: FallbackSessionRuntime) =
    let baseRuntime = { s with Owner = owner }

    if owner <> SessionOwner.NoOwner then
        { baseRuntime with
            TerminalConsumed = false }
    else
        baseRuntime

let armNudgeNonce nonce (s: FallbackSessionRuntime) = { s with ActiveNudgeNonce = nonce }

let disarmNudgeNonce (s: FallbackSessionRuntime) = { s with ActiveNudgeNonce = "" }

let tryConsumeNudgeNonce observedNonce (s: FallbackSessionRuntime) =
    if observedNonce = "" then
        s, false
    elif s.ActiveNudgeNonce <> "" && s.ActiveNudgeNonce = observedNonce then
        { s with ActiveNudgeNonce = "" }, true
    else
        s, false

// ----- Human turn transitions -----

let recordLatestHumanModel model (s: FallbackSessionRuntime) =
    { s with LatestHumanModel = Some model }

let clearLatestHumanModel (s: FallbackSessionRuntime) = { s with LatestHumanModel = None }

// ----- Assistant message identity tracking -----

let setLastAssistantMessageId messageId (s: FallbackSessionRuntime) =
    { s with
        Core =
            { s.Core with
                LastAssistantMessageId = messageId } }

let setHumanTurnId turnId (s: FallbackSessionRuntime) = { s with HumanTurnId = turnId }

let advanceHumanTurn (s: FallbackSessionRuntime) =
    let nextOrdinal = s.HumanTurnOrdinal + 1
    let nextId = "turn-" + System.Guid.NewGuid().ToString("N")

    { s with
        HumanTurnId = nextId
        CancelGeneration = s.CancelGeneration + 1
        HumanTurnOrdinal = nextOrdinal },
    nextId

// ----- Ordinal transitions -----

let setSessionGeneration gen (s: FallbackSessionRuntime) = { s with SessionGeneration = gen }

let setCancelGeneration gen (s: FallbackSessionRuntime) = { s with CancelGeneration = gen }

let incrementCancelGeneration (s: FallbackSessionRuntime) =
    let next = s.CancelGeneration + 1
    { s with CancelGeneration = next }, next

let setHumanTurnOrdinal ordinal (s: FallbackSessionRuntime) = { s with HumanTurnOrdinal = ordinal }

let incrementHumanTurnOrdinal (s: FallbackSessionRuntime) =
    let next = s.HumanTurnOrdinal + 1
    { s with HumanTurnOrdinal = next }, next

let setContinuationOrdinal ordinal (s: FallbackSessionRuntime) =
    { s with ContinuationOrdinal = ordinal }

let incrementContinuationOrdinal (s: FallbackSessionRuntime) =
    let next = s.ContinuationOrdinal + 1
    { s with ContinuationOrdinal = next }, next

let setNudgeOrdinal ordinal (s: FallbackSessionRuntime) = { s with NudgeOrdinal = ordinal }

let incrementNudgeOrdinal (s: FallbackSessionRuntime) =
    let next = s.NudgeOrdinal + 1
    { s with NudgeOrdinal = next }, next

let setCompactionOrdinal ordinal (s: FallbackSessionRuntime) = { s with CompactionOrdinal = ordinal }

let incrementCompactionOrdinal (s: FallbackSessionRuntime) =
    let next = s.CompactionOrdinal + 1
    { s with CompactionOrdinal = next }, next

// ----- Gate flag transitions -----

let setNudgeActive value (s: FallbackSessionRuntime) =
    { s with
        ActiveGates =
            if value then
                Set.add FallbackSessionGateFlag.NudgeActive s.ActiveGates
            else
                Set.remove FallbackSessionGateFlag.NudgeActive s.ActiveGates }

let setEventHandlingActive value (s: FallbackSessionRuntime) =
    { s with
        ActiveGates =
            if value then
                Set.add FallbackSessionGateFlag.EventHandlingActive s.ActiveGates
            else
                Set.remove FallbackSessionGateFlag.EventHandlingActive s.ActiveGates }

let setMainContinuationAwaitingStart value (s: FallbackSessionRuntime) =
    { s with
        ActiveGates =
            if value then
                Set.add FallbackSessionGateFlag.MainContinuationAwaitingStart s.ActiveGates
            else
                Set.remove FallbackSessionGateFlag.MainContinuationAwaitingStart s.ActiveGates }

// ----- Gate flag queries -----

let isNudgeActive (s: FallbackSessionRuntime) : bool =
    Set.contains FallbackSessionGateFlag.NudgeActive s.ActiveGates

let isEventHandlingActive (s: FallbackSessionRuntime) : bool =
    Set.contains FallbackSessionGateFlag.EventHandlingActive s.ActiveGates

let isMainContinuationAwaitingStart (s: FallbackSessionRuntime) : bool =
    Set.contains FallbackSessionGateFlag.MainContinuationAwaitingStart s.ActiveGates

let getActiveGates (s: FallbackSessionRuntime) : Set<FallbackSessionGateFlag> = s.ActiveGates

// ----- Model injection transitions -----

let clearInjected (s: FallbackSessionRuntime) =
    { s with
        InjectedModel = None
        InjectedAt = None }

let isInjectedSince sinceMs (s: FallbackSessionRuntime) =
    s.InjectedAt |> Option.exists (fun at -> at >= sinceMs)

let setInjectedAt ts (s: FallbackSessionRuntime) = { s with InjectedAt = Some ts }

let setInjectedModel model (s: FallbackSessionRuntime) = { s with InjectedModel = Some model }

let setInjected model ts (s: FallbackSessionRuntime) =
    { s with
        InjectedModel = Some model
        InjectedAt = Some ts }

let setAbortUnavailable value (s: FallbackSessionRuntime) = { s with AbortUnavailable = value }
