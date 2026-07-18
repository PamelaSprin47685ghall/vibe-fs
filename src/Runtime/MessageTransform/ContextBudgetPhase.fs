module Wanxiangshu.Runtime.MessageTransform.ContextBudgetPhase

open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.ContextBudgetStore
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Backlog.BacklogTypes
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.BacklogProjection
open Fable.Core

let resolveCurrentTokens
    (totalBytes: int)
    (tokenCountOpt: int option)
    (storeEntry: ContextBudgetEntry)
    : int * UsageConfidence =
    match tokenCountOpt with
    | Some t when t > 0 -> (t, UsageConfidence.Observed)
    | _ ->
        match storeEntry.LastUsage with
        | Some u when u.confidence <> UsageConfidence.BootstrapEstimate ->
            let simplified =
                Some
                    {| tokenCount = u.tokenCount
                       textBytes = u.textBytes |}

            match estimateTokens totalBytes simplified with
            | Some t when t > 0 -> (t, UsageConfidence.CalibratedEstimate)
            | _ ->
                let estimate = totalBytes / 2
                (max 1 estimate, UsageConfidence.BootstrapEstimate)
        | _ ->
            let estimate = totalBytes / 2
            (max 1 estimate, UsageConfidence.BootstrapEstimate)

let resolveCurrentTokensFromCalibration
    (totalBytes: int)
    (tokenCountOpt: int option)
    (storeEntry: ContextBudgetEntry)
    : int * UsageConfidence =
    match storeEntry.LastCalibration with
    | Some calibration ->
        match estimateTokensFromCalibration calibration totalBytes with
        | Some tokens when tokens > 0L -> int tokens, UsageConfidence.CalibratedEstimate
        | _ ->
            let estimate = max 1 (totalBytes / 2)
            estimate, UsageConfidence.BootstrapEstimate
    | None -> resolveCurrentTokens totalBytes tokenCountOpt storeEntry

let private estimateBytesAsTokens (calibration: UsageCalibration option) (bytes: int) (fallbackTokens: int) : int64 =
    match calibration with
    | Some cal ->
        estimateTokensFromCalibration cal bytes
        |> Option.defaultValue (int64 (max 1 (bytes / 2)))
    | None -> int64 fallbackTokens

type ProjectionMetadata =
    { TotalTodoOrdinal: int
      FoldFrontierOrdinal: int
      RemainingTodoWritesUntilFold: int
      DidAdvanceFoldFrontier: bool }

type PhaseTransition =
    | ColdStart
    | TodoAcknowledged
    | FoldFrontierAdvanced
    | BacklogOnlyChange
    | NoChange

let classifyTransition
    (existingState: ContextState option)
    (projection: BacklogProjectionResult<obj>)
    (lastBacklog: BacklogEntry list)
    (currentBacklog: BacklogEntry list)
    : PhaseTransition =
    match existingState with
    | None -> ColdStart
    | Some state ->
        if
            projection.DidAdvanceFoldFrontier
            && projection.FoldFrontierOrdinal > state.FoldFrontierOrdinal
        then
            FoldFrontierAdvanced
        elif projection.TotalTodoOrdinal > state.BaselineTodoOrdinal then
            TodoAcknowledged
        elif currentBacklog <> lastBacklog then
            BacklogOnlyChange
        else
            NoChange

let projectedMessagesBytes
    (cleaned: Message<obj> list)
    (backlogOpsHost: Host)
    (backlogSessionID: string)
    (backlog: BacklogEntry list)
    (encodeMessages: Message<obj> list -> obj array)
    : BacklogProjectionResult<obj> * int =
    let result =
        projectBacklogFor backlogOpsHost cleaned backlog FoldStrategy.FoldAfterSecond backlogSessionID

    let encoded = encodeMessages result.Messages
    let bytes = utf8JsonBytes (box encoded)
    result, bytes

let private freshEpisodeId () = System.Guid.NewGuid().ToString("N")

let private estimateBaseline (store: ContextBudgetEntry) (projectedBytes: int) (currentTokens: int) : int64 =
    estimateBytesAsTokens store.LastCalibration projectedBytes currentTokens

let private resetNudgeFields (entry: ContextBudgetEntry) : ContextBudgetEntry =
    { entry with
        NudgeTrack = Idle
        NudgeCount = 0
        SignalTodoOrdinal = None
        SignalTokens = None
        StableSyntheticNudgeID = None }

let private withActiveCycle
    (cycle: ContextState)
    (backlog: BacklogEntry list)
    (episodeId: string)
    (entry: ContextBudgetEntry)
    : ContextBudgetEntry =
    { resetNudgeFields entry with
        State = Some cycle
        LastBacklog = backlog
        EpisodeID = episodeId }

let private withRefreshedCycle
    (cycle: ContextState)
    (backlog: BacklogEntry list)
    (entry: ContextBudgetEntry)
    : ContextBudgetEntry =
    { resetNudgeFields entry with
        State = Some cycle
        LastBacklog = backlog }

let private withBacklogOnly (backlog: BacklogEntry list) (entry: ContextBudgetEntry) : ContextBudgetEntry =
    { resetNudgeFields entry with
        LastBacklog = backlog }

let applyTransition
    (scope: RuntimeScope)
    (sessionID: string)
    (transition: PhaseTransition)
    (projection: BacklogProjectionResult<obj>)
    (projectedBytes: int)
    (currentTokens: int)
    (currentStore: ContextBudgetEntry)
    (backlog: BacklogEntry list)
    : ContextState * bool =
    match transition with
    | ColdStart ->
        let baseline = estimateBaseline currentStore projectedBytes currentTokens

        let cycle =
            beginCycle baseline projection.TotalTodoOrdinal projection.RemainingTodoWritesUntilFold

        ContextBudgetStore.update scope sessionID (withActiveCycle cycle backlog (freshEpisodeId ()))
        cycle, true

    | FoldFrontierAdvanced ->
        let baseline = estimateBaseline currentStore projectedBytes currentTokens

        let cycle =
            rebuildCycleAtFold
                baseline
                projection.TotalTodoOrdinal
                projection.FoldFrontierOrdinal
                projection.RemainingTodoWritesUntilFold

        ContextBudgetStore.update scope sessionID (withActiveCycle cycle backlog (freshEpisodeId ()))
        cycle, false

    | TodoAcknowledged ->
        let cycle =
            match currentStore.State with
            | Some existing -> advanceSegment existing projection.TotalTodoOrdinal
            | None ->
                let baseline = estimateBaseline currentStore projectedBytes currentTokens
                beginCycle baseline projection.TotalTodoOrdinal projection.RemainingTodoWritesUntilFold

        ContextBudgetStore.update scope sessionID (withRefreshedCycle cycle backlog)
        cycle, false

    | BacklogOnlyChange ->
        ContextBudgetStore.update scope sessionID (withBacklogOnly backlog)
        currentStore.State.Value, false

    | NoChange -> currentStore.State.Value, false
