module Wanxiangshu.Kernel.ResourcePlan

open Wanxiangshu.Kernel.Subsession.Types

// ── Resource identity (stable across restarts) ──

type ResourceId =
    | TurnDeadlineId of turnId: string
    | AbortDeadlineId of turnId: string
    | ReconciliationDeadlineId of turnId: string

// ── Absolute deadline (persisted, not relative duration) ──

/// Absolute wall-clock time in milliseconds since epoch.
/// On restart, the ResourceScope computes `remaining = DeadlineAt - Clock.Now`
/// and arms a timer for the remaining duration (or fires immediately if expired).
type AbsoluteDeadline = { DeadlineAtMs: int64 }

// ── Resource spec (what SHOULD exist after a state commit) ──

type ResourceSpec =
    | TurnDeadline of ResourceId * AbsoluteDeadline
    | AbortDeadline of ResourceId * AbsoluteDeadline
    | ReconciliationDeadline of ResourceId * AbsoluteDeadline

// ── Pure projection: SubsessionState → ResourceSpec list ──

let private activeTurnId (turn: ActiveTurn) : TurnId =
    match turn with
    | NotYetStarted p -> p.TurnId
    | Started s -> s.Plan.TurnId

/// Derive the set of resources that MUST exist after committing to `state`.
/// Called after every decision commit in the actor.
/// `nowMs` MUST be injected so tests can be deterministic.
let projectResources (nowMs: int64) (state: SubsessionState) : ResourceSpec list =
    match state with
    | Available _ ->
        // No active turn — no deadlines.
        []

    | Poisoned _ ->
        // Poisoned — no deadlines; all timers should be disposed.
        []

    | Dispatching(_, plan, _)
    | CancellingDispatch(_, plan, _) ->
        let id = TurnDeadlineId(TurnId.value plan.TurnId)
        [ TurnDeadline(id, { DeadlineAtMs = nowMs + 300_000L }) ]

    | ReconcilingUnknownDispatch(_, plan, _, _)
    | ClosingUnknownDispatch(_, plan, _) ->
        // Both turn deadline AND reconciliation deadline are active
        let turnId = TurnDeadlineId(TurnId.value plan.TurnId)
        let reconId = ReconciliationDeadlineId(TurnId.value plan.TurnId)

        [ TurnDeadline(turnId, { DeadlineAtMs = nowMs + 300_000L })
          ReconciliationDeadline(reconId, { DeadlineAtMs = nowMs + 30_000L }) ]

    | Running(_, started, _)
    | Draining(_, started, _, _) ->
        let id = TurnDeadlineId(TurnId.value started.Plan.TurnId)
        [ TurnDeadline(id, { DeadlineAtMs = nowMs + 300_000L }) ]

    | IssuingAbort(_, turn, _, _)
    | AwaitingAbortSettle(_, turn, _)
    | ReconcilingAbortSettle(_, turn, _) ->
        let tid = activeTurnId turn
        let abortId = AbortDeadlineId(TurnId.value tid)
        [ AbortDeadline(abortId, { DeadlineAtMs = nowMs + 60_000L }) ]

// ── Diff computation ──

/// Compute resources to acquire vs release given old and new specs.
/// Resources present in `next` but absent from `prev` → acquire (arm timer).
/// Resources present in `prev` but absent from `next` → release (clear timer).
/// Resources present in both with same Identity → leave unchanged (keep timer).
type ResourceDiff =
    { ToAcquire: ResourceSpec list
      ToRelease: ResourceId list }

let diffResources (prev: ResourceSpec list) (next: ResourceSpec list) : ResourceDiff =
    let prevIds =
        prev
        |> List.map (fun spec ->
            spec
            |> function
                | TurnDeadline(id, _) -> id
                | AbortDeadline(id, _) -> id
                | ReconciliationDeadline(id, _) -> id)
        |> Set.ofList

    let nextIds =
        next
        |> List.map (fun spec ->
            spec
            |> function
                | TurnDeadline(id, _) -> id
                | AbortDeadline(id, _) -> id
                | ReconciliationDeadline(id, _) -> id)
        |> Set.ofList

    let toRelease = Set.difference prevIds nextIds |> Set.toList
    let toAcquire = Set.difference nextIds prevIds |> Set.toList

    let acquireSpecs =
        next
        |> List.filter (fun spec ->
            let id =
                match spec with
                | TurnDeadline(id, _) -> id
                | AbortDeadline(id, _) -> id
                | ReconciliationDeadline(id, _) -> id

            Set.contains id (Set.ofList toAcquire))

    { ToAcquire = acquireSpecs
      ToRelease = toRelease }
