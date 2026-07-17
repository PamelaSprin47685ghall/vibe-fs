module Wanxiangshu.Runtime.Fallback.GateFlagTransitions

open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore

/// Thin wrappers retained for backward compatibility with host hooks and tests.
/// These now operate on the ActiveGates field of FallbackSessionRuntime via
/// UpdateSession, rather than a separate mutable map in the store.

type FallbackRuntimeStore with

    member this.SetNudgeActive (sessionID: string) (value: bool) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    ActiveGates =
                        if value then
                            Set.add FallbackSessionGateFlag.NudgeActive s.ActiveGates
                        else
                            Set.remove FallbackSessionGateFlag.NudgeActive s.ActiveGates }
        )

        this.TriggerStateChanged sessionID

    member this.IsNudgeActive(sessionID: string) : bool =
        Set.contains FallbackSessionGateFlag.NudgeActive (this.GetSession sessionID).ActiveGates

    member this.SetEventHandlingActive (sessionID: string) (value: bool) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    ActiveGates =
                        if value then
                            Set.add FallbackSessionGateFlag.EventHandlingActive s.ActiveGates
                        else
                            Set.remove FallbackSessionGateFlag.EventHandlingActive s.ActiveGates }
        )

        this.TriggerStateChanged sessionID

    member this.IsEventHandlingActive(sessionID: string) : bool =
        Set.contains FallbackSessionGateFlag.EventHandlingActive (this.GetSession sessionID).ActiveGates

    member this.SetMainContinuationAwaitingStart (sessionID: string) (value: bool) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    ActiveGates =
                        if value then
                            Set.add FallbackSessionGateFlag.MainContinuationAwaitingStart s.ActiveGates
                        else
                            Set.remove FallbackSessionGateFlag.MainContinuationAwaitingStart s.ActiveGates }
        )

        this.TriggerStateChanged sessionID

    member this.IsMainContinuationAwaitingStart(sessionID: string) : bool =
        Set.contains FallbackSessionGateFlag.MainContinuationAwaitingStart (this.GetSession sessionID).ActiveGates

    /// Reads the per-session gate set directly from the record field — no separate store map.
    member this.GetActiveGates(sessionID: string) : Set<FallbackSessionGateFlag> =
        (this.GetSession sessionID).ActiveGates
