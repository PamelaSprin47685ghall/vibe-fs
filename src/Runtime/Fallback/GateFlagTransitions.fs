module Wanxiangshu.Runtime.Fallback.GateFlagTransitions

open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

type FallbackRuntimeStore with
    member this.SetNudgeActive (sessionID: string) (value: bool) : unit =
        this.Update(sessionID, setNudgeActive value)

    member this.IsNudgeActive(sessionID: string) : bool =
        Set.contains FallbackSessionGateFlag.NudgeActive (this.GetSession sessionID).ActiveGates

    member this.SetEventHandlingActive (sessionID: string) (value: bool) : unit =
        this.Update(sessionID, setEventHandlingActive value)

    member this.IsEventHandlingActive(sessionID: string) : bool =
        Set.contains FallbackSessionGateFlag.EventHandlingActive (this.GetSession sessionID).ActiveGates

    member this.SetMainContinuationAwaitingStart (sessionID: string) (value: bool) : unit =
        this.Update(sessionID, setMainContinuationAwaitingStart value)

    member this.IsMainContinuationAwaitingStart(sessionID: string) : bool =
        Set.contains FallbackSessionGateFlag.MainContinuationAwaitingStart (this.GetSession sessionID).ActiveGates

    member this.GetActiveGates(sessionID: string) : Set<FallbackSessionGateFlag> =
        (this.GetSession sessionID).ActiveGates
