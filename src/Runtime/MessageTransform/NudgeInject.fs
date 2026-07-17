module Wanxiangshu.Runtime.MessageTransform.NudgeInject

open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ContextBudgetStore
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ContextBudget
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Backlog.BacklogTypes

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

let completedTodoCount (host: Host) (messages: Message<obj> list) : int =
    flatten messages
    |> List.filter (fun fp -> isTodoResultFor host fp.part)
    |> List.length

let ensureStableNudgeId
    (scope: RuntimeScope)
    (sessionID: string)
    (existing: string option)
    (persist: string -> unit)
    : string =
    match existing with
    | Some id -> id
    | None ->
        let id = "context-budget-nudge-" + System.Guid.NewGuid().ToString()
        persist id
        id

let tryGetOrCreateStableNudgeIdSameEpisode
    (scope: RuntimeScope)
    (sessionID: string)
    (storeEntry: ContextBudgetEntry)
    : string =
    ensureStableNudgeId scope sessionID storeEntry.StableSyntheticNudgeID (fun id ->
        ContextBudgetStore.update scope sessionID (fun entry ->
            { entry with
                StableSyntheticNudgeID = Some id }))

let tryGetOrCreateStableNudgeIdMaxReached
    (scope: RuntimeScope)
    (sessionID: string)
    (todoCount: int)
    (storeEntry: ContextBudgetEntry)
    : string =
    let stableId =
        storeEntry.StableSyntheticNudgeID
        |> Option.defaultValue ("context-budget-nudge-" + System.Guid.NewGuid().ToString())

    ContextBudgetStore.update scope sessionID (fun entry ->
        { entry with
            StableSyntheticNudgeID = Some stableId
            NudgeTrack = EmergencySignaled
            SignalTodoOrdinal = Some todoCount })

    stableId

let updateNudgeStoreForFirstSignal
    (scope: RuntimeScope)
    (sessionID: string)
    (currentTokens: int)
    (todoCount: int)
    (storeEntry: ContextBudgetEntry)
    : string =
    let stableId = "context-budget-nudge-" + System.Guid.NewGuid().ToString()

    ContextBudgetStore.update scope sessionID (fun entry ->
        { entry with
            NudgeTrack = EmergencySignaled
            NudgeCount = entry.NudgeCount + 1
            SignalTodoOrdinal = Some todoCount
            SignalTokens = Some(int64 currentTokens)
            StableSyntheticNudgeID = Some stableId })

    stableId

let isPressureEmergency
    (maxInputTokens: int)
    (currentTokens: int)
    (state: ContextState)
    (completedTodoCount: int)
    : bool =
    match classifyPressure maxInputTokens false (int64 currentTokens) state completedTodoCount with
    | RequireTodoWriteEmergency -> true
    | _ -> false

let shouldIncludeNudge (contextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy) : bool =
    contextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include

let isSameEpisode (storeEntry: ContextBudgetEntry) (completedTodoCount: int) : bool =
    storeEntry.NudgeTrack = EmergencySignaled
    && storeEntry.SignalTodoOrdinal = Some completedTodoCount

let injectSameEpisodeNudge
    (scope: RuntimeScope)
    (sessionID: string)
    (storeEntry: ContextBudgetEntry)
    (messages: Message<obj> list)
    : Message<obj> list =
    let nudgeId = tryGetOrCreateStableNudgeIdSameEpisode scope sessionID storeEntry
    let nudgeMsg = buildContextBudgetNudgeMessage sessionID nudgeId
    List.append messages [ nudgeMsg ]

let injectMaxReachedNudge
    (scope: RuntimeScope)
    (sessionID: string)
    (completedTodoCount: int)
    (storeEntry: ContextBudgetEntry)
    (messages: Message<obj> list)
    : Message<obj> list =
    let stableId =
        tryGetOrCreateStableNudgeIdMaxReached scope sessionID completedTodoCount storeEntry

    List.append messages [ buildContextBudgetNudgeMessage sessionID stableId ]

let injectFirstSignalNudge
    (scope: RuntimeScope)
    (sessionID: string)
    (currentTokens: int)
    (completedTodoCount: int)
    (storeEntry: ContextBudgetEntry)
    (messages: Message<obj> list)
    : Message<obj> list =
    let stableId =
        updateNudgeStoreForFirstSignal scope sessionID currentTokens completedTodoCount storeEntry

    let nudgeMsg = buildContextBudgetNudgeMessage sessionID stableId
    List.append messages [ nudgeMsg ]

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
    if
        confidence = UsageConfidence.BootstrapEstimate
        && not (bootstrapHardSafety (int64 currentTokens) (int64 maxInputTokens))
    then
        messages
    elif int64 currentTokens <= state.phaseBaseTokens then
        messages
    else
        let todoCount = completedTodoCount host messages

        if not (isPressureEmergency maxInputTokens currentTokens state todoCount) then
            messages
        elif not (shouldIncludeNudge contextBudgetPolicy) then
            messages
        elif isSameEpisode storeEntry todoCount then
            injectSameEpisodeNudge scope sessionID storeEntry messages
        elif storeEntry.NudgeCount >= 2 then
            injectMaxReachedNudge scope sessionID todoCount storeEntry messages
        else
            injectFirstSignalNudge scope sessionID currentTokens todoCount storeEntry messages
