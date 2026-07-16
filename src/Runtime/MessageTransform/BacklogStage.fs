module Wanxiangshu.Runtime.MessageTransform.BacklogStage

open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Stack
open Wanxiangshu.Runtime.MessageTransform.ToolCallIntegrity

let applyBacklogSlot
    (state: TransformState)
    (eventCount: int)
    (segmentLength: int)
    (encoded: obj array)
    : obj array * TransformState =
    if state.Backlog.EventCount = eventCount && state.Backlog.Segment.Length > 0 then
        let segmentLength = min state.Backlog.Segment.Length encoded.Length
        Array.blit state.Backlog.Segment 0 encoded 0 segmentLength
        encoded, state
    else
        let length = min segmentLength encoded.Length
        let segment = if length = 0 then [||] else encoded.[.. length - 1]
        let backlogRevision = state.BacklogRevision

        encoded,
        { state with
            Backlog =
                { EventCount = eventCount
                  Segment = segment
                  BacklogRevision = backlogRevision } }

let computeTopSlotKey
    (plan: MessageTransformPlan)
    (afterBacklog: Message<obj> list)
    (afterBudget: Message<obj> list)
    (afterPrompt: Message<obj> list)
    : TopSlotKey =
    if afterBudget.Length > afterBacklog.Length then
        let storeEntry =
            Wanxiangshu.Runtime.ContextBudgetStore.get plan.Scope plan.SessionID

        let episodeId = storeEntry.EpisodeID
        let syntheticId = storeEntry.StableSyntheticNudgeID |> Option.defaultValue ""
        let contentVersion = storeEntry.NudgeCount
        BudgetNudgeTop(episodeId, syntheticId, contentVersion)
    elif afterPrompt.Length > afterBacklog.Length then
        let nativeMsgs = afterBudget |> List.filter (fun m -> m.source = Native)

        let lastAssistantIdxOpt =
            nativeMsgs |> List.tryFindIndexBack (fun m -> m.info.role = Assistant)

        match lastAssistantIdxOpt with
        | Some lastIdx ->
            let lastAssistantMsg = nativeMsgs.[lastIdx]
            let realCallIDs = getRealCallIds lastAssistantMsg

            match realCallIDs with
            | targetCallID :: _ -> ParallelHintTop(targetCallID, lastAssistantMsg.info.id, 1)
            | [] -> NoTop
        | None -> NoTop
    else
        NoTop

let applyTopSlot (state: TransformState) (key: TopSlotKey) (encoded: obj array) : obj array * TransformState =
    match key, state.Top with
    | NoTop, _ ->
        let budgetRevision = state.BudgetRevision

        encoded,
        { state with
            Top =
                { Key = NoTop
                  Item = None
                  BudgetRevision = budgetRevision } }
    | _, { Key = cachedKey; Item = Some item } when cachedKey = key ->
        encoded.[encoded.Length - 1] <- item
        encoded, state
    | _ ->
        let budgetRevision = state.BudgetRevision

        encoded,
        { state with
            Top =
                { Key = key
                  Item = Some encoded.[encoded.Length - 1]
                  BudgetRevision = budgetRevision } }
