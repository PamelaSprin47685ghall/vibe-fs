module Wanxiangshu.Shell.FallbackRuntimeState

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Kernel.FallbackRuntimeLifecycle
open Wanxiangshu.Shell.FallbackRuntimeStateGates

type PendingLease =
    { ContinuationID: string
      ContinuationOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      CancelGeneration: int
      Owner: string
      Model: FallbackModel
      PromptText: string option
      Status: string }

type NudgeLease =
    { NudgeID: string
      NudgeOrdinal: int
      Nonce: string
      HumanTurnID: string
      SessionGeneration: int
      CancelGeneration: int
      Owner: string
      Status: string }

[<RequireQualifiedAccess>]
type SubsessionRunStatus =
    | Requested
    | Running
    | Continuing
    | Settled
    | Failed
    | Cancelled

[<RequireQualifiedAccess>]
type SubsessionMatchResult =
    | NoMatch
    | StrongMatch
    | BoundaryMatch

[<RequireQualifiedAccess>]
type AttemptObservation =
    | AwaitingStart
    | RunningObserved
    | AssistantObserved of string

type SubsessionRunLease =
    { RunId: string
      ChildId: string
      ParentSessionId: string
      mutable ActiveAttemptOrdinal: int
      mutable Status: SubsessionRunStatus
      mutable ActiveContinuationId: string
      mutable ActiveContinuationOrdinal: int
      mutable DispatchMessageBoundary: string option
      mutable ActiveObservation: AttemptObservation
      mutable InjectedUserMessageId: string option }

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
    let mutable latestHumanModels = Map.empty<string, string>
    let mutable humanTurnIds = Map.empty<string, string>
    let mutable sessionGenerations = Map.ofList<string, int> []
    let mutable cancelGenerations = Map.ofList<string, int> []
    let mutable activeContinuationGens = Map.ofList<string, int> []
    let mutable activeContinuationCancelGens = Map.ofList<string, int> []
    let mutable sessionOwners = Map.ofList<string, string> []
    let mutable pendingLeases = Map.empty<string, obj>
    let mutable pendingNudgeLeases = Map.empty<string, NudgeLease>
    let mutable activeCompactionIds = Map.empty<string, string>
    let mutable activeCompactionOrdinals = Map.empty<string, int>
    let mutable forceStoppedSessions = Set.empty<string>
    let mutable compactedSessions = Set.empty<string>
    let mutable compactionContinuationObserved = Set.empty<string>
    let mutable compactionGenerations = Map.empty<string, int>
    let mutable activeNudgeNonces = Map.empty<string, string>
    let mutable humanTurnOrdinals = Map.empty<string, int>
    let mutable continuationOrdinals = Map.empty<string, int>
    let mutable nudgeOrdinals = Map.empty<string, int>
    let mutable compactionOrdinals = Map.empty<string, int>
    let mutable lastHumanMessageIds = Map.empty<string, string>
    let mutable subsessionRuns = Map.empty<string * string, SubsessionRunLease>
    let mutable activeRunByChild = Map.empty<string, string>
    let mutable activeObservations = Map.empty<string, AttemptObservation>

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

    member this.UpdateState (sessionID: string) (state: SessionFallbackState) : unit =
        let finalState =
            if state.Lifecycle = FallbackLifecycle.Cancelled then
                activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.AwaitingBusy false
                activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.SubsessionPending false
                activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.EventHandlingActive false
                activeGates <- setGateActive activeGates sessionID FallbackSessionGateFlag.NudgeActive false
                busyCounts <- Map.add sessionID 0 busyCounts
                consumed <- clearConsumedMap consumed sessionID
                injectedModels <- Map.remove sessionID injectedModels
                injectedAts <- Map.remove sessionID injectedAts
                activeObservations <- Map.remove sessionID activeObservations

                { state with
                    Phase = FallbackPhase.Idle
                    ContinueCount = 0
                    FailureCount = 0 }
            else
                state

        states <- Map.add sessionID finalState states

        let activeLeaseOpt =
            match Map.tryFind sessionID activeRunByChild with
            | Some activeRunId -> Map.tryFind (sessionID, activeRunId) subsessionRuns
            | None -> None

        match activeLeaseOpt with
        | Some lease ->
            if finalState.Lifecycle = FallbackLifecycle.TaskComplete then
                if
                    this.TryGetPendingLease(sessionID).IsNone
                    && not (this.IsAwaitingBusy(sessionID))
                then
                    lease.Status <- SubsessionRunStatus.Settled
            elif finalState.Lifecycle = FallbackLifecycle.Cancelled then
                lease.Status <- SubsessionRunStatus.Cancelled
            elif finalState.Phase = FallbackPhase.Exhausted then
                lease.Status <- SubsessionRunStatus.Failed
            elif
                match finalState.Phase with
                | FallbackPhase.Retrying _ -> true
                | _ -> false
            then
                lease.Status <- SubsessionRunStatus.Continuing
            elif
                lease.Status = SubsessionRunStatus.Requested
                || lease.Status = SubsessionRunStatus.Continuing
            then
                lease.Status <- SubsessionRunStatus.Running
        | None -> ()

        triggerStateChanged sessionID

    member _.SetLatestHumanModel (sessionID: string) (model: string) : unit =
        latestHumanModels <- Map.add sessionID model latestHumanModels

    member _.GetLatestHumanModel(sessionID: string) : string option = Map.tryFind sessionID latestHumanModels

    member _.ClearLatestHumanModel(sessionID: string) : unit =
        latestHumanModels <- Map.remove sessionID latestHumanModels

    member _.GetHumanTurnId(sessionID: string) : string =
        Map.tryFind sessionID humanTurnIds |> Option.defaultValue ""

    member _.SetHumanTurnId (sessionID: string) (turnId: string) : unit =
        humanTurnIds <- Map.add sessionID turnId humanTurnIds

    member _.SetSessionGeneration (sessionID: string) (gen: int) : unit =
        sessionGenerations <- Map.add sessionID gen sessionGenerations

    member _.SetCancelGeneration (sessionID: string) (gen: int) : unit =
        cancelGenerations <- Map.add sessionID gen cancelGenerations

    member _.IncrementHumanTurnId(sessionID: string) : string =
        let nextId = "turn-" + System.Guid.NewGuid().ToString("N")
        humanTurnIds <- Map.add sessionID nextId humanTurnIds
        let currentGen = Map.tryFind sessionID sessionGenerations |> Option.defaultValue 0
        sessionGenerations <- Map.add sessionID (currentGen + 1) sessionGenerations

        humanTurnOrdinals <-
            Map.add
                sessionID
                (Map.tryFind sessionID humanTurnOrdinals |> Option.defaultValue 0 |> (+) 1)
                humanTurnOrdinals

        nextId

    member _.GetSessionGeneration(sessionID: string) : int =
        Map.tryFind sessionID sessionGenerations |> Option.defaultValue 0

    member _.GetHumanTurnOrdinal(sessionID: string) : int =
        Map.tryFind sessionID humanTurnOrdinals |> Option.defaultValue 0

    member _.SetHumanTurnOrdinal (sessionID: string) (ordinal: int) : unit =
        humanTurnOrdinals <- Map.add sessionID ordinal humanTurnOrdinals

    member _.IncrementHumanTurnOrdinal(sessionID: string) : int =
        let current = Map.tryFind sessionID humanTurnOrdinals |> Option.defaultValue 0
        let next = current + 1
        humanTurnOrdinals <- Map.add sessionID next humanTurnOrdinals
        next

    member _.GetContinuationOrdinal(sessionID: string) : int =
        Map.tryFind sessionID continuationOrdinals |> Option.defaultValue 0

    member _.SetContinuationOrdinal (sessionID: string) (ordinal: int) : unit =
        continuationOrdinals <- Map.add sessionID ordinal continuationOrdinals

    member _.IncrementContinuationOrdinal(sessionID: string) : int =
        let current = Map.tryFind sessionID continuationOrdinals |> Option.defaultValue 0
        let next = current + 1
        continuationOrdinals <- Map.add sessionID next continuationOrdinals
        next

    member _.GetNudgeOrdinal(sessionID: string) : int =
        Map.tryFind sessionID nudgeOrdinals |> Option.defaultValue 0

    member _.SetNudgeOrdinal (sessionID: string) (ordinal: int) : unit =
        nudgeOrdinals <- Map.add sessionID ordinal nudgeOrdinals

    member _.IncrementNudgeOrdinal(sessionID: string) : int =
        let current = Map.tryFind sessionID nudgeOrdinals |> Option.defaultValue 0
        let next = current + 1
        nudgeOrdinals <- Map.add sessionID next nudgeOrdinals
        next

    member _.GetCompactionOrdinal(sessionID: string) : int =
        Map.tryFind sessionID compactionOrdinals |> Option.defaultValue 0

    member _.SetCompactionOrdinal (sessionID: string) (ordinal: int) : unit =
        compactionOrdinals <- Map.add sessionID ordinal compactionOrdinals

    member _.IncrementCompactionOrdinal(sessionID: string) : int =
        let current = Map.tryFind sessionID compactionOrdinals |> Option.defaultValue 0
        let next = current + 1
        compactionOrdinals <- Map.add sessionID next compactionOrdinals
        next

    member _.GetLastHumanMessageId(sessionID: string) : string =
        Map.tryFind sessionID lastHumanMessageIds |> Option.defaultValue ""

    member _.SetLastHumanMessageId (sessionID: string) (messageId: string) : unit =
        lastHumanMessageIds <- Map.add sessionID messageId lastHumanMessageIds

    member _.ClearLastHumanMessageId(sessionID: string) : unit =
        lastHumanMessageIds <- Map.remove sessionID lastHumanMessageIds

    member _.IncrementCancelGeneration(sessionID: string) : int =
        let current = Map.tryFind sessionID cancelGenerations |> Option.defaultValue 0
        let next = current + 1
        cancelGenerations <- Map.add sessionID next cancelGenerations
        next

    member _.GetCancelGeneration(sessionID: string) : int =
        Map.tryFind sessionID cancelGenerations |> Option.defaultValue 0

    member _.SetActiveContinuationGeneration (sessionID: string) (gen: int) : unit =
        activeContinuationGens <- Map.add sessionID gen activeContinuationGens

    member _.GetActiveContinuationGeneration(sessionID: string) : int =
        Map.tryFind sessionID activeContinuationGens |> Option.defaultValue 0

    member _.SetActiveContinuationCancelGeneration (sessionID: string) (gen: int) : unit =
        activeContinuationCancelGens <- Map.add sessionID gen activeContinuationCancelGens

    member _.GetActiveContinuationCancelGeneration(sessionID: string) : int =
        Map.tryFind sessionID activeContinuationCancelGens |> Option.defaultValue 0

    member _.SetPendingLease(sessionID: string, lease: PendingLease) : unit =
        pendingLeases <- Map.add sessionID (box lease) pendingLeases

    member _.TryGetPendingLease(sessionID: string) : PendingLease option =
        match Map.tryFind sessionID pendingLeases with
        | Some leaseObj -> Some(leaseObj :?> PendingLease)
        | None -> None

    member _.TryClearPendingLease(sessionID: string, continuationID: string) : bool =
        match Map.tryFind sessionID pendingLeases with
        | Some leaseObj ->
            let lease = leaseObj :?> PendingLease

            if lease.ContinuationID = continuationID then
                pendingLeases <- Map.remove sessionID pendingLeases
                true
            else
                false
        | None -> false

    member this.TryTransitionPendingLease
        (sessionID: string, expectedID: string, expectedStatus: string, nextStatus: string)
        : bool =
        match Map.tryFind sessionID pendingLeases with
        | Some leaseObj ->
            let lease = leaseObj :?> PendingLease

            let isCurrent =
                lease.ContinuationID = expectedID
                && lease.Status = expectedStatus
                && lease.SessionGeneration = this.GetSessionGeneration(sessionID)
                && lease.HumanTurnID = this.GetHumanTurnId(sessionID)
                && lease.CancelGeneration = this.GetCancelGeneration(sessionID)
                && this.GetSessionOwner(sessionID) = "Fallback"

            if isCurrent then
                pendingLeases <- Map.add sessionID (box { lease with Status = nextStatus }) pendingLeases

                true
            else
                false
        | None -> false

    member _.ClearPendingLease(sessionID: string) : unit =
        pendingLeases <- Map.remove sessionID pendingLeases

    member _.SetPendingNudgeLease(sessionID: string, lease: NudgeLease) : unit =
        pendingNudgeLeases <- Map.add sessionID lease pendingNudgeLeases

    member _.TryGetPendingNudgeLease(sessionID: string) : NudgeLease option =
        Map.tryFind sessionID pendingNudgeLeases

    member this.ApplyCancelNudgeLease(sessionID: string, expectedNudgeID: string) : bool =
        match Map.tryFind sessionID pendingNudgeLeases with
        | Some lease when lease.NudgeID = expectedNudgeID ->
            pendingNudgeLeases <- Map.remove sessionID pendingNudgeLeases
            activeNudgeNonces <- Map.remove sessionID activeNudgeNonces
            this.SetNudgeActive sessionID false

            if this.GetSessionOwner sessionID = "Nudge" then
                this.SetSessionOwner sessionID "None"

            true
        | _ -> false

    member this.TryTransitionPendingNudgeLease
        (sessionID: string, expectedID: string, expectedStatus: string, nextStatus: string)
        : bool =
        match Map.tryFind sessionID pendingNudgeLeases with
        | Some lease ->
            let isCurrent =
                lease.NudgeID = expectedID
                && lease.Status = expectedStatus
                && lease.SessionGeneration = this.GetSessionGeneration(sessionID)
                && lease.HumanTurnID = this.GetHumanTurnId(sessionID)
                && lease.CancelGeneration = this.GetCancelGeneration(sessionID)
                && this.GetSessionOwner(sessionID) = "Nudge"

            if isCurrent then
                pendingNudgeLeases <- Map.add sessionID { lease with Status = nextStatus } pendingNudgeLeases
                true
            else
                false
        | None -> false

    member _.TryClearPendingNudgeLease(sessionID: string, expectedNudgeID: string) : bool =
        match Map.tryFind sessionID pendingNudgeLeases with
        | Some lease when lease.NudgeID = expectedNudgeID ->
            pendingNudgeLeases <- Map.remove sessionID pendingNudgeLeases
            true
        | _ -> false

    member _.ClearPendingNudgeLease(sessionID: string) : unit =
        pendingNudgeLeases <- Map.remove sessionID pendingNudgeLeases

    member _.SetActiveCompactionId(sessionID: string, id: string, ordinal: int) : unit =
        activeCompactionIds <- Map.add sessionID id activeCompactionIds
        activeCompactionOrdinals <- Map.add sessionID ordinal activeCompactionOrdinals

    member _.GetActiveCompactionOrdinal(sessionID: string) : int =
        Map.tryFind sessionID activeCompactionOrdinals |> Option.defaultValue 0

    member _.TryGetSettleInfo(sessionID: string, expectedCompactionID: string) : (string * int) option =
        match Map.tryFind sessionID activeCompactionIds with
        | Some currentCompID when currentCompID = expectedCompactionID ->
            let ordinal =
                Map.tryFind sessionID activeCompactionOrdinals |> Option.defaultValue 0

            Some(currentCompID, ordinal)
        | _ -> None

    member this.ApplySettle(sessionID: string, expectedCompactionID: string) : bool =
        match Map.tryFind sessionID activeCompactionIds with
        | Some currentCompID when currentCompID = expectedCompactionID ->
            activeCompactionIds <- Map.remove sessionID activeCompactionIds
            activeCompactionOrdinals <- Map.remove sessionID activeCompactionOrdinals
            compactionGenerations <- Map.remove sessionID compactionGenerations
            compactedSessions <- Set.remove sessionID compactedSessions
            compactionContinuationObserved <- Set.remove sessionID compactionContinuationObserved

            if this.GetSessionOwner sessionID = "Compaction" then
                this.ClearSessionOwner sessionID

            true
        | _ -> false

    member _.SetActiveNudgeNonce (sessionID: string) (nonce: string) : unit =
        activeNudgeNonces <- Map.add sessionID nonce activeNudgeNonces

    member _.GetActiveNudgeNonce(sessionID: string) : string =
        Map.tryFind sessionID activeNudgeNonces |> Option.defaultValue ""

    member _.ClearActiveNudgeNonce(sessionID: string) : unit =
        activeNudgeNonces <- Map.remove sessionID activeNudgeNonces

    member _.SetCompacted (sessionID: string) (value: bool) : unit =
        if value then
            compactedSessions <- Set.add sessionID compactedSessions
        else
            compactedSessions <- Set.remove sessionID compactedSessions

        triggerStateChanged sessionID

    member _.IsCompacted(sessionID: string) : bool =
        Set.contains sessionID compactedSessions

    member _.SetCompactionContinuationObserved (sessionID: string) (value: bool) : unit =
        if value then
            compactionContinuationObserved <- Set.add sessionID compactionContinuationObserved
        else
            compactionContinuationObserved <- Set.remove sessionID compactionContinuationObserved

        triggerStateChanged sessionID

    member _.IsCompactionContinuationObserved(sessionID: string) : bool =
        Set.contains sessionID compactionContinuationObserved

    member _.SetCompactionGeneration (sessionID: string) (gen: int) : unit =
        compactionGenerations <- Map.add sessionID gen compactionGenerations

    member _.GetCompactionGeneration(sessionID: string) : int =
        Map.tryFind sessionID compactionGenerations |> Option.defaultValue 0

    member _.GetActiveCompactionId(sessionID: string) : string =
        Map.tryFind sessionID activeCompactionIds |> Option.defaultValue ""

    member _.MarkForceStopped(sessionID: string) : unit =
        forceStoppedSessions <- Set.add sessionID forceStoppedSessions

    member _.RemoveForceStopped(sessionID: string) : unit =
        forceStoppedSessions <- Set.remove sessionID forceStoppedSessions

    member _.IsForceStopped(sessionID: string) : bool =
        Set.contains sessionID forceStoppedSessions

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

        if value then
            activeObservations <- Map.add sessionID AttemptObservation.AwaitingStart activeObservations

        triggerStateChanged sessionID

    member _.IsAwaitingBusy(sessionID: string) : bool =
        isGateActive activeGates sessionID FallbackSessionGateFlag.AwaitingBusy

    member this.UpdateActiveObservation(sessionID: string, obs: AttemptObservation) : unit =
        activeObservations <- Map.add sessionID obs activeObservations

        match Map.tryFind sessionID activeRunByChild with
        | Some runId ->
            match Map.tryFind (sessionID, runId) subsessionRuns with
            | Some subRun ->
                match subRun.ActiveObservation, obs with
                | AttemptObservation.AssistantObserved _, AttemptObservation.RunningObserved -> ()
                | _ -> subRun.ActiveObservation <- obs
            | None -> ()
        | None -> ()

    member this.SetBusyObserved(sessionID: string, value: bool) : unit =
        let obs =
            if value then
                AttemptObservation.RunningObserved
            else
                AttemptObservation.AwaitingStart

        this.UpdateActiveObservation(sessionID, obs)

    member this.IsBusyOrAssistantObserved(sessionID: string) : bool =
        let subRunObs =
            match Map.tryFind sessionID activeRunByChild with
            | Some runId ->
                match Map.tryFind (sessionID, runId) subsessionRuns with
                | Some subRun -> Some subRun.ActiveObservation
                | None -> None
            | None -> None

        let obs =
            match subRunObs with
            | Some o -> o
            | None ->
                Map.tryFind sessionID activeObservations
                |> Option.defaultValue AttemptObservation.AwaitingStart

        match obs with
        | AttemptObservation.RunningObserved
        | AttemptObservation.AssistantObserved _ -> true
        | AttemptObservation.AwaitingStart -> false

    member this.IsBusyObserved(sessionID: string) : bool =
        this.IsBusyOrAssistantObserved sessionID

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

    member _.SetSessionOwner (sessionID: string) (owner: string) : unit =
        sessionOwners <- Map.add sessionID owner sessionOwners

    member _.GetSessionOwner(sessionID: string) : string =
        Map.tryFind sessionID sessionOwners |> Option.defaultValue "None"

    member _.ClearSessionOwner(sessionID: string) : unit =
        sessionOwners <- Map.remove sessionID sessionOwners

    member _.RestoreSubsessionRun(lease: SubsessionRunLease) : unit =
        subsessionRuns <- Map.add (lease.ChildId, lease.RunId) lease subsessionRuns

        if
            lease.Status = SubsessionRunStatus.Requested
            || lease.Status = SubsessionRunStatus.Running
            || lease.Status = SubsessionRunStatus.Continuing
        then
            activeRunByChild <- Map.add lease.ChildId lease.RunId activeRunByChild

    member _.StartSubsessionRun(childID: string, parentSessionID: string, runId: string) : bool =
        let mutable canStart = true

        match Map.tryFind childID activeRunByChild with
        | Some oldRunId ->
            match Map.tryFind (childID, oldRunId) subsessionRuns with
            | Some oldLease ->
                if
                    oldLease.Status = SubsessionRunStatus.Requested
                    || oldLease.Status = SubsessionRunStatus.Running
                    || oldLease.Status = SubsessionRunStatus.Continuing
                then
                    canStart <- false
            | None -> ()
        | None -> ()

        if canStart then
            activeRunByChild <- Map.add childID runId activeRunByChild

            let lease =
                { RunId = runId
                  ChildId = childID
                  ParentSessionId = parentSessionID
                  ActiveAttemptOrdinal = 0
                  Status = SubsessionRunStatus.Requested
                  ActiveContinuationId = ""
                  ActiveContinuationOrdinal = 0
                  DispatchMessageBoundary = None
                  ActiveObservation = AttemptObservation.AwaitingStart
                  InjectedUserMessageId = None }

            subsessionRuns <- Map.add (childID, runId) lease subsessionRuns
            true
        else
            false

    member this.ActivateAttempt
        (sessionID: string, continuationID: string, continuationOrdinal: int, dispatchBoundary: string option)
        : unit =
        match Map.tryFind sessionID activeRunByChild with
        | Some runId ->
            match Map.tryFind (sessionID, runId) subsessionRuns with
            | Some lease ->
                lease.ActiveAttemptOrdinal <- lease.ActiveAttemptOrdinal + 1
                lease.ActiveContinuationId <- continuationID
                lease.ActiveContinuationOrdinal <- continuationOrdinal
                lease.DispatchMessageBoundary <- dispatchBoundary
                lease.ActiveObservation <- AttemptObservation.AwaitingStart
                lease.InjectedUserMessageId <- None
            | None -> ()
        | None -> ()

    member this.CheckSubsessionEventMatch
        (
            sessionID: string,
            continuationId: string,
            continuationOrdinal: int,
            isBusyOrAssistant: bool,
            eventMessageBoundary: string option,
            eventParentId: string option,
            eventHostRunId: string option,
            isError: bool
        ) : SubsessionMatchResult option =
        let res =
            match Map.tryFind sessionID activeRunByChild with
            | Some runId ->
                match Map.tryFind (sessionID, runId) subsessionRuns with
                | Some subRun ->
                    if subRun.ActiveAttemptOrdinal = 0 || subRun.ActiveContinuationId = "" then
                        None
                    else
                        let hostRunMatches =
                            match eventHostRunId with
                            | Some rid ->
                                (subRun.InjectedUserMessageId |> Option.exists (fun inj -> inj = rid))
                                || (subRun.RunId = rid)
                            | None -> false

                        let parentIdMatches =
                            match eventParentId, subRun.InjectedUserMessageId with
                            | Some pId, Some injId when pId = injId -> true
                            | _ -> false

                        let hasIdMismatch =
                            (match eventParentId, subRun.InjectedUserMessageId with
                             | Some pId, Some injId when pId <> injId -> true
                             | None, Some _ -> true // Expected a parent ID, but none was provided
                             | _ -> false)
                            || (match eventHostRunId with
                                | Some rid ->
                                    rid <> subRun.RunId
                                    && not (subRun.InjectedUserMessageId |> Option.exists (fun inj -> inj = rid))
                                | None -> false)
                            || (continuationId <> "" && continuationId <> subRun.ActiveContinuationId)
                            || (continuationOrdinal <> 0
                                && continuationOrdinal <> subRun.ActiveContinuationOrdinal)

                        if hasIdMismatch then
                            Some SubsessionMatchResult.NoMatch
                        elif hostRunMatches || parentIdMatches then
                            if isBusyOrAssistant then
                                match eventMessageBoundary with
                                | Some evB -> subRun.ActiveObservation <- AttemptObservation.AssistantObserved evB
                                | None -> subRun.ActiveObservation <- AttemptObservation.RunningObserved

                            Some SubsessionMatchResult.StrongMatch
                        elif continuationId <> "" then
                            let matched = continuationId = subRun.ActiveContinuationId

                            if matched then
                                if isBusyOrAssistant then
                                    match eventMessageBoundary with
                                    | Some evB -> subRun.ActiveObservation <- AttemptObservation.AssistantObserved evB
                                    | None -> subRun.ActiveObservation <- AttemptObservation.RunningObserved

                                Some SubsessionMatchResult.StrongMatch
                            else
                                Some SubsessionMatchResult.NoMatch
                        elif continuationOrdinal <> 0 then
                            let matched = continuationOrdinal = subRun.ActiveContinuationOrdinal

                            if matched then
                                if isBusyOrAssistant then
                                    match eventMessageBoundary with
                                    | Some evB -> subRun.ActiveObservation <- AttemptObservation.AssistantObserved evB
                                    | None -> subRun.ActiveObservation <- AttemptObservation.RunningObserved

                                Some SubsessionMatchResult.StrongMatch
                            else
                                Some SubsessionMatchResult.NoMatch
                        else
                            match eventMessageBoundary, subRun.DispatchMessageBoundary with
                            | Some evB, Some dispB ->
                                if evB <> dispB then
                                    if isBusyOrAssistant then
                                        subRun.ActiveObservation <- AttemptObservation.AssistantObserved evB
                                        Some SubsessionMatchResult.BoundaryMatch
                                    else
                                        match subRun.ActiveObservation with
                                        | AttemptObservation.AssistantObserved obsB when obsB = evB ->
                                            Some SubsessionMatchResult.BoundaryMatch
                                        | _ -> Some SubsessionMatchResult.NoMatch
                                else
                                    Some SubsessionMatchResult.NoMatch
                            | Some evB, None ->
                                if isBusyOrAssistant then
                                    subRun.ActiveObservation <- AttemptObservation.AssistantObserved evB
                                    Some SubsessionMatchResult.BoundaryMatch
                                else
                                    match subRun.ActiveObservation with
                                    | AttemptObservation.AssistantObserved obsB when obsB = evB ->
                                        Some SubsessionMatchResult.BoundaryMatch
                                    | _ -> Some SubsessionMatchResult.NoMatch
                            | None, _ ->
                                if isBusyOrAssistant then
                                    match subRun.ActiveObservation with
                                    | AttemptObservation.AwaitingStart ->
                                        match subRun.DispatchMessageBoundary with
                                        | None ->
                                            subRun.ActiveObservation <- AttemptObservation.RunningObserved
                                            Some SubsessionMatchResult.StrongMatch
                                        | Some _ ->
                                            if isError then
                                                Some SubsessionMatchResult.StrongMatch
                                            else
                                                Some SubsessionMatchResult.NoMatch
                                    | _ -> Some SubsessionMatchResult.StrongMatch
                                else
                                    match subRun.ActiveObservation with
                                    | AttemptObservation.RunningObserved
                                    | AttemptObservation.AssistantObserved _ -> Some SubsessionMatchResult.StrongMatch
                                    | AttemptObservation.AwaitingStart -> Some SubsessionMatchResult.NoMatch
                | None -> Some SubsessionMatchResult.NoMatch
            | None -> None

        res

    member _.GetSubsessionRun(childID: string, expectedRunId: string) : SubsessionRunLease option =
        Map.tryFind (childID, expectedRunId) subsessionRuns

    member _.TryGetActiveRunId(childID: string) : string option = Map.tryFind childID activeRunByChild

    member _.UpdateSubsessionRunStatus(childID: string, expectedRunId: string, status: SubsessionRunStatus) : unit =
        match Map.tryFind (childID, expectedRunId) subsessionRuns with
        | Some lease -> lease.Status <- status
        | _ -> ()

    member _.ClearSubsessionRun(childID: string, expectedRunId: string) : unit =
        subsessionRuns <- Map.remove (childID, expectedRunId) subsessionRuns

        match Map.tryFind childID activeRunByChild with
        | Some activeId when activeId = expectedRunId -> activeRunByChild <- Map.remove childID activeRunByChild
        | _ -> ()

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
        latestHumanModels <- Map.remove sessionID latestHumanModels
        humanTurnIds <- Map.remove sessionID humanTurnIds
        sessionGenerations <- Map.remove sessionID sessionGenerations
        cancelGenerations <- Map.remove sessionID cancelGenerations
        activeContinuationGens <- Map.remove sessionID activeContinuationGens
        activeContinuationCancelGens <- Map.remove sessionID activeContinuationCancelGens
        sessionOwners <- Map.remove sessionID sessionOwners
        pendingLeases <- Map.remove sessionID pendingLeases
        activeCompactionIds <- Map.remove sessionID activeCompactionIds
        activeCompactionOrdinals <- Map.remove sessionID activeCompactionOrdinals
        forceStoppedSessions <- Set.remove sessionID forceStoppedSessions
        compactedSessions <- Set.remove sessionID compactedSessions
        compactionContinuationObserved <- Set.remove sessionID compactionContinuationObserved
        compactionGenerations <- Map.remove sessionID compactionGenerations
        activeNudgeNonces <- Map.remove sessionID activeNudgeNonces
        pendingNudgeLeases <- Map.remove sessionID pendingNudgeLeases
        humanTurnOrdinals <- Map.remove sessionID humanTurnOrdinals
        continuationOrdinals <- Map.remove sessionID continuationOrdinals
        nudgeOrdinals <- Map.remove sessionID nudgeOrdinals
        subsessionRuns <- subsessionRuns |> Map.filter (fun (cid, _) _ -> cid <> sessionID)
        activeRunByChild <- Map.remove sessionID activeRunByChild
        activeObservations <- Map.remove sessionID activeObservations
        compactionOrdinals <- Map.remove sessionID compactionOrdinals
        lastHumanMessageIds <- Map.remove sessionID lastHumanMessageIds
        triggerStateChanged sessionID
