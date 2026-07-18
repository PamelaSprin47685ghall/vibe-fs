module Wanxiangshu.Runtime.Fallback.RuntimeStore

/// Authoritative per-session fallback runtime: one aggregate map plus change
/// listeners. All state mutation flows through Update / UpdateSession; listeners
/// fire via TriggerStateChanged. Gate flags live on the session record itself
/// (FallbackSessionRuntime.ActiveGates) — there is no separate mutable map.

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Kernel.FallbackRuntimeLifecycle
open Wanxiangshu.Runtime.Fallback.SessionRuntime

type FallbackRuntimeStore() =
    let mutable sessionStates = Map.empty<string, FallbackSessionRuntime>
    let mutable listeners = Map.empty<string, ResizeArray<unit -> unit>>

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

    member _.TriggerStateChanged(sessionID: string) : unit = triggerStateChanged sessionID

    member _.GetSession(sessionID: string) : FallbackSessionRuntime = getSession sessionID

    member _.UpdateSession(sessionID: string, f: FallbackSessionRuntime -> FallbackSessionRuntime) : unit =
        updateSession sessionID f

    /// Single aggregate mutation surface: session map + listener notify.
    member this.Update(sessionID: string, f: FallbackSessionRuntime -> FallbackSessionRuntime) : unit =
        this.UpdateSession(sessionID, f)
        this.TriggerStateChanged sessionID

    /// Update session and return a value produced by the transition, without notifying listeners.
    member _.UpdateSessionReturning(sessionID: string, f: FallbackSessionRuntime -> FallbackSessionRuntime * 'a) : 'a =
        let s = getSession sessionID
        let s', result = f s
        sessionStates <- Map.add sessionID s' sessionStates
        result

    /// Update session and return a value produced by the transition, then notify listeners.
    member this.UpdateReturning(sessionID: string, f: FallbackSessionRuntime -> FallbackSessionRuntime * 'a) : 'a =
        let result = this.UpdateSessionReturning(sessionID, f)
        this.TriggerStateChanged sessionID
        result

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
        listeners <- Map.remove sessionID listeners // release registered callbacks
        triggerStateChanged sessionID
