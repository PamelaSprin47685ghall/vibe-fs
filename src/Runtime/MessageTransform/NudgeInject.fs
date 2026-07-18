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

let private persistStableNudgeId
    (scope: RuntimeScope)
    (sessionID: string)
    (stableId: string)
    (track: BudgetNudgeTrack)
    (signalOrdinal: int)
    : unit =
    ContextBudgetStore.update scope sessionID (fun entry ->
        { entry with
            StableSyntheticNudgeID = Some stableId
            NudgeTrack = track
            SignalTodoOrdinal = Some signalOrdinal })

let shouldIncludeNudge (contextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy) : bool =
    contextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include

let checkAndInjectNudge
    (sessionID: string)
    (maxInputTokens: int)
    (contextBudgetPolicy: Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy)
    (scope: RuntimeScope)
    (currentTokens: int)
    (confidence: UsageConfidence)
    (state: ContextState)
    (messages: Message<obj> list)
    (currentTodoOrdinal: int)
    (storeEntry: ContextBudgetEntry)
    : Message<obj> list * ContextBudgetPressure * NudgeAction =
    if
        confidence = UsageConfidence.BootstrapEstimate
        && not (bootstrapHardSafety (int64 currentTokens) (int64 maxInputTokens))
    then
        messages, BelowThreshold, NoNudge
    elif int64 currentTokens <= state.BaselineTokens then
        messages, BelowThreshold, NoNudge
    else
        let pressure = classifyPressure maxInputTokens (int64 currentTokens) state

        let action =
            classifyNudgeAction
                pressure
                storeEntry.NudgeTrack
                storeEntry.SignalTodoOrdinal
                currentTodoOrdinal
                storeEntry.NudgeCount

        if not (shouldIncludeNudge contextBudgetPolicy) || action = NoNudge then
            messages, pressure, action
        else
            let stableId =
                match action with
                | InjectFirstSignal ->
                    let id = "context-budget-nudge-" + System.Guid.NewGuid().ToString()

                    persistStableNudgeId scope sessionID id EmergencySignaled currentTodoOrdinal

                    ContextBudgetStore.update scope sessionID (fun entry ->
                        { entry with
                            NudgeCount = entry.NudgeCount + 1
                            SignalTokens = Some(int64 currentTokens) })

                    id
                | InjectSameEpisode ->
                    ensureStableNudgeId scope sessionID storeEntry.StableSyntheticNudgeID (fun id ->
                        persistStableNudgeId scope sessionID id EmergencySignaled currentTodoOrdinal)
                | InjectCatchUp ->
                    let id =
                        storeEntry.StableSyntheticNudgeID
                        |> Option.defaultValue ("context-budget-nudge-" + System.Guid.NewGuid().ToString())

                    persistStableNudgeId scope sessionID id EmergencySignaled currentTodoOrdinal

                    ContextBudgetStore.update scope sessionID (fun entry ->
                        { entry with
                            NudgeCount = entry.NudgeCount + 1
                            SignalTokens = Some(int64 currentTokens) })

                    id
                | NoNudge -> ""

            let nudgeMsg = buildContextBudgetNudgeMessage sessionID stableId
            List.append messages [ nudgeMsg ], pressure, action
