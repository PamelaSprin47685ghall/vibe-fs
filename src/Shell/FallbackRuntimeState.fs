module Wanxiangshu.Shell.FallbackRuntimeState

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Kernel.FallbackRuntimeLifecycle
open Wanxiangshu.Shell.FallbackRuntimeStateGates

let private freshState: SessionFallbackState =
    { Phase = FallbackPhase.Idle
      CurrentIndex = 0
      FailureCount = 0
      Lifecycle = FallbackLifecycle.Active
      ContinueCount = 0
      RecoveryCount = 0 }

type FallbackRuntimeState() =
    let mutable states = Map.ofList<string, SessionFallbackState> []
    let mutable chains = Map.ofList<string, FallbackChain> []
    let mutable agents = Map.ofList<string, string> []
    let mutable models = Map.ofList<string, FallbackModel> []
    let mutable busyCounts = Map.ofList<string, int> []
    let mutable consumed = emptyConsumed
    let mutable activeGates = Map.empty<string, Set<FallbackSessionGateFlag>>
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
        consumed <- setConsumedBool consumed sessionID value
        triggerStateChanged sessionID

    member _.GetConsumed(sessionID: string) : bool option = getConsumedBool consumed sessionID

    member _.ClearConsumed(sessionID: string) : unit =
        consumed <- clearConsumedMap consumed sessionID
        triggerStateChanged sessionID

    member _.SetNudgeActive (sessionID: string) (value: bool) : unit =
        activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.NudgeActive value
        triggerStateChanged sessionID

    member _.IsNudgeActive(sessionID: string) : bool =
        isGateActive activeGates sessionID FallbackSessionGateFlag.NudgeActive

    member _.SetSubsessionPending (sessionID: string) (value: bool) : unit =
        activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.SubsessionPending value
        triggerStateChanged sessionID

    member _.IsSubsessionPending(sessionID: string) : bool =
        isGateActive activeGates sessionID FallbackSessionGateFlag.SubsessionPending

    member _.ClearSubsessionPending(sessionID: string) : unit =
        activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.SubsessionPending false
        triggerStateChanged sessionID

    member _.SetEventHandlingActive (sessionID: string) (value: bool) : unit =
        activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.EventHandlingActive value
        triggerStateChanged sessionID

    member _.IsEventHandlingActive(sessionID: string) : bool =
        isGateActive activeGates sessionID FallbackSessionGateFlag.EventHandlingActive

    member _.SetAwaitingBusy (sessionID: string) (value: bool) : unit =
        activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.AwaitingBusy value
        triggerStateChanged sessionID

    member _.IsAwaitingBusy(sessionID: string) : bool =
        isGateActive activeGates sessionID FallbackSessionGateFlag.AwaitingBusy

    member _.GetActiveGates(sessionID: string) : Set<FallbackSessionGateFlag> =
        Map.tryFind sessionID activeGates |> Option.defaultValue emptyActiveGates

    member this.ApplyContinueMode (sessionID: string) (mode: FallbackContinueMode) : unit =
        let s = this.GetOrCreateState sessionID
        this.UpdateState sessionID { s with Phase = phaseForContinue mode }

    member this.SetContinueActive (sessionID: string) (value: bool) : unit =
        this.ApplyContinueMode sessionID (continueModeFromBool value)

    member this.ApplyTaskCompletion (sessionID: string) (completion: FallbackTaskCompletion) : unit =
        let s = this.GetOrCreateState sessionID

        this.UpdateState
            sessionID
            { s with
                Lifecycle = lifecycleForTask completion }

    member this.SetTaskComplete (sessionID: string) (value: bool) : unit =
        this.ApplyTaskCompletion sessionID (taskCompletionFromBool value)

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
        consumed <- clearConsumedMap consumed sessionID
        activeGates <- removeSessionGates activeGates sessionID
        injectedModels <- Map.remove sessionID injectedModels
        injectedAts <- Map.remove sessionID injectedAts
        triggerStateChanged sessionID
