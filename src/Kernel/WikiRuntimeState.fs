module VibeFs.Kernel.WikiRuntimeState

open System
open VibeFs.Kernel.Wiki

type BookkeeperLaunch =
    { agent: string
      title: string
      prompt: string
      result: string }

/// Single-responsibility immutable aggregate of all wiki host state (P52/P53).
/// Every field is updated only by the pure transition functions below; the IO
/// shell applies them through the `reducer`. `scheduledMaintenance` is a
/// process-level dedup set: a workspace+kind+value triple queues a background
/// rewrite at most once per cycle; keys accumulate for the lifetime of the process
/// and reset on restart, so a killed process retries accumulated work.
type WikiState =
    { sessionSnapshots: Map<string, WikiProjection>
      bookkeeperLaunches: BookkeeperLaunch list
      scheduledMaintenance: Set<string> }

let initialWikiState : WikiState =
    { sessionSnapshots = Map.empty
      bookkeeperLaunches = []
      scheduledMaintenance = Set.empty }

let private cacheSnapshot (state: WikiState) (sessionID: string) (projection: WikiProjection) : WikiState =
    { state with sessionSnapshots = Map.add sessionID projection state.sessionSnapshots }

let private recordLaunch (state: WikiState) (launch: BookkeeperLaunch) : WikiState =
    { state with bookkeeperLaunches = state.bookkeeperLaunches @ [ launch ] }

let private updateLatestLaunchResult (state: WikiState) (title: string) (result: string) : WikiState =
    let rec loop rev remaining =
        match remaining with
        | [] -> List.rev rev
        | launch :: rest ->
            let tail = List.rev rev
            if List.exists (fun candidate -> candidate.title = title) (launch :: rest) then
                loop (launch :: rev) rest
            else if launch.title = title then
                List.rev ({ launch with result = result } :: rev) @ rest
            else
                List.rev rev @ remaining
    { state with bookkeeperLaunches = loop [] state.bookkeeperLaunches }

/// Dedup by workspace+kind+value triple: returns whether this is the first
/// launch for the triple (caller queues the job only then) and the next state.
let recordLaunchOnce (state: WikiState) (key: string) (launch: BookkeeperLaunch) : bool * WikiState =
    if Set.contains key state.scheduledMaintenance then false, state
    else true, { state with scheduledMaintenance = Set.add key state.scheduledMaintenance; bookkeeperLaunches = state.bookkeeperLaunches @ [ launch ] }

let drainLaunches (state: WikiState) : BookkeeperLaunch list * WikiState =
    state.bookkeeperLaunches, { state with bookkeeperLaunches = [] }

let normalizeDraftIds (projection: WikiProjection) (drafts: WikiDraft list) : WikiDraft list =
    drafts
    |> List.map (fun draft ->
        match draft.id |> Option.bind tryParseId with
        | Some wikiId when Map.containsKey wikiId projection -> draft
        | _ -> { draft with id = None })

/// Unified command type covering every state change in the runtime (P53). The
/// reducer is the single pure dispatch; multi-value transitions
/// (recordLaunchOnce/drainLaunches) stay available as standalone functions so
/// callers needing their extra return value read it in the same tick.
type WikiCommand =
    | CacheSnapshotCmd of sessionID: string * projection: WikiProjection
    | RecordLaunchCmd of launch: BookkeeperLaunch
    | UpdateLatestLaunchResultCmd of title: string * result: string
    | RecordLaunchOnceCmd of key: string * launch: BookkeeperLaunch
    | DrainLaunchesCmd

let reducer (state: WikiState) (cmd: WikiCommand) : WikiState =
    match cmd with
    | CacheSnapshotCmd (sessionID, projection) -> cacheSnapshot state sessionID projection
    | RecordLaunchCmd launch -> recordLaunch state launch
    | UpdateLatestLaunchResultCmd (title, result) -> updateLatestLaunchResult state title result
    | RecordLaunchOnceCmd (key, launch) -> recordLaunchOnce state key launch |> snd
    | DrainLaunchesCmd -> drainLaunches state |> snd
