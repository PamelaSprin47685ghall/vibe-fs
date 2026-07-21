module Wanxiangshu.Kernel.Nudge.NudgeProjection

/// Independent projection for nudge state (dedup + snapshot).
///
/// Owner: Nudge subsystem
/// Input events: nudge_*, assistant_completed,
///               human_turn_started, submit_review_wip_recorded, loop_*
/// Query: IsBlocked, CurrentSnapshot

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotProjection

// Re-export snapshot API so existing callers continue to open this module.
type NudgeSnapshotState = NudgeSnapshotProjection.NudgeSnapshotState

let emptySnapshotState: NudgeSnapshotState =
    NudgeSnapshotProjection.emptySnapshotState

let foldSingleSnapshotEvent = NudgeSnapshotProjection.foldSingleSnapshotEvent
let foldSnapshotStream = NudgeSnapshotProjection.foldSnapshotStream

let payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let payloadAnchor (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "anchor"
    |> Option.bind (fun t -> if t.Trim() = "" then None else Some(t.Trim()))

type NudgeDedupState =
    { PendingNudge: (string * string) option
      LastDispatchedAnchor: string option }

let emptyDedupState: NudgeDedupState =
    { PendingNudge = None
      LastDispatchedAnchor = None }

let dedupFolder (st: NudgeDedupState) (e: WanEvent) : NudgeDedupState =
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

let foldSingleDedupEvent (st: NudgeDedupState) (e: WanEvent) : NudgeDedupState = dedupFolder st e

let foldDedupStream (sessionId: string) (events: WanEvent list) : NudgeDedupState =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold dedupFolder emptyDedupState

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
