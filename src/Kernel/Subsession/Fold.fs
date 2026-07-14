module Wanxiangshu.Kernel.Subsession.Fold

open Wanxiangshu.Kernel.Subsession.Types

// ── Session safety projection ──
// Tracks active runs AND persistently poisoned sessions.
// Historical runs (RunFinished) are kept in NDJSON only.

type ActiveRunProjection =
    { RunId: RunId
      ParentSessionId: SessionId }

type SessionSafetyEntry =
    | ActiveRun of ActiveRunProjection
    | PersistentlyPoisoned of PoisonReason

type SessionSafetyProjection = Map<SessionId, SessionSafetyEntry>

let emptyProjection: SessionSafetyProjection = Map.empty

/// Fold a single domain event into the session safety projection.
/// RunFinished removes only ActiveRun entries matching runId (never PersistentlyPoisoned).
/// SessionPoisoned replaces any existing entry with PersistentlyPoisoned.
let projectEvent (proj: SessionSafetyProjection) (evt: SubsessionEvent) : SessionSafetyProjection =
    match evt with
    | RunStarted data ->
        match Map.tryFind data.SessionId proj with
        | Some(PersistentlyPoisoned _) -> proj
        | _ ->
            Map.add
                data.SessionId
                (ActiveRun
                    { RunId = data.RunId
                      ParentSessionId = data.ParentSessionId })
                proj
    | RunFinished(runId, _) ->
        proj
        |> Map.filter (fun _ v ->
            match v with
            | ActiveRun r -> r.RunId <> runId
            | PersistentlyPoisoned _ -> true)
    | SessionPoisoned(sessionId, reason) -> Map.add sessionId (PersistentlyPoisoned reason) proj
    | PhysicalSessionClosed sessionId -> Map.remove sessionId proj
    | TurnDispatchRequested _
    | TurnStarted _
    | TurnFinished _
    | AbortRequested _ -> proj

/// Fold a list of events into an initial projection.
let projectEvents (events: SubsessionEvent list) : SessionSafetyProjection =
    List.fold projectEvent emptyProjection events
