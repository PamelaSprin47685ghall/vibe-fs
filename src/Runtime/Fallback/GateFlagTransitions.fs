module Wanxiangshu.Runtime.Fallback.GateFlagTransitions

open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.GateState
open Wanxiangshu.Runtime.Fallback.RuntimeStore

type FallbackRuntimeStore with

    member this.SetNudgeActive (sessionID: string) (value: bool) : unit =
        this.ActiveGates <- setGateActive this.ActiveGates sessionID FallbackSessionGateFlag.NudgeActive value
        this.TriggerStateChanged sessionID

    member this.IsNudgeActive(sessionID: string) : bool =
        isGateActive this.ActiveGates sessionID FallbackSessionGateFlag.NudgeActive

    member this.SetEventHandlingActive (sessionID: string) (value: bool) : unit =
        this.ActiveGates <- setGateActive this.ActiveGates sessionID FallbackSessionGateFlag.EventHandlingActive value
        this.TriggerStateChanged sessionID

    member this.IsEventHandlingActive(sessionID: string) : bool =
        isGateActive this.ActiveGates sessionID FallbackSessionGateFlag.EventHandlingActive

    member this.SetMainContinuationAwaitingStart (sessionID: string) (value: bool) : unit =
        this.ActiveGates <-
            setGateActive this.ActiveGates sessionID FallbackSessionGateFlag.MainContinuationAwaitingStart value

        this.TriggerStateChanged sessionID

    member this.IsMainContinuationAwaitingStart(sessionID: string) : bool =
        isGateActive this.ActiveGates sessionID FallbackSessionGateFlag.MainContinuationAwaitingStart

    member this.GetActiveGates(sessionID: string) : Set<FallbackSessionGateFlag> =
        Map.tryFind sessionID this.ActiveGates |> Option.defaultValue emptyActiveGates
