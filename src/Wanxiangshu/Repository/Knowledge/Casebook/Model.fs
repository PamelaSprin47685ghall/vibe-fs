namespace Wanxiangshu.Repository.Knowledge.Casebook

open System

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

/// DSL-class: DurableFact — CASE-002 / KR-002: minimal Case model with dual baselines.
/// Identity is the stable logical case identity (scoped to invocation).
type Case =
    {
        Identity: string
        SourceTrace: string
        Q: string
        A: string
        RelatedPaths: string list
        CompletionFileState: string
        MaintenanceFileState: string
        AccessOrder: int64
        Observations: Observation list
    }
    member this.SessionId = this.Identity
    member this.LastAccessOrder = this.AccessOrder

/// DSL-class: DurableFact — CASE-007 / KR-007: the Casebook domain events. Physical
/// persistence is the unified EventStore; these are the fold inputs.
[<RequireQualifiedAccess>]
type CasebookEvent =
    | CaseCaptured of Case
    | CaseRefreshed of identity: string * q: string * a: string * maintenanceFileState: string * relatedPaths: string list * observations: Observation list
    | CaseAccessed of identity: string
    | CaseEvicted of identity: string

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
/// inserts/replaces a Case; Refreshed replaces Q/A/maintenance/related; Accessed
/// bumps the derived access order; Evicted removes. Same-Case concurrent
/// forks surface as DomainConflict at the EventStore layer and converge via
/// later resolution/refresh/evict events — never via revision/wall_clock LWW.
module CasebookProjection =

    /// Incremental Current owned by the canonical Integrator. The access counter
    /// is part of the derived state so boot replay and live integration use the
    /// exact same single-event rule.
    type State =
        { AccessCounter: int64
          Cases: Map<string, Case> }

    let emptyState: State =
        { AccessCounter = 0L
          Cases = Map.empty }

    let empty: Map<string, Case> = emptyState.Cases

    let private refreshCase state identity q a maintenanceFileState relatedPaths observations =
        match Map.tryFind identity state.Cases with
        | Some existing ->
            let updated =
                { existing with
                    Q = q
                    A = a
                    MaintenanceFileState = if String.IsNullOrEmpty maintenanceFileState then existing.MaintenanceFileState else maintenanceFileState
                    RelatedPaths = if List.isEmpty relatedPaths then existing.RelatedPaths else relatedPaths
                    Observations = Observations.normalize observations
                    AccessOrder = state.AccessCounter }

            { AccessCounter = state.AccessCounter + 1L
              Cases = Map.add identity updated state.Cases }
        | None -> state

    let private accessCase state identity =
        match Map.tryFind identity state.Cases with
        | Some existing ->
            let touched =
                { existing with
                    AccessOrder = state.AccessCounter }

            { AccessCounter = state.AccessCounter + 1L
              Cases = Map.add identity touched state.Cases }
        | None -> state

    let apply (state: State) (event: CasebookEvent) : State =
        match event with
        | CasebookEvent.CaseCaptured case ->
            let withAccess =
                { case with
                    Observations = Observations.normalize case.Observations
                    AccessOrder = state.AccessCounter }

            { AccessCounter = state.AccessCounter + 1L
              Cases = Map.add case.Identity withAccess state.Cases }
        | CasebookEvent.CaseRefreshed(identity, q, a, maintenanceFileState, relatedPaths, observations) ->
            refreshCase state identity q a maintenanceFileState relatedPaths observations
        | CasebookEvent.CaseAccessed identity -> accessCase state identity
        | CasebookEvent.CaseEvicted identity ->
            { state with
                Cases = Map.remove identity state.Cases }

    // No history-fold API by design. CanonicalIntegrator is the sole history
    // enumerator and registers `apply` as this module's one-event oracle.

    /// CASE-008: LRU eviction — keep the capacity most-recently-accessed
    /// Cases; the evicted session ids are returned so the caller can append
    /// Evicted facts (tombstones are events too).
    let evict (capacity: int) (cases: Map<string, Case>) : Map<string, Case> * string list =
        if capacity <= 0 || Map.count cases <= capacity then
            cases, []
        else
            let victims =
                cases
                |> Map.toList
                |> List.sortBy (fun (_, case) -> case.AccessOrder)
                |> List.take (Map.count cases - capacity)
                |> List.map fst

            let remaining = victims |> List.fold (fun acc id -> Map.remove id acc) cases
            remaining, victims
