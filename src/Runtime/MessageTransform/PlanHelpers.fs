module Wanxiangshu.Runtime.MessageTransform.PlanHelpers

open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ContextBudgetStore
open Wanxiangshu.Runtime.ContextBudgetUsageCodec
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Runtime.BacklogProjection
open Fable.Core

let contextBudgetNudgeText =
    "Attention: the system context is about to be suspended. "
    + "You must immediately force an emergency stop to all work "
    + "and call the todowrite tool."

let buildContextBudgetNudgeMessage (sessionID: string) (id: string) : Message<obj> =
    { info =
        { id = id
          sessionID = sessionID
          role = User
          agent = "orchestrator"
          isError = false
          toolName = ""
          details = null
          time = null }
      parts = [ TextPart contextBudgetNudgeText ]
      source = Synthetic "context-budget-nudge-"
      raw = null }

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

let computeStableTokensAndPhaseState
    (sessionID: string)
    (getContextUsage: obj array -> JS.Promise<int option>)
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
        let! stableTokensOpt = getContextUsage stableEncoded

        let currentLastUsage =
            (ContextBudgetStore.get scope sessionID).LastUsage
            |> Option.map (fun u ->
                {| tokenCount = u.tokenCount
                   textBytes = u.textBytes |})

        let stableTokens =
            match stableTokensOpt with
            | Some t -> int64 t
            | None ->
                match estimateTokens stableBytes currentLastUsage with
                | Some t -> int64 t
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
    (getContextUsage: obj array -> JS.Promise<int option>)
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
                    getContextUsage
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

let checkAndInjectNudge
    (sessionID: string)
    (maxInputTokens: int)
    (contextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy)
    (scope: RuntimeScope)
    (currentTokens: int)
    (confidence: UsageConfidence)
    (state: ContextState)
    (messages: Message<obj> list)
    (host: Host)
    (storeEntry: ContextBudgetEntry)
    : Message<obj> list =
    if confidence = UsageConfidence.BootstrapEstimate then
        messages
    elif int64 currentTokens <= state.phaseBaseTokens then
        messages
    else
        let completedTodoCount =
            flatten messages
            |> List.filter (fun fp -> isTodoResultFor host fp.part)
            |> List.length

        match classifyPressure maxInputTokens false (int64 currentTokens) state completedTodoCount with
        | RequireTodoWriteEmergency when
            contextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
            ->
            let isSameEpisode =
                storeEntry.NudgeTrack = EmergencySignaled
                && storeEntry.SignalTodoOrdinal = Some completedTodoCount

            if isSameEpisode then
                let nudgeId =
                    match storeEntry.StableSyntheticNudgeID with
                    | Some id -> id
                    | None -> "context-budget-nudge-" + System.Guid.NewGuid().ToString()

                let nudgeMsg = buildContextBudgetNudgeMessage sessionID nudgeId
                List.append messages [ nudgeMsg ]
            else
                let isMaxReached = storeEntry.NudgeCount >= 2

                if isMaxReached then
                    messages
                else
                    let stableId = "context-budget-nudge-" + System.Guid.NewGuid().ToString()

                    ContextBudgetStore.update scope sessionID (fun entry ->
                        { entry with
                            NudgeTrack = EmergencySignaled
                            NudgeCount = entry.NudgeCount + 1
                            SignalTodoOrdinal = Some completedTodoCount
                            SignalTokens = Some(int64 currentTokens)
                            StableSyntheticNudgeID = Some stableId })

                    let nudgeMsg = buildContextBudgetNudgeMessage sessionID stableId
                    List.append messages [ nudgeMsg ]
        | _ -> messages
