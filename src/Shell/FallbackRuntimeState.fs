module Wanxiangshu.Shell.FallbackRuntimeState

open Wanxiangshu.Kernel.FallbackKernel.Types

let private freshState: SessionFallbackState =
    { Phase = FallbackPhase.Idle
      CurrentIndex = 0
      FailureCount = 0
      Cancelled = false
      TaskComplete = false
      ContinueCount = 0
      RecoveryCount = 0 }

type FallbackRuntimeState() =
    let mutable states = Map.ofList<string, SessionFallbackState> []
    let mutable chains = Map.ofList<string, FallbackChain> []
    let mutable agents = Map.ofList<string, string> []
    let mutable models = Map.ofList<string, FallbackModel> []
    let mutable busyCounts = Map.ofList<string, int> []
    let mutable consumed = Map.ofList<string, bool> []
    let mutable nudgeActive = Map.ofList<string, bool> []
    let mutable subsessionPending = Map.ofList<string, bool> []
    let mutable eventHandlingActive = Map.ofList<string, bool> []
    let mutable awaitingBusy = Map.ofList<string, bool> []
    let mutable injectedModels = Map.ofList<string, FallbackModel> []
    let mutable injectedAts = Map.ofList<string, int64> []
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

    member _.OnStateChanged (sessionID: string) (callback: unit -> unit) : unit =
        let list =
            match Map.tryFind sessionID listeners with
            | Some arr -> arr
            | None ->
                let arr = ResizeArray<unit -> unit>()
                listeners <- Map.add sessionID arr listeners
                arr

        list.Add(callback)

    member _.HasListeners(sessionID: string) : bool =
        match Map.tryFind sessionID listeners with
        | Some arr -> arr.Count > 0
        | None -> false

    member _.HasState(sessionID: string) : bool = Map.containsKey sessionID states

    member _.TryGetState(sessionID: string) : SessionFallbackState option = Map.tryFind sessionID states

    member _.GetOrCreateState(sessionID: string) : SessionFallbackState =
        match Map.tryFind sessionID states with
        | Some s -> s
        | None ->
            states <- Map.add sessionID freshState states
            freshState

    member _.UpdateState (sessionID: string) (state: SessionFallbackState) : unit =
        states <- Map.add sessionID state states
        triggerStateChanged sessionID

    member _.GetChain(sessionID: string) : FallbackChain =
        Map.tryFind sessionID chains |> Option.defaultValue []

    member _.SetChain (sessionID: string) (chain: FallbackChain) : unit =
        chains <- Map.add sessionID chain chains

    member _.SetAgentName (sessionID: string) (agentName: string) : unit =
        agents <- Map.add sessionID agentName agents

    member _.GetAgentName(sessionID: string) : string =
        Map.tryFind sessionID agents |> Option.defaultValue ""

    member _.SetModel (sessionID: string) (model: FallbackModel) : unit =
        models <- Map.add sessionID model models

    member _.GetModel(sessionID: string) : FallbackModel option = Map.tryFind sessionID models

    member _.GetBusyCount(sessionID: string) : int =
        Map.tryFind sessionID busyCounts |> Option.defaultValue 0

    member _.SetBusyCount (sessionID: string) (n: int) : unit =
        busyCounts <- Map.add sessionID n busyCounts

    member _.SetConsumed (sessionID: string) (value: bool) : unit =
        consumed <- Map.add sessionID value consumed
        triggerStateChanged sessionID

    member _.GetConsumed(sessionID: string) : bool option = Map.tryFind sessionID consumed

    member _.ClearConsumed(sessionID: string) : unit =
        consumed <- Map.remove sessionID consumed
        triggerStateChanged sessionID

    member _.SetNudgeActive (sessionID: string) (value: bool) : unit =
        nudgeActive <- Map.add sessionID value nudgeActive
        triggerStateChanged sessionID

    member _.IsNudgeActive(sessionID: string) : bool =
        Map.tryFind sessionID nudgeActive |> Option.defaultValue false

    member _.SetSubsessionPending (sessionID: string) (value: bool) : unit =
        subsessionPending <- Map.add sessionID value subsessionPending
        triggerStateChanged sessionID

    member _.IsSubsessionPending(sessionID: string) : bool =
        Map.tryFind sessionID subsessionPending |> Option.defaultValue false

    member _.ClearSubsessionPending(sessionID: string) : unit =
        subsessionPending <- Map.remove sessionID subsessionPending
        triggerStateChanged sessionID

    member _.SetEventHandlingActive (sessionID: string) (value: bool) : unit =
        eventHandlingActive <- Map.add sessionID value eventHandlingActive
        triggerStateChanged sessionID

    member _.IsEventHandlingActive(sessionID: string) : bool =
        Map.tryFind sessionID eventHandlingActive |> Option.defaultValue false

    member _.SetAwaitingBusy (sessionID: string) (value: bool) : unit =
        awaitingBusy <- Map.add sessionID value awaitingBusy
        triggerStateChanged sessionID

    member _.IsAwaitingBusy(sessionID: string) : bool =
        Map.tryFind sessionID awaitingBusy |> Option.defaultValue false

    member this.SetContinueActive (sessionID: string) (value: bool) : unit =
        let s = this.GetOrCreateState sessionID

        let nextPhase =
            if value then
                FallbackPhase.Retrying 1
            else
                FallbackPhase.Idle

        this.UpdateState sessionID { s with Phase = nextPhase }

    member this.SetTaskComplete (sessionID: string) (value: bool) : unit =
        let s = this.GetOrCreateState sessionID
        this.UpdateState sessionID { s with TaskComplete = value }

    member _.ClearModel(sessionID: string) : unit = models <- Map.remove sessionID models

    member _.SetInjectedModel (sessionID: string) (model: FallbackModel) : unit =
        injectedModels <- Map.add sessionID model injectedModels

    member _.GetInjectedModel(sessionID: string) : FallbackModel option = Map.tryFind sessionID injectedModels

    member _.SetInjectedAt (sessionID: string) (atMs: int64) : unit =
        injectedAts <- Map.add sessionID atMs injectedAts

    member _.GetInjectedAt(sessionID: string) : int64 option = Map.tryFind sessionID injectedAts

    member _.IsInjectedSince (sessionID: string) (msgTimeMs: int64) : bool =
        match Map.tryFind sessionID injectedAts with
        | Some at -> msgTimeMs >= at
        | None -> false

    member _.ClearInjected(sessionID: string) : unit =
        injectedModels <- Map.remove sessionID injectedModels
        injectedAts <- Map.remove sessionID injectedAts

    member _.CleanupSession(sessionID: string) : unit =
        states <- Map.remove sessionID states
        chains <- Map.remove sessionID chains
        agents <- Map.remove sessionID agents
        models <- Map.remove sessionID models
        busyCounts <- Map.remove sessionID busyCounts
        consumed <- Map.remove sessionID consumed
        nudgeActive <- Map.remove sessionID nudgeActive
        subsessionPending <- Map.remove sessionID subsessionPending
        eventHandlingActive <- Map.remove sessionID eventHandlingActive
        awaitingBusy <- Map.remove sessionID awaitingBusy
        injectedModels <- Map.remove sessionID injectedModels
        injectedAts <- Map.remove sessionID injectedAts
        triggerStateChanged sessionID
