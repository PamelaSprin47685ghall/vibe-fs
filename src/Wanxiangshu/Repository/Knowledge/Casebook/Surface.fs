namespace Wanxiangshu.Repository.Knowledge.Casebook

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore

/// JS-native semantic surface for Casebook laws (PR 7 exemplar).
///
/// A JS test expresses observations / events / cases in plain JS:
///
/// ```js
/// const normalized = casebook.normalize([
///   { kind: 'file-read', path: 'a.txt', contentHash: 'h1' },
///   { kind: 'file-read', path: 'a.txt', contentHash: 'h1' },
/// ])
/// // [{ kind: 'file-read', path: 'a.txt', contentHash: 'h1' }]
///
/// const view = casebook.foldEvents([
///   { kind: 'case-captured', case: { sessionId: 's1', q: 'Q', a: 'A', observations: [] } },
/// ])
/// // { ok: true, cases: [{ sessionId: 's1', ... }] }
/// ```
///
/// The F# `Observation` / `CasebookEvent` / `Case` unions stay inside the
/// surface; translation happens at the owner boundary
/// (JS-SEMANTIC-SURFACE-002/003/005). `store` is the opaque IEventStore
/// handle the test obtains from its fixture — passed back, never inspected.
module CasebookSurface =

    // ── Observation translation (JS ↔ F#) ────────────────────────────────────

    let private observationToJs (observation: Observation) : obj =
        match observation with
        | Observation.FileRead(path, contentHash) ->
            box {| kind = "file-read"; path = path; contentHash = contentHash |}
        | Observation.GlobResult(pattern, paths) ->
            box {| kind = "glob-result"; pattern = pattern; paths = List.toArray paths |}
        | Observation.GrepResult(pattern, matches) ->
            let flat =
                matches
                |> List.map (fun (path, index, text) -> box [| box path; box index; box text |])
                |> List.toArray

            box {| kind = "grep-result"; pattern = pattern; matches = flat |}

    let private observationOfJs (value: obj) : Result<Observation, string> =
        let kind = string (value?kind)

        let stringsOf (v: obj) : string array = unbox<string array> v
        let arrayOf (v: obj) : obj array = unbox<obj array> v
        let intOfJs (v: obj) : int = int (string v)

        match kind with
        | "file-read" -> Ok(Observation.FileRead(string (value?path), string (value?contentHash)))
        | "glob-result" ->
            let paths = stringsOf (value?paths) |> Array.toList
            Ok(Observation.GlobResult(string (value?pattern), paths))
        | "grep-result" ->
            let matches =
                arrayOf (value?matches)
                |> Array.toList
                |> List.map (fun m ->
                    let arr = arrayOf m
                    (string arr[0], intOfJs arr[1], string arr[2]))

            Ok(Observation.GrepResult(string (value?pattern), matches))
        | other -> Error $"unknown observation kind: {other}"

    // ── Case translation (JS ↔ F#) ───────────────────────────────────────────

    let private caseToJs (case: Case) : obj =
        box
            {| sessionId = case.SessionId
               q = case.Q
               a = case.A
               observations = case.Observations |> List.map observationToJs |> List.toArray
               lastAccessOrder = case.LastAccessOrder |}

    let private caseOfJs (value: obj) : Result<Case, string> =
        let arrayOf (v: obj) : obj array = unbox<obj array> v
        let observations =
            arrayOf (value?observations)
            |> Array.toList
            |> List.map observationOfJs
            |> List.fold
                (fun acc item ->
                    match acc, item with
                    | Error message, _ -> Error message
                    | _, Error message -> Error message
                    | Ok list, Ok observation -> Ok(observation :: list))
                (Ok [])

        observations
        |> Result.map (fun obs ->
            { SessionId = string (value?sessionId)
              Q = string (value?q)
              A = string (value?a)
              Observations = List.rev obs
              LastAccessOrder = int64 (value?lastAccessOrder) })

    // ── CASE-003: normalize — dedupe by identity, canonical order ────────────

    /// Normalize a JS observation array: same identity → one entry; glob
    /// paths order-insensitive. Returns the normalized JS array.
    let normalize (observations: obj array) : obj array =
        observations
        |> Array.toList
        |> List.map observationOfJs
        |> List.choose (fun item -> match item with Ok o -> Some o | Error _ -> None)
        |> Observations.normalize
        |> List.map observationToJs
        |> List.toArray

    /// CASE-003 replay classification:
    /// `'fresh'` only on exact normalized equality, `'stale'` otherwise.
    let classifyReplay (stored: obj array) (replayed: obj array) : string =
        let storedObs =
            stored
            |> Array.toList
            |> List.map observationOfJs
            |> List.choose (fun item -> match item with Ok o -> Some o | Error _ -> None)

        let replayedObs =
            replayed
            |> Array.toList
            |> List.map observationOfJs
            |> List.choose (fun item -> match item with Ok o -> Some o | Error _ -> None)

        match Observations.classifyReplay storedObs replayedObs with
        | ReplayResult.Fresh -> "fresh"
        | ReplayResult.Stale -> "stale"

    // ── CASE-002/007/008: fold events → Cases (JS-shaped) ────────────────────

    /// Parse a JS observations array; fails closed on the first unknown kind.
    let private observationsOfJs (value: obj) : Result<Observation list, string> =
        let rec loop (acc: Observation list) (remaining: obj list) =
            match remaining with
            | [] -> Ok(List.rev acc)
            | item :: rest ->
                observationOfJs item
                |> Result.bind (fun observation -> loop (observation :: acc) rest)

        loop [] (unbox<obj array> value |> Array.toList)

    let private eventOfJs (value: obj) : Result<CasebookEvent, string> =
        let kind = string (value?kind)

        match kind with
        | "case-captured" ->
            caseOfJs (value?``case``) |> Result.map CasebookEvent.CaseCaptured
        | "case-refreshed" ->
            observationsOfJs (value?observations)
            |> Result.map (fun obs ->
                CasebookEvent.CaseRefreshed(string (value?sessionId), string (value?q), string (value?a), obs))
        | "case-accessed" -> Ok(CasebookEvent.CaseAccessed(string (value?sessionId)))
        | "case-evicted" -> Ok(CasebookEvent.CaseEvicted(string (value?sessionId)))
        | other -> Error $"unknown casebook event kind: {other}"

    /// Fold JS casebook events through the production projection.
    /// Returns `{ ok: true, cases: [...] }` or `{ ok: false, error }`.
    let foldEvents (events: obj array) : obj =
        let rec foldAll (state: CasebookProjection.State) (remaining: obj list) =
            match remaining with
            | [] -> Ok state
            | event :: rest ->
                eventOfJs event
                |> Result.bind (fun parsed -> foldAll (CasebookProjection.apply state parsed) rest)

        match foldAll CasebookProjection.emptyState (List.ofArray events) with
        | Error message -> box {| ok = false; error = message |}
        | Ok state ->
            let cases = state.Cases |> Map.toList |> List.map (fun (_, case) -> caseToJs case) |> List.toArray
            box {| ok = true; cases = cases |}

    /// CASE-008 LRU eviction. JS Cases in, `{ kept, victims }` out.
    let evict (capacity: int) (cases: obj array) : obj =
        let parsed =
            cases
            |> Array.toList
            |> List.map (fun value -> caseOfJs value |> Result.map (fun case -> case.SessionId, case))
            |> List.choose (fun item -> match item with Ok c -> Some c | Error _ -> None)
            |> Map.ofList

        let kept, victims = CasebookProjection.evict capacity parsed

        box
            {| kept = kept |> Map.toList |> List.map (fun (_, case) -> caseToJs case) |> List.toArray
               victims = List.toArray victims |}

    // ── CASE-010 / archive: store-bound workflows (opaque IEventStore) ────────

    /// Execute one store workflow over a parsed Case (single decision layer).
    let private runWorkflowTask
        (workflow: IEventStore -> Case -> System.Threading.Tasks.Task<Result<unit, string>>)
        (store: IEventStore)
        (parsed: Case)
        : System.Threading.Tasks.Task<obj> =
        task {
            match! workflow store parsed with
            | Ok() -> return box {| ok = true |}
            | Error message -> return box {| ok = false; error = message |}
        }

    /// CASE-010 exactly-once finalize. `{ ok: true } | { ok: false, error }`.
    let private runStoreWorkflow
        (workflow: IEventStore -> Case -> System.Threading.Tasks.Task<Result<unit, string>>)
        (store: IEventStore)
        (case: obj)
        : System.Threading.Tasks.Task<obj> =
        match caseOfJs case with
        | Error message -> System.Threading.Tasks.Task.FromResult(box {| ok = false; error = message |})
        | Ok parsed -> runWorkflowTask workflow store parsed

    /// CASE-010 exactly-once finalize. `{ ok: true } | { ok: false, error }`.
    let finalize (store: IEventStore) (case: obj) : System.Threading.Tasks.Task<obj> =
        runStoreWorkflow CasebookWorkflow.finalizeCase store case

    /// Archive one Inspector result. `{ ok: true } | { ok: false, error }`.
    let archive (store: IEventStore) (case: obj) : System.Threading.Tasks.Task<obj> =
        runStoreWorkflow CasebookWorkflow.archiveInspectorResult store case
