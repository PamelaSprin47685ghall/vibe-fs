module Wanxiangshu.Runtime.MessageTransform.PlanHelpers

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

let stableProjectionBytes
    (cleaned: Message<obj> list)
    (backlogOpsHost: Host)
    (backlogSessionID: string)
    (backlog: BacklogEntry list)
    (encodeMessages: Message<obj> list -> obj array)
    : int =
    projectBacklogFor backlogOpsHost cleaned backlog FoldStrategy.FoldAfterFirst backlogSessionID
    |> encodeMessages
    |> box
    |> utf8JsonBytes

let computeStableTokensAndPhaseState
    (sessionID: string)
    (cleaned: Message<obj> list)
    (host: Host)
    (backlogOpsHost: Host)
    (scope: RuntimeScope)
    (backlogSessionID: string)
    (backlog: BacklogEntry list)
    (currentStore: ContextBudgetEntry)
    (encodeMessages: Message<obj> list -> obj array)
    (currentTokens: int)
    : JS.Promise<int64 * ContextState> =
    promise {
        let stableMessages =
            projectBacklogFor backlogOpsHost cleaned backlog FoldStrategy.FoldAfterFirst backlogSessionID

        let stableEncoded = encodeMessages stableMessages
        let stableBytes = utf8JsonBytes (box stableEncoded)

        let stableTokens =
            match currentStore.LastCalibration with
            | Some calibration ->
                estimateTokensFromCalibration calibration stableBytes
                |> Option.defaultValue (int64 (max 1 (stableBytes / 2)))
            | None -> int64 currentTokens

        let backlogBytes =
            ContextBudgetUsageCodec.backlogBytesFromEncoded backlogOpsHost stableEncoded

        let backlogTokens =
            if int64 stableBytes <= 0L then
                0L
            else
                stableTokens * int64 backlogBytes / int64 stableBytes

        let currentOrdinal =
            flatten cleaned
            |> List.filter (fun fp -> isTodoResultFor host fp.part)
            |> List.length

        let newState =
            { phaseBaseTokens = stableTokens
              backlogTokensAtPhaseStart = backlogTokens
              phaseStartTodoOrdinal = currentOrdinal }

        return stableTokens, newState
    }

let rebuildPhaseState
    (sessionID: string)
    (cleaned: Message<obj> list)
    (host: Host)
    (backlogOpsHost: Host)
    (backlogSessionID: string)
    (scope: RuntimeScope)
    (backlog: BacklogEntry list)
    (currentStore: ContextBudgetEntry)
    (encodeMessages: Message<obj> list -> obj array)
    (currentTokens: int)
    (totalBytes: int)
    (forceRebuild: bool)
    : JS.Promise<ContextState * bool> =
    promise {
        let isJustInitialized = currentStore.State.IsNone

        if backlog <> currentStore.LastBacklog || isJustInitialized || forceRebuild then
            let! stableTokens, newState =
                computeStableTokensAndPhaseState
                    sessionID
                    cleaned
                    host
                    backlogOpsHost
                    scope
                    backlogSessionID
                    backlog
                    currentStore
                    encodeMessages
                    currentTokens

            let nextEpisode = System.Guid.NewGuid().ToString("N")

            ContextBudgetStore.update scope sessionID (fun entry ->
                { entry with
                    State = Some newState
                    LastBacklog = backlog
                    NudgeTrack = afterPhaseBoundaryReset entry.NudgeTrack
                    EpisodeID = nextEpisode
                    NudgeCount = 0
                    SignalTodoOrdinal = None
                    SignalTokens = None
                    StableSyntheticNudgeID = None })

            return newState, isJustInitialized
        else
            return currentStore.State.Value, false
    }
