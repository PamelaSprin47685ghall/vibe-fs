module Wanxiangshu.Kernel.SessionControl.LeaseIdentity

/// Lease identity derivation and pure validation helpers.
/// Zero state mutation — given inputs, return a value or boolean.

open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event

// ── Identity derivation ──────────────────────────────────────────────────────

let deriveHumanTurnId (fallback: HumanTurnState option) (explicitId: string option) : string =
    explicitId |> Option.orElse (fallback |> Option.map (fun t -> t.TurnId)) |> Option.defaultValue ""

let defaultOrdinal (current: int) (opt: int option) : int = opt |> Option.defaultValue (current + 1)
let defaultGeneration (sessionGen: int) (opt: int option) : int = opt |> Option.defaultValue sessionGen
let defaultCancelGeneration (cancelGen: int) (opt: int option) : int = opt |> Option.defaultValue cancelGen

// ── Stage validation ─────────────────────────────────────────────────────────

type LeaseGuard = OrdinalStale | StageMismatch | Ok

let guardContinuationStage (ordinal: int) (expectedStage: EpisodeStage) (currentOrdinal: int) (currentStage: EpisodeStage) : LeaseGuard =
    if ordinal <> currentOrdinal then OrdinalStale elif currentStage <> expectedStage then StageMismatch else Ok

let guardNudgeStage (ordinal: int) (expectedStage: EpisodeStage) (currentOrdinal: int) (currentStage: EpisodeStage) : LeaseGuard =
    if ordinal <> currentOrdinal then OrdinalStale elif currentStage <> expectedStage then StageMismatch else Ok

let guardCompactionMatch (compactionId: string) (ordinalOpt: int option) (activeCompaction: ReplayCompactionState option) : bool =
    compactionId = "" || ordinalOpt.IsNone || activeCompaction |> Option.exists (fun c -> c.CompactionID = compactionId && c.CompactionOrdinal = ordinalOpt.Value)

// ── Lease field selector ──────────────────────────────────────────────────────

type LeaseField = ContinuationField | NudgeField

let private ordinalOf = function ContinuationField -> (fun (l: OwnerEpisodeState) -> l.ContinuationOrdinal) | NudgeField -> (fun (l: OwnerEpisodeState) -> l.NudgeOrdinal)
let private stageOf = function ContinuationField -> (fun (l: OwnerEpisodeState) -> l.ContinuationStage) | NudgeField -> (fun (l: OwnerEpisodeState) -> l.NudgeStage)
let private leaseIdOf = function ContinuationField -> (fun (l: OwnerEpisodeState) -> l.ContinuationLease) | NudgeField -> (fun (l: OwnerEpisodeState) -> l.NudgeLease)
let private idField = function ContinuationField -> (fun (l: ReplayLeaseState) -> l.ContinuationID) | NudgeField -> (fun (l: ReplayLeaseState) -> l.NudgeID)

let private setLeaseStatus (field: LeaseField) (status: string) (lease: ReplayLeaseState) : ReplayLeaseState =
    { lease with Status = status }

let private applyLeaseInState (field: LeaseField) (newLease: ReplayLeaseState) (nextStage: EpisodeStage) (st: OwnerEpisodeState) : OwnerEpisodeState =
    match field with ContinuationField -> { st with ContinuationLease = Some newLease; ContinuationStage = nextStage } | NudgeField -> { st with NudgeLease = Some newLease; NudgeStage = nextStage }

// ── Generic helpers ───────────────────────────────────────────────────────────

let advanceLease (field: LeaseField) (status: string) (nextStage: EpisodeStage) (st: OwnerEpisodeState) (ev: EpisodeStageEvent) : OwnerEpisodeState =
    let eventOrdinal = defaultOrdinal (ordinalOf field st) ev.Ordinal
    let currentOrdinal = ordinalOf field st
    let currentStage = stageOf field st
    match guardContinuationStage eventOrdinal nextStage currentOrdinal currentStage with
    | Ok ->
        match leaseIdOf field st with Some l when idField field l = ev.Id -> st |> applyLeaseInState field (setLeaseStatus field status l) nextStage | _ -> st
    | _ -> st

let requestLease (field: LeaseField) (owner: string) (st: OwnerEpisodeState) (evOrdinal: int option) (evHumanTurnId: string option) (evSessionGen: int option) (evCancelGen: int option) : OwnerEpisodeState =
    let currentOrdinal = ordinalOf field st
    let newOrdinal = defaultOrdinal currentOrdinal evOrdinal
    if newOrdinal <= currentOrdinal then st
    else
        let nextLease = { Status = "requested"; HumanTurnID = deriveHumanTurnId st.HumanTurn evHumanTurnId; SessionGeneration = defaultGeneration st.SessionGeneration evSessionGen; CancelGeneration = defaultCancelGeneration st.CancelGeneration evCancelGen }
        st |> applyLeaseInState field nextLease Requested |> (fun s -> { s with Owner = Some owner })

let releaseOwnerIf (agent: string) (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with Owner = if st.Owner = Some agent then Some "None" else st.Owner }

// ── Reusable field resets ─────────────────────────────────────────────────────

let userAbortState (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with Owner = Some "None"; ContinuationLease = None; ContinuationStage = NoEpisode; NudgeLease = None; NudgeStage = NoEpisode; Compaction = None; CompactionStage = NoEpisode; IsCompacted = false; CompactionGeneration = 0 }

let nudgeDedupState (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with Owner = Some "None"; NudgeLease = None; NudgeStage = NoEpisode }

let continuationTerminal (evId: string) (st: OwnerEpisodeState) : OwnerEpisodeState =
    let hasId = st.ContinuationLease |> Option.exists (fun l -> l.ContinuationID = evId)
    { st with ContinuationLease = None; ContinuationStage = Terminal; Owner = if hasId then releaseOwnerIf "Fallback" st |> (fun s -> s.Owner) else st.Owner }

let nudgeTerminal (evId: string) (st: OwnerEpisodeState) : OwnerEpisodeState =
    let hasId = st.NudgeLease |> Option.exists (fun nl -> nl.NudgeID = evId)
    { st with NudgeLease = None; NudgeStage = Terminal; Owner = if hasId then releaseOwnerIf "Nudge" st |> (fun s -> s.Owner) else st.Owner }
