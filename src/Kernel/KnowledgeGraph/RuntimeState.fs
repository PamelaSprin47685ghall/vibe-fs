module Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState

open System
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types

type BookkeeperLaunch =
    { agent: string
      title: string
      prompt: string
      result: string }

/// Single-responsibility immutable aggregate of all knowledge graph host state (P52/P53).
type KnowledgeGraphState =
    { sessionSnapshots: Map<string, KnowledgeGraphProjection>
      bookkeeperLaunches: BookkeeperLaunch list
      scheduledMaintenance: Set<string> }

let emptyState : KnowledgeGraphState =
    { sessionSnapshots = Map.empty
      bookkeeperLaunches = []
      scheduledMaintenance = Set.empty }

let initialKnowledgeGraphState : KnowledgeGraphState = emptyState

let cacheSnapshot (state: KnowledgeGraphState) (sessionID: string) (projection: KnowledgeGraphProjection) : KnowledgeGraphState =
    { state with sessionSnapshots = Map.add sessionID projection state.sessionSnapshots }

let appendLaunch (state: KnowledgeGraphState) (launch: BookkeeperLaunch) : KnowledgeGraphState =
    { state with bookkeeperLaunches = launch :: state.bookkeeperLaunches }

let recordLaunch (state: KnowledgeGraphState) (launch: BookkeeperLaunch) : KnowledgeGraphState =
    appendLaunch state launch

let updateLatestLaunchResult (state: KnowledgeGraphState) (title: string) (result: string) : KnowledgeGraphState =
    let rec loop (rev: BookkeeperLaunch list) (remaining: BookkeeperLaunch list) : BookkeeperLaunch list =
        match remaining with
        | [] -> List.rev rev
        | launch :: rest ->
            if List.exists (fun candidate -> candidate.title = title) (launch :: rest) then
                loop (launch :: rev) rest
            else if launch.title = title then
                List.rev ({ launch with result = result } :: rev) @ rest
            else
                List.rev rev @ remaining
    { state with bookkeeperLaunches = loop [] state.bookkeeperLaunches }

let recordLaunchOnce (state: KnowledgeGraphState) (key: string) (launch: BookkeeperLaunch) : bool * KnowledgeGraphState =
    if Set.contains key state.scheduledMaintenance then false, state
    else true, { appendLaunch state launch with scheduledMaintenance = Set.add key state.scheduledMaintenance }

let drainLaunches (state: KnowledgeGraphState) : BookkeeperLaunch list * KnowledgeGraphState =
    state.bookkeeperLaunches, { state with bookkeeperLaunches = [] }

let normalizeDraftIds (projection: KnowledgeGraphProjection) (drafts: KnowledgeGraphDraft list) : KnowledgeGraphDraft list =
    drafts
    |> List.map (fun draft ->
        match draft.id |> Option.bind tryParseId with
        | Some knowledgeGraphId when Map.containsKey knowledgeGraphId projection -> draft
        | _ -> { draft with id = None })

type KnowledgeGraphCommand =
    | CacheSnapshotCmd of sessionID: string * projection: KnowledgeGraphProjection
    | RecordLaunchCmd of launch: BookkeeperLaunch
    | UpdateLatestLaunchResultCmd of title: string * result: string
    | RecordLaunchOnceCmd of key: string * launch: BookkeeperLaunch
    | DrainLaunchesCmd

let reducer (state: KnowledgeGraphState) (cmd: KnowledgeGraphCommand) : KnowledgeGraphState =
    match cmd with
    | CacheSnapshotCmd (sessionID, projection) -> cacheSnapshot state sessionID projection
    | RecordLaunchCmd launch -> recordLaunch state launch
    | UpdateLatestLaunchResultCmd (title, result) -> updateLatestLaunchResult state title result
    | RecordLaunchOnceCmd (key, launch) -> recordLaunchOnce state key launch |> snd
    | DrainLaunchesCmd -> drainLaunches state |> snd
