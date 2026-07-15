module Wanxiangshu.Shell.MessageTransformStack

open Wanxiangshu.Shell.RuntimeScope

/// The mutually exclusive trailing synthetic message.
type TopSlotKind =
    | NoTop
    | BudgetNudgeTop
    | ParallelHintTop

/// Synthetic sections owned by the transform hook for one session. Host
/// messages are deliberately absent: they are reconstructed from the host DB
/// on every run and must never be treated as hook state.
type BacklogSlot = { EventCount: int; Segment: obj array }

type TopSlot = { Kind: TopSlotKind; Item: obj option }

type TransformState =
    { Caps: obj array option
      Backlog: BacklogSlot
      Top: TopSlot }

let private stateKey (sessionID: string) = "message_transform_state_" + sessionID

let private emptyState =
    { Caps = None
      Backlog = { EventCount = -1; Segment = [||] }
      Top = { Kind = NoTop; Item = None } }

let get (scope: RuntimeScope) (sessionID: string) : TransformState =
    match scope.TryFindKey(stateKey sessionID) with
    | Some value -> unbox<TransformState> value
    | None -> emptyState

let set (scope: RuntimeScope) (sessionID: string) (state: TransformState) : unit =
    scope.Add(stateKey sessionID, box state)

let getCapsSlot (scope: RuntimeScope) (sessionID: string) = (get scope sessionID).Caps

let getBacklogSlot (scope: RuntimeScope) (sessionID: string) =
    let slot = (get scope sessionID).Backlog

    if slot.EventCount < 0 || slot.Segment.Length = 0 then
        None
    else
        Some slot

let getTopSlot (scope: RuntimeScope) (sessionID: string) =
    let slot = (get scope sessionID).Top
    if slot.Kind = NoTop then None else Some slot
