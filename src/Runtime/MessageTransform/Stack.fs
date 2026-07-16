module Wanxiangshu.Runtime.MessageTransform.Stack
open Wanxiangshu.Runtime

open Wanxiangshu.Runtime.RuntimeScope

/// Monotonic revision counter identifiers for independent content domains.
type RevisionId = int

[<RequireQualifiedAccess>]
module RevisionId =
    let initial = 0
    let next (r: RevisionId) = r + 1

/// The mutually exclusive trailing synthetic message.
type TopSlotKey =
    | NoTop
    | BudgetNudgeTop of episodeId: string * syntheticId: string * contentVersion: int
    | ParallelHintTop of callId: string * assistantMessageId: string * contentVersion: int

/// Synthetic sections owned by the transform hook for one session. Host
/// messages are deliberately absent: they are reconstructed from the host DB
/// on every run and must never be treated as hook state.
type BacklogSlot =
    { EventCount: int
      Segment: obj array
      BacklogRevision: int }

type CapsSlot =
    { Segment: obj array option
      ScopeId: string
      CapsRevision: int
      PolicyVersion: int }

type TopSlot =
    { Key: TopSlotKey
      Item: obj option
      BudgetRevision: int }

type TransformState =
    {
        Caps: CapsSlot option
        Backlog: BacklogSlot
        Top: TopSlot
        /// Monotonic revision for backlog content changes.
        BacklogRevision: int
        /// Monotonic revision for caps content changes.
        CapsRevision: int
        /// Monotonic revision for policy version changes.
        PolicyVersion: int
        /// Monotonic revision for budget/pressure content changes.
        BudgetRevision: int
    }

let private stateKey (sessionID: string) = "message_transform_state_" + sessionID

let private emptyState: TransformState =
    { Caps = None
      Backlog =
        { EventCount = -1
          Segment = [||]
          BacklogRevision = 0 }
      Top =
        { Key = NoTop
          Item = None
          BudgetRevision = 0 }
      BacklogRevision = 0
      CapsRevision = 0
      PolicyVersion = 0
      BudgetRevision = 0 }

let get (scope: RuntimeScope) (sessionID: string) : TransformState =
    match scope.TryFindKey(stateKey sessionID) with
    | Some value -> unbox<TransformState> value
    | None -> emptyState

let set (scope: RuntimeScope) (sessionID: string) (state: TransformState) : unit =
    scope.Add(stateKey sessionID, box state)

let getCapsSlot (scope: RuntimeScope) (sessionID: string) = (get scope sessionID).Caps

let getBacklogSlot (scope: RuntimeScope) (sessionID: string) =
    let state = get scope sessionID
    let slot = state.Backlog

    if slot.EventCount < 0 || slot.Segment.Length = 0 then
        None
    else
        Some slot

let getTopSlot (scope: RuntimeScope) (sessionID: string) =
    let slot = (get scope sessionID).Top

    match slot.Key with
    | NoTop -> None
    | _ -> Some slot

/// Constructor helpers to create revision-driven state entries.
module TransformState =

    let emptyState: TransformState =
        { Caps = None
          Backlog =
            { EventCount = -1
              Segment = [||]
              BacklogRevision = 0 }
          Top =
            { Key = NoTop
              Item = None
              BudgetRevision = 0 }
          BacklogRevision = 0
          CapsRevision = 0
          PolicyVersion = 0
          BudgetRevision = 0 }

    let updateCapsRevision (state: TransformState) (revision: int) : TransformState =
        { state with CapsRevision = revision }

    let updateBacklogRevision (state: TransformState) (revision: int) : TransformState =
        { state with
            BacklogRevision = revision }

    let updatePolicyVersion (state: TransformState) (version: int) : TransformState =
        { state with PolicyVersion = version }

    let updateBudgetRevision (state: TransformState) (revision: int) : TransformState =
        { state with BudgetRevision = revision }

    let capsKey (state: TransformState) : (string * int * int) option =
        match state.Caps with
        | Some c -> Some(c.ScopeId, c.CapsRevision, c.PolicyVersion)
        | None -> None

    let setCaps
        (state: TransformState)
        (scopeId: string)
        (capsRevision: int)
        (policyVersion: int)
        (slot: obj array option)
        : TransformState =
        { state with
            Caps =
                Some
                    { Segment = slot
                      ScopeId = scopeId
                      CapsRevision = capsRevision
                      PolicyVersion = policyVersion }
            CapsRevision = capsRevision
            PolicyVersion = policyVersion }

    let setBacklog
        (state: TransformState)
        (eventCount: int)
        (segment: obj array)
        (backlogRevision: int)
        : TransformState =
        { state with
            Backlog =
                { EventCount = eventCount
                  Segment = segment
                  BacklogRevision = backlogRevision }
            BacklogRevision = backlogRevision }

    let setTop (state: TransformState) (key: TopSlotKey) (item: obj option) (budgetRevision: int) : TransformState =
        { state with
            Top =
                { Key = key
                  Item = item
                  BudgetRevision = budgetRevision }
            BudgetRevision = budgetRevision }
