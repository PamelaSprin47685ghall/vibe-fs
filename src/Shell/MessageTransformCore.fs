module Wanxiangshu.Shell.MessageTransformCore

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.BacklogProjection
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.ContextBudgetStore
open Wanxiangshu.Shell.ContextBudgetUsageCodec
open Fable.Core

[<RequireQualifiedAccess>]
type ProjectionPolicy =
    | IncludeProjection
    | ExcludeProjection

type MessageTransformPlan =
    { SessionID: string
      Agent: string
      Directory: string
      ProjectionPolicy: ProjectionPolicy
      BacklogProjectionPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy
      CapsInjectionPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy
      ParallelHintPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy
      ContextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy
      IsSubagentSession: bool
      Cleaned: Message<obj> list
      RawArray: obj array option
      SembleInjectEnabled: bool
      Scope: RuntimeScope
      MaxInputTokens: int
      GetContextUsage: obj array -> JS.Promise<int option> }

type BacklogSessionOps =
    { Host: Host
      GetOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list }

let backlogSessionOpsFrom
    (host: Host)
    (getOrRebuildBacklog: string -> Message<obj> list -> BacklogEntry list)
    : BacklogSessionOps =
    { Host = host
      GetOrRebuildBacklog = getOrRebuildBacklog }

let applyBacklogProjection
    (sessionID: string)
    (policy: Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy)
    (backlogSession: BacklogSessionOps)
    (cleaned: Message<obj> list)
    : Message<obj> list =
    match policy with
    | Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Exclude -> cleaned
    | Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include ->
        let backlog = backlogSession.GetOrRebuildBacklog sessionID cleaned
        projectBacklogFor backlogSession.Host cleaned backlog FoldStrategy.FoldAfterSecond sessionID

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

let private resolveCurrentTokens
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

let private rebuildPhaseState
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (backlog: BacklogEntry list)
    (currentStore: ContextBudgetEntry)
    (encodeMessages: Message<obj> list -> obj array)
    (currentTokens: int)
    (totalBytes: int)
    : JS.Promise<ContextState * bool> =
    promise {
        let isJustInitialized = currentStore.State.IsNone

        if backlog <> currentStore.LastBacklog || isJustInitialized then
            let stableMessages =
                projectBacklogFor backlogOps.Host plan.Cleaned backlog FoldStrategy.FoldAfterFirst plan.SessionID

            let stableEncoded = encodeMessages stableMessages
            let stableBytes = utf8JsonBytes (box stableEncoded)
            let! stableTokensOpt = plan.GetContextUsage stableEncoded

            let stableTokens =
                match stableTokensOpt with
                | Some t -> int64 t
                | None ->
                    let currentLastUsage =
                        (ContextBudgetStore.get plan.Scope plan.SessionID).LastUsage
                        |> Option.map (fun u ->
                            {| tokenCount = u.tokenCount
                               textBytes = u.textBytes |})

                    match estimateTokens stableBytes currentLastUsage with
                    | Some t -> int64 t
                    | None -> int64 currentTokens

            let backlogBytes =
                ContextBudgetUsageCodec.backlogBytesFromEncoded backlogOps.Host stableEncoded

            let backlogTokens =
                if int64 stableBytes <= 0L then
                    0L
                else
                    stableTokens * int64 backlogBytes / int64 stableBytes

            let currentOrdinal =
                flatten plan.Cleaned
                |> List.filter (fun fp -> isTodoResultFor backlogOps.Host fp.part)
                |> List.length

            let newState =
                { phaseBaseTokens = stableTokens
                  backlogTokensAtPhaseStart = backlogTokens
                  phaseStartTodoOrdinal = currentOrdinal }

            let nextEpisode = System.Guid.NewGuid().ToString("N")

            ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
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

let private checkAndInjectNudge
    (plan: MessageTransformPlan)
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

        match classifyPressure plan.MaxInputTokens false (int64 currentTokens) state completedTodoCount with
        | RequireTodoWriteEmergency when
            plan.ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
            ->
            let isSameEpisode =
                storeEntry.NudgeTrack = EmergencySignaled
                && storeEntry.SignalTodoOrdinal = Some completedTodoCount

            if isSameEpisode then
                let nudgeId =
                    match storeEntry.StableSyntheticNudgeID with
                    | Some id -> id
                    | None -> "context-budget-nudge-" + System.Guid.NewGuid().ToString()

                let nudgeMsg = buildContextBudgetNudgeMessage plan.SessionID nudgeId
                List.append messages [ nudgeMsg ]
            else
                let isMaxReached = storeEntry.NudgeCount >= 2

                if isMaxReached then
                    messages
                else
                    let stableId = "context-budget-nudge-" + System.Guid.NewGuid().ToString()

                    ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                        { entry with
                            NudgeTrack = EmergencySignaled
                            NudgeCount = entry.NudgeCount + 1
                            SignalTodoOrdinal = Some completedTodoCount
                            SignalTokens = Some(int64 currentTokens)
                            StableSyntheticNudgeID = Some stableId })

                    let nudgeMsg = buildContextBudgetNudgeMessage plan.SessionID stableId
                    List.append messages [ nudgeMsg ]
        | _ -> messages

let applyContextBudget
    (plan: MessageTransformPlan)
    (backlogOps: BacklogSessionOps)
    (messages: Message<obj> list)
    (encodedAll: obj array)
    (encodeMessages: Message<obj> list -> obj array)
    : JS.Promise<Message<obj> list> =
    promise {
        if
            messages.IsEmpty
            || plan.MaxInputTokens <= 0
            || plan.ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Disable
        then
            return messages
        else
            let totalBytes = utf8JsonBytes (box encodedAll)
            let! tokenCountOpt = plan.GetContextUsage encodedAll
            let storeEntry = ContextBudgetStore.get plan.Scope plan.SessionID

            let currentTokens, confidence =
                resolveCurrentTokens totalBytes tokenCountOpt storeEntry

            ContextBudgetStore.update plan.Scope plan.SessionID (fun entry ->
                { entry with
                    LastUsage =
                        Some
                            {| tokenCount = currentTokens
                               textBytes = totalBytes
                               confidence = confidence |} })

            let backlog = backlogOps.GetOrRebuildBacklog plan.SessionID plan.Cleaned
            let currentStore = ContextBudgetStore.get plan.Scope plan.SessionID

            let! state, isJustInitialized =
                rebuildPhaseState plan backlogOps backlog currentStore encodeMessages currentTokens totalBytes

            if isJustInitialized then
                return messages
            else
                let finalStoreEntry = ContextBudgetStore.get plan.Scope plan.SessionID
                return checkAndInjectNudge plan currentTokens confidence state messages backlogOps.Host finalStoreEntry
    }
