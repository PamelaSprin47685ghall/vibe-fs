module Wanxiangshu.Runtime.Fallback.RuntimeStore

/// Authoritative per-session fallback runtime: one aggregate map plus gate
/// flags and change listeners. All state mutation flows through Update /
/// UpdateSession; listeners fire via TriggerStateChanged.

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Kernel.FallbackRuntimeLifecycle
open Wanxiangshu.Runtime.Fallback.GateState
open Wanxiangshu.Runtime.Fallback.SessionRuntime

type FallbackRuntimeStore() =
    let mutable sessionStates = Map.empty<string, FallbackSessionRuntime>
    let mutable listeners = Map.empty<string, ResizeArray<unit -> unit>>
    let mutable activeGates = Map.empty<string, Set<FallbackSessionGateFlag>>

    let triggerStateChanged (sessionID: string) : unit =
        match Map.tryFind sessionID listeners with
        | Some arr ->
            let copy = arr.ToArray()
            arr.Clear()

            for cb in copy do
                try
                    cb ()
                with _ ->
                    ()
        | None -> ()

    let getSession (sessionID: string) : FallbackSessionRuntime =
        Map.tryFind sessionID sessionStates |> Option.defaultValue freshSessionState

    let updateSession (sessionID: string) (f: FallbackSessionRuntime -> FallbackSessionRuntime) : unit =
        sessionStates <- Map.add sessionID (f (getSession sessionID)) sessionStates

    member _.ActiveGates
        with get () = activeGates
        and set (v) = activeGates <- v

    member _.TriggerStateChanged(sessionID: string) : unit = triggerStateChanged sessionID

    member _.GetSession(sessionID: string) : FallbackSessionRuntime = getSession sessionID

    member _.UpdateSession(sessionID: string, f: FallbackSessionRuntime -> FallbackSessionRuntime) : unit =
        updateSession sessionID f

    /// Single aggregate mutation surface: session map + listener notify.
    member this.Update(sessionID: string, f: FallbackSessionRuntime -> FallbackSessionRuntime) : unit =
        this.UpdateSession(sessionID, f)
        this.TriggerStateChanged sessionID

    member _.OnStateChanged (sessionID: string) (callback: unit -> unit) : unit =
        let list =
            match Map.tryFind sessionID listeners with
            | Some arr -> arr
            | None ->
                let arr = ResizeArray<unit -> unit>()
                listeners <- Map.add sessionID arr listeners
                arr

        list.Add(callback)

    member _.HasState(sessionID: string) : bool = Map.containsKey sessionID sessionStates

    member _.TryGetState(sessionID: string) : SessionFallbackState option =
        match Map.tryFind sessionID sessionStates with
        | Some s -> Some s.Core
        | None -> None

    member _.GetOrCreateState(sessionID: string) : SessionFallbackState =
        if not (Map.containsKey sessionID sessionStates) then
            updateSession sessionID id

        (getSession sessionID).Core

    member _.CleanupSession(sessionID: string) : unit =
        sessionStates <- Map.remove sessionID sessionStates
        activeGates <- removeSessionGates activeGates sessionID
        triggerStateChanged sessionID
