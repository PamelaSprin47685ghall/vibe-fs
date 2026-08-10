namespace Wanxiangshu.Domain

/// DSL-class: DurableFact — CASE-003: one typed observation captured from the
/// final execution layer (builtin read/glob/grep Host execution). Never
/// inferred from transcript text; capture may be incomplete.
[<RequireQualifiedAccess>]
type Observation =
    | FileRead of path: string * contentHash: string
    | GlobResult of pattern: string * paths: string list
    | GrepResult of pattern: string * matches: (string * int * string) list

/// DSL-class: Vocabulary — normalized identity of an observation: same path +
/// same content (or same result set) dedupe to one identity (CASE-003).
type ObservationIdentity = private ObservationIdentity of string

module ObservationIdentity =

    let ofObservation (observation: Observation) : ObservationIdentity =
        let raw =
            match observation with
            | Observation.FileRead(path, hash) -> "read:" + path + ":" + hash
            | Observation.GlobResult(pattern, paths) ->
                "glob:" + pattern + ":" + (paths |> List.sort |> String.concat ",")
            | Observation.GrepResult(pattern, matches) ->
                let flat =
                    matches
                    |> List.map (fun (path, index, text) -> path + "@" + string index + ":" + text)
                    |> List.sort
                    |> String.concat "|"

                "grep:" + pattern + ":" + flat

        ObservationIdentity raw

/// DSL-class: DurableFact — CASE-002: the logical Case materials. Q is the
/// verbatim Inspector initial prompt; A is the verbatim ToolResult body;
/// observations are the replayable evidence behind A.
type Case =
    {
        SessionId: string
        Q: string
        A: string
        Observations: Observation list
        /// Projection-derived access order (monotonic counter — never a wall
        /// clock; CASE-008/G4R time boundary).
        LastAccessOrder: int64
    }

/// DSL-class: DurableFact — CASE-007: the Casebook domain events. Physical
/// persistence is the unified EventStore; these are the fold inputs.
[<RequireQualifiedAccess>]
type CasebookEvent =
    | CaseCaptured of Case
    | CaseRefreshed of sessionId: string * q: string * a: string * observations: Observation list
    | CaseAccessed of sessionId: string
    | CaseEvicted of sessionId: string

/// DSL-class: Decision — CASE-004/005: the replay classification. No-delta is
/// only a freshness hint, never a correctness proof.
[<RequireQualifiedAccess>]
type ReplayResult =
    | Fresh
    | Stale

module Observations =

    /// CASE-003: normalize — dedupe by identity and fix a canonical order so
    /// the same captured evidence always folds to the same Case bytes.
    let normalize (observations: Observation list) : Observation list =
        observations
        |> List.map (fun o -> ObservationIdentity.ofObservation o, o)
        |> List.sortBy (fun (identity, _) -> let (ObservationIdentity raw) = identity in raw)
        |> List.distinctBy fst
        |> List.map snd

    /// CASE-003: replay classification — every stored observation must match
    /// the replayed result set exactly (same normalized set).
    let classifyReplay (stored: Observation list) (replayed: Observation list) : ReplayResult =
        let storedIds =
            stored |> normalize |> List.map ObservationIdentity.ofObservation |> Set.ofList

        let replayedIds =
            replayed
            |> normalize
            |> List.map ObservationIdentity.ofObservation
            |> Set.ofList

        if storedIds = replayedIds then
            ReplayResult.Fresh
        else
            ReplayResult.Stale

/// DSL-class: Decision — CASE-008: the CasebookProjection fold. Captured
/// inserts/replaces a Case; Refreshed replaces Q/A/observations; Accessed
/// bumps the derived access order; Evicted removes. Same-Case concurrent
/// forks surface as DomainConflict at the EventStore layer and converge via
/// later resolution/refresh/evict events — never via revision/wall_clock LWW.
module CasebookProjection =

    let empty: Map<string, Case> = Map.empty

    let fold (events: CasebookEvent list) : Map<string, Case> =
        let rec go
            (accessCounter: int64)
            (cases: Map<string, Case>)
            (remaining: CasebookEvent list)
            : Map<string, Case> =
            match remaining with
            | [] -> cases
            | CasebookEvent.CaseCaptured case :: rest ->
                let withAccess =
                    { case with
                        LastAccessOrder = accessCounter }

                go (accessCounter + 1L) (Map.add case.SessionId withAccess cases) rest
            | CasebookEvent.CaseRefreshed(sessionId, q, a, observations) :: rest ->
                match Map.tryFind sessionId cases with
                | Some existing ->
                    let updated =
                        { existing with
                            Q = q
                            A = a
                            Observations = Observations.normalize observations
                            LastAccessOrder = accessCounter }

                    go (accessCounter + 1L) (Map.add sessionId updated cases) rest
                | None -> go accessCounter cases rest
            | CasebookEvent.CaseAccessed sessionId :: rest ->
                match Map.tryFind sessionId cases with
                | Some existing ->
                    let touched =
                        { existing with
                            LastAccessOrder = accessCounter }

                    go (accessCounter + 1L) (Map.add sessionId touched cases) rest
                | None -> go accessCounter cases rest
            | CasebookEvent.CaseEvicted sessionId :: rest -> go accessCounter (Map.remove sessionId cases) rest

        go 0L empty events

    /// CASE-008: LRU eviction — keep the capacity most-recently-accessed
    /// Cases; the evicted session ids are returned so the caller can append
    /// InspectorCaseEvicted facts (tombstones are events too).
    let evict (capacity: int) (cases: Map<string, Case>) : Map<string, Case> * string list =
        if capacity <= 0 || Map.count cases <= capacity then
            cases, []
        else
            let victims =
                cases
                |> Map.toList
                |> List.sortBy (fun (_, case) -> case.LastAccessOrder)
                |> List.take (Map.count cases - capacity)
                |> List.map fst

            let remaining = victims |> List.fold (fun acc id -> Map.remove id acc) cases
            remaining, victims
