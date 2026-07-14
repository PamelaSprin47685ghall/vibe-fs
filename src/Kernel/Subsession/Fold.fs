module Wanxiangshu.Kernel.Subsession.Fold

open Wanxiangshu.Kernel.Subsession.Types

// ── Active-run projection ──
// Tracks only currently-active runs; historical runs are kept in NDJSON only.

type ActiveRunProjection =
    { RunId: RunId
      ParentSessionId: SessionId }

type ActiveSubsessionProjection = Map<SessionId, ActiveRunProjection>

let emptyProjection: ActiveSubsessionProjection = Map.empty

/// Fold a single domain event into the active-run projection.
/// RunFinished removes the entry → memory is O(active child sessions).
let projectEvent (proj: ActiveSubsessionProjection) (evt: SubsessionEvent) : ActiveSubsessionProjection =
    match evt with
    | RunStarted data ->
        Map.add
            data.SessionId
            { RunId = data.RunId
              ParentSessionId = data.ParentSessionId }
            proj
    | RunFinished(runId, _) -> proj |> Map.filter (fun _ v -> v.RunId <> runId)
    | SessionPoisoned(sessionId, _) -> Map.remove sessionId proj
    | _ -> proj

/// Fold a list of events into an initial projection.
let projectEvents (events: SubsessionEvent list) : ActiveSubsessionProjection =
    List.fold projectEvent emptyProjection events
