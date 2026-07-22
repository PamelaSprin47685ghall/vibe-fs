module Wanxiangshu.Kernel.Nudge.NudgeSnapshotProjection

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let private strOrEmpty (o: string option) : string =
    match o with
    | Some s -> s
    | None -> ""

type NudgeSnapshotState =
    { openTodos: string list
      lastAssistantText: string
      skipTodo: bool
      skipReview: bool
      agentFromMessage: string option
      modelFromMessage: string option
      turnId: string
      reviewLoop: ReviewLoopFold
      workState: SessionWorkState
      pendingNudge: (string * string) option
      lastDispatchedAnchor: string option }

let private syncWorkState (st: NudgeSnapshotState) : NudgeSnapshotState =
    let isActive = st.reviewLoop |> ReviewLoopFold.isLoopActive
    let hasTodos = not (List.isEmpty st.openTodos)

    { st with
        workState =
            if not isActive && not hasTodos then
                SessionWorkState.Idle
            elif not isActive && hasTodos then
                SessionWorkState.TodosOnly
            elif isActive && not hasTodos then
                SessionWorkState.LoopIdle
            else
                SessionWorkState.LoopWithTodos }

let emptySnapshotState: NudgeSnapshotState =
    { openTodos = []
      lastAssistantText = ""
      skipTodo = false
      skipReview = false
      agentFromMessage = None
      modelFromMessage = None
      turnId = ""
      reviewLoop = ReviewLoopFold.initial
      workState = SessionWorkState.Idle
      pendingNudge = None
      lastDispatchedAnchor = None }

let private snapshotFromAssistantCompleted (st: NudgeSnapshotState) (e: WanEvent) : NudgeSnapshotState =
    let msg = payloadField "assistantMessage" e |> strOrEmpty

    let agent =
        payloadField "agent" e |> Option.bind (fun a -> if a = "" then None else Some a)

    let model =
        payloadField "model" e |> Option.bind (fun m -> if m = "" then None else Some m)

    let tid = payloadField "turnId" e |> strOrEmpty
    let todosFromPayload = payloadField "openTodosJson" e |> Option.map decodeOpenTodosJson
    let openTodos = todosFromPayload |> Option.defaultValue st.openTodos

    syncWorkState
        { st with
            lastAssistantText = msg
            skipTodo = payloadBool "skipTodo" e.Payload
            skipReview = payloadBool "skipReview" e.Payload
            agentFromMessage = agent
            modelFromMessage = model
            turnId = tid
            openTodos = openTodos }

let private snapshotFromNudgeEvent (st: NudgeSnapshotState) (e: WanEvent) : NudgeSnapshotState =
    match e.Kind with
    | k when k = eventKindNudgeRequested ->
        match payloadField "anchor" e, payloadField "nudgeId" e with
        | Some anchor, Some nid ->
            { st with
                pendingNudge = Some(anchor, nid) }
        | _ -> st
    | k when k = eventKindNudgeDispatched ->
        match payloadField "anchor" e with
        | Some anchor ->
            { st with
                lastDispatchedAnchor = Some anchor
                pendingNudge = None }
        | None -> st
    | k when k = eventKindNudgeFailed || k = eventKindNudgeCancelled ->
        match payloadField "nudgeId" e with
        | Some nid ->
            match st.pendingNudge with
            | Some(_, pendingNid) when pendingNid = nid -> { st with pendingNudge = None }
            | _ -> st
        | None -> st
    | _ -> st

let private snapshotFromWorkEvent (st: NudgeSnapshotState) (e: WanEvent) : NudgeSnapshotState =
    let todosOpt = payloadField "todosJson" e |> Option.map decodeOpenTodosJson

    syncWorkState
        { st with
            openTodos = todosOpt |> Option.defaultValue st.openTodos }

let private snapshotFolder (st: NudgeSnapshotState) (e: WanEvent) : NudgeSnapshotState =
    match e.Kind with
    | k when k = eventKindAssistantCompleted -> snapshotFromAssistantCompleted st e
    | k when
        k = eventKindLoopActivated
        || k = eventKindLoopCancelled
        || k = eventKindReviewVerdict
        ->
        let reviewLoop = ReviewLoopFold.foldEvent st.reviewLoop e
        syncWorkState { st with reviewLoop = reviewLoop }
    | k when
        k = eventKindNudgeRequested
        || k = eventKindNudgeDispatched
        || k = eventKindNudgeFailed
        || k = eventKindNudgeCancelled
        ->
        snapshotFromNudgeEvent st e
    | k when
        k = eventKindSubmitReviewWipRecorded
        || k = eventKindNudgeDedupCleared
        || k = eventKindHumanTurnStarted
        || k = eventKindNudgeSettled
        ->
        { st with
            lastDispatchedAnchor = None
            pendingNudge = None }
    | _ -> st

let foldSingleSnapshotEvent (st: NudgeSnapshotState) (e: WanEvent) : NudgeSnapshotState = snapshotFolder st e

let foldSnapshotStream (sessionId: string) (events: WanEvent list) : NudgeSnapshotState =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold snapshotFolder emptySnapshotState
