module Wanxiangshu.Kernel.Nudge.NudgeProjection

/// Independent projection for nudge state (dedup + snapshot).
///
/// Owner: Nudge subsystem
/// Input events: nudge_*, assistant_completed, work_backlog_committed,
///               human_turn_started, submit_review_wip_recorded, loop_*
/// Query: IsBlocked, CurrentSnapshot

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.Nudge.Types

// ── NudgeDedupState ──

type NudgeDedupState =
    { PendingNudge: (string * string) option
      LastDispatchedAnchor: string option }

let emptyDedupState: NudgeDedupState =
    { PendingNudge = None
      LastDispatchedAnchor = None }

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let private payloadAnchor (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "anchor"
    |> Option.bind (fun t -> if t.Trim() = "" then None else Some(t.Trim()))

let private dedupFolder (st: NudgeDedupState) (e: WanEvent) : NudgeDedupState =
    match e.Kind with
    | k when k = eventKindNudgeRequested ->
        match payloadAnchor e, payloadField "nudgeId" e with
        | Some anchor, Some nid ->
            { st with
                PendingNudge = Some(anchor, nid) }
        | _ -> st
    | k when k = eventKindNudgeDispatched ->
        match payloadAnchor e with
        | Some anchor ->
            { st with
                LastDispatchedAnchor = Some anchor
                PendingNudge = None }
        | None -> st
    | k when k = eventKindNudgeFailed || k = eventKindNudgeCancelled ->
        match payloadField "nudgeId" e with
        | Some nid ->
            match st.PendingNudge with
            | Some(_, pendingNid) when pendingNid = nid -> { st with PendingNudge = None }
            | _ -> st
        | None -> st
    | k when
        k = eventKindSubmitReviewWipRecorded
        || k = eventKindNudgeDedupCleared
        || k = eventKindHumanTurnStarted
        ->
        emptyDedupState
    | _ -> st

/// Fold a single dedup event (public for composite projection).
let foldSingleDedupEvent (st: NudgeDedupState) (e: WanEvent) : NudgeDedupState = dedupFolder st e

let foldDedupStream (sessionId: string) (events: WanEvent list) : NudgeDedupState =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold dedupFolder emptyDedupState

// ── NudgeSnapshotState ──

type NudgeSnapshotState =
    { openTodos: string list
      lastAssistantText: string
      agentFromMessage: string option
      modelFromMessage: string option
      turnId: string
      reviewLoop: ReviewLoopFold
      workState: SessionWorkState
      pendingNudge: (string * string) option
      lastDispatchedAnchor: string option }

let private parseTodosJson (json: string) : string list =
    let trimmed = json.Trim()

    if trimmed = "" || trimmed = "[]" then
        []
    elif trimmed.Length < 2 || trimmed.[0] <> '[' || trimmed.[trimmed.Length - 1] <> ']' then
        []
    else
        trimmed.Substring(1, trimmed.Length - 2).Split(',')
        |> Array.choose (fun segment ->
            let s = segment.Trim()

            if s.Length < 2 || s.[0] <> '"' || s.[s.Length - 1] <> '"' then
                None
            else
                Some(s.Substring(1, s.Length - 2)))
        |> Array.toList

let private syncWorkState (st: NudgeSnapshotState) : NudgeSnapshotState =
    let isActive = st.reviewLoop |> ReviewLoopFold.isLoopActive
    let hasTodos = not (List.isEmpty st.openTodos)

    { st with
        workState =
            if not isActive && not hasTodos then
                SessionWorkState.Idle
            elif not isActive && hasTodos then
                SessionWorkState.BacklogOnly
            elif isActive && not hasTodos then
                SessionWorkState.LoopIdle
            else
                SessionWorkState.LoopWithBacklog }

let emptySnapshotState: NudgeSnapshotState =
    { openTodos = []
      lastAssistantText = ""
      agentFromMessage = None
      modelFromMessage = None
      turnId = ""
      reviewLoop = ReviewLoopFold.initial
      workState = SessionWorkState.Idle
      pendingNudge = None
      lastDispatchedAnchor = None }

let private strOrEmpty (o: string option) : string =
    match o with
    | Some s -> s
    | None -> ""

// ARCHITECTURE_EXEMPT: split this 67-line function later
let private snapshotFolder (st: NudgeSnapshotState) (e: WanEvent) : NudgeSnapshotState =
    match e.Kind with
    | k when k = eventKindAssistantCompleted ->
        let msg = payloadField "assistantMessage" e |> strOrEmpty

        let agent =
            payloadField "agent" e |> Option.bind (fun a -> if a = "" then None else Some a)

        let model =
            payloadField "model" e |> Option.bind (fun m -> if m = "" then None else Some m)

        let tid = payloadField "turnId" e |> strOrEmpty
        let todosFromPayload = payloadField "openTodosJson" e |> Option.map parseTodosJson
        let openTodos = todosFromPayload |> Option.defaultValue st.openTodos

        syncWorkState
            { st with
                lastAssistantText = msg
                agentFromMessage = agent
                modelFromMessage = model
                turnId = tid
                openTodos = openTodos }
    | k when
        k = eventKindLoopActivated
        || k = eventKindLoopCancelled
        || k = eventKindReviewVerdict
        ->
        let reviewLoop = ReviewLoopFold.foldEvent st.reviewLoop e
        syncWorkState { st with reviewLoop = reviewLoop }
    | k when k = eventKindNudgeRequested ->
        match payloadAnchor e, payloadField "nudgeId" e with
        | Some anchor, Some nid ->
            { st with
                pendingNudge = Some(anchor, nid) }
        | _ -> st
    | k when k = eventKindNudgeDispatched ->
        match payloadAnchor e with
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
    | k when
        k = eventKindSubmitReviewWipRecorded
        || k = eventKindNudgeDedupCleared
        || k = eventKindHumanTurnStarted
        || k = eventKindNudgeSettled
        ->
        { st with
            lastDispatchedAnchor = None
            pendingNudge = None }
    | k when k = eventKindWorkBacklogCommitted ->
        let todosOpt = payloadField "todosJson" e |> Option.map parseTodosJson

        syncWorkState
            { st with
                openTodos = todosOpt |> Option.defaultValue st.openTodos }
    | _ -> st

/// Fold a single snapshot event (public for composite projection).
let foldSingleSnapshotEvent (st: NudgeSnapshotState) (e: WanEvent) : NudgeSnapshotState = snapshotFolder st e

let foldSnapshotStream (sessionId: string) (events: WanEvent list) : NudgeSnapshotState =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold snapshotFolder emptySnapshotState

// ── Helpers ──

let nudgeAnchorKey (turnId: string) (assistantMessage: string) : string =
    let body = assistantMessage.Trim()
    let tid = turnId.Trim()
    if tid = "" then body else tid + "\u001e" + body

let isBlocked (st: NudgeDedupState) (anchorKey: string) : bool =
    let trimmed = anchorKey.Trim()

    match st.PendingNudge with
    | Some(anchor, _) when anchor = trimmed -> true
    | _ ->
        match st.LastDispatchedAnchor with
        | Some anchor when anchor = trimmed -> true
        | _ -> false
