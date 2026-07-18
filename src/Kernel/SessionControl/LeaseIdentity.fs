module Wanxiangshu.Kernel.SessionControl.LeaseIdentity

/// Lease identity derivation and pure validation helpers.
/// Zero state mutation — given inputs, return a value or boolean.

open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope

// ── LeaseUnion ────────────────────────────────────────────────────────────────

type LeaseUnion =
    | Continuation of ReplayLeaseState
    | Nudge of ReplayNudgeLeaseState

// ── Identity derivation ──────────────────────────────────────────────────────

let deriveHumanTurnId (fallback: HumanTurnState option) (explicitId: string option) : string =
    explicitId
    |> Option.orElse (fallback |> Option.map (fun t -> t.TurnId))
    |> Option.defaultValue ""

let defaultOrdinal (current: int) (opt: int option) : int =
    opt |> Option.defaultValue (current + 1)

let defaultGeneration (sessionGen: int) (opt: int option) : int = opt |> Option.defaultValue sessionGen
let defaultCancelGeneration (cancelGen: int) (opt: int option) : int = opt |> Option.defaultValue cancelGen

// ── Stage validation ─────────────────────────────────────────────────────────

type LeaseGuard =
    | OrdinalStale
    | StageMismatch
    | Ok

let private isValidStageTransition (expectedStage: EpisodeStage) (currentStage: EpisodeStage) : bool =
    if expectedStage = currentStage then
        true
    else
        match expectedStage, currentStage with
        | DispatchStarted, Requested -> true
        | Dispatched, Requested -> true
        | Dispatched, DispatchStarted -> true
        | _ -> false

let guardContinuationStage
    (ordinal: int)
    (expectedStage: EpisodeStage)
    (currentOrdinal: int)
    (currentStage: EpisodeStage)
    : LeaseGuard =
    if ordinal <> currentOrdinal then
        OrdinalStale
    elif isValidStageTransition expectedStage currentStage then
        Ok
    else
        StageMismatch

let guardNudgeStage
    (ordinal: int)
    (expectedStage: EpisodeStage)
    (currentOrdinal: int)
    (currentStage: EpisodeStage)
    : LeaseGuard =
    if ordinal <> currentOrdinal then
        OrdinalStale
    elif isValidStageTransition expectedStage currentStage then
        Ok
    else
        StageMismatch

let guardCompactionMatch
    (compactionId: string)
    (ordinalOpt: int option)
    (activeCompaction: ReplayCompactionState option)
    : bool =
    compactionId = ""
    || ordinalOpt.IsNone
    || activeCompaction
       |> Option.exists (fun c -> c.CompactionID = compactionId && c.CompactionOrdinal = ordinalOpt.Value)

// ── Lease field selector ──────────────────────────────────────────────────────

type LeaseField =
    | ContinuationField
    | NudgeField

let internal ordinalOf =
    function
    | ContinuationField -> (fun (l: OwnerEpisodeState) -> l.ContinuationOrdinal)
    | NudgeField -> (fun (l: OwnerEpisodeState) -> l.NudgeOrdinal)

let internal stageOf =
    function
    | ContinuationField -> (fun (l: OwnerEpisodeState) -> l.ContinuationStage)
    | NudgeField -> (fun (l: OwnerEpisodeState) -> l.NudgeStage)

let internal leaseIdOf (field: LeaseField) (st: OwnerEpisodeState) : LeaseUnion option =
    match field with
    | ContinuationField -> st.ContinuationLease |> Option.map Continuation
    | NudgeField -> st.NudgeLease |> Option.map Nudge

let internal idOfLeaseUnion =
    function
    | Continuation l -> l.ContinuationID
    | Nudge l -> l.NudgeID

let internal setLeaseStatus (field: LeaseField) (status: string) (lease: LeaseUnion) : LeaseUnion =
    match field, lease with
    | ContinuationField, Continuation l -> Continuation { l with Status = status }
    | NudgeField, Nudge l -> Nudge { l with Status = status }
    | _ -> lease

let internal applyLeaseInState
    (field: LeaseField)
    (newLease: LeaseUnion)
    (nextStage: EpisodeStage)
    (st: OwnerEpisodeState)
    : OwnerEpisodeState =
    match field, newLease with
    | ContinuationField, Continuation l ->
        { st with
            ContinuationLease = Some l
            ContinuationStage = nextStage }
    | NudgeField, Nudge l ->
        { st with
            NudgeLease = Some l
            NudgeStage = nextStage }
    | _ -> st
