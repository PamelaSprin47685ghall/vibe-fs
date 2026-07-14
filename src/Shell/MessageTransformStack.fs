module Wanxiangshu.Shell.MessageTransformStack

open Wanxiangshu.Shell.RuntimeScope

/// CAPS: built once per session+conversation, never rebuilt for the same
/// conversation. Stores the already-built encoded caps prefix so we can
/// Array.append it without re-encoding. ConvKey distinguishes different
/// conversations that may share an empty sessionID.
type CapsSlot = { Prefix: obj array; ConvKey: string }

/// Backlog: tracks the folded-backlog count from the last turn and the
/// encoded segment (synthetic prefix messages + modified anchor) produced
/// at that count. When the count is unchanged the segment is reused as-is.
type BacklogSlot =
    { FoldedCount: int
      EncodedSegment: obj array }

/// Top-of-stack: the single trailing synthetic (nudge or parallel-hint).
/// Stores the encoded obj so it can be reused in the same episode without
/// re-encoding.
type TopSlot = { Item: obj option }

// ── CapsSlot ───────────────────────────────────────────────────────────────

let private capsKey (sessionID: string) = "transform_caps_" + sessionID

let getCapsSlot (scope: RuntimeScope) (sessionID: string) : CapsSlot option =
    match scope.TryFindKey(capsKey sessionID) with
    | Some o -> Some(unbox<CapsSlot> o)
    | None -> None

let setCapsSlot (scope: RuntimeScope) (sessionID: string) (slot: CapsSlot) : unit =
    scope.Add(capsKey sessionID, box slot)

// ── BacklogSlot ────────────────────────────────────────────────────────────

let private backlogKey (sessionID: string) = "transform_backlog_" + sessionID

let getBacklogSlot (scope: RuntimeScope) (sessionID: string) : BacklogSlot option =
    match scope.TryFindKey(backlogKey sessionID) with
    | Some o -> Some(unbox<BacklogSlot> o)
    | None -> None

let setBacklogSlot (scope: RuntimeScope) (sessionID: string) (slot: BacklogSlot) : unit =
    scope.Add(backlogKey sessionID, box slot)

// ── TopSlot ────────────────────────────────────────────────────────────────

let private topKey (sessionID: string) = "transform_top_" + sessionID

let getTopSlot (scope: RuntimeScope) (sessionID: string) : TopSlot option =
    match scope.TryFindKey(topKey sessionID) with
    | Some o -> Some(unbox<TopSlot> o)
    | None -> None

let setTopSlot (scope: RuntimeScope) (sessionID: string) (slot: TopSlot) : unit = scope.Add(topKey sessionID, box slot)

let clearTopSlot (scope: RuntimeScope) (sessionID: string) : unit = scope.Remove(topKey sessionID)
