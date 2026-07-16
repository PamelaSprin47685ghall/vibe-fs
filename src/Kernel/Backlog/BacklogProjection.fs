module Wanxiangshu.Kernel.Backlog.BacklogProjection

/// Independent projection for todo backlog state.
///
/// Owner: Backlog subsystem
/// Input events: work_backlog_committed
/// Query: LatestEntry, TodosJson, BacklogEntry list

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Backlog.BacklogTypes

type WorkBacklogSnapshot =
    { TodosJson: string option
      LatestEntry: BacklogEntry option }

let emptySnapshot: WorkBacklogSnapshot = { TodosJson = None; LatestEntry = None }

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

/// Build a BacklogEntry from event payload fields.
let backlogEntryFromPayload (payload: Map<string, string>) : BacklogEntry option =
    match
        Map.tryFind "ahaMoments" payload,
        Map.tryFind "changesAndReasons" payload,
        Map.tryFind "gotchas" payload,
        Map.tryFind "lessonsAndConventions" payload,
        Map.tryFind "plan" payload
    with
    | Some aha, Some car, Some got, Some les, Some pl ->
        Some
            { ahaMoments = aha
              changesAndReasons = car
              gotchas = got
              lessonsAndConventions = les
              plan = pl }
    | _ -> None

let private snapshotFolder (snap: WorkBacklogSnapshot) (e: WanEvent) : WorkBacklogSnapshot =
    if e.Kind <> eventKindWorkBacklogCommitted then
        snap
    else
        let entryOpt = backlogEntryFromPayload e.Payload

        { TodosJson = payloadField "todosJson" e |> Option.orElse snap.TodosJson
          LatestEntry = entryOpt |> Option.orElse snap.LatestEntry }

/// Fold a single event into a WorkBacklogSnapshot (public for compose projection).
let foldSingleEvent (snap: WorkBacklogSnapshot) (e: WanEvent) : WorkBacklogSnapshot = snapshotFolder snap e

/// Fold a full event stream into a WorkBacklogSnapshot.
let foldBacklogStream (sessionId: string) (events: WanEvent list) : WorkBacklogSnapshot =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold snapshotFolder emptySnapshot
