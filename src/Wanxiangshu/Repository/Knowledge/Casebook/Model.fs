namespace Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

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

    let apply (state: State) (event: CasebookEvent) : State =
        match event with
        | CasebookEvent.CaseCaptured case ->
            let withAccess =
                { case with
                    LastAccessOrder = state.AccessCounter }

            { AccessCounter = state.AccessCounter + 1L
              Cases = Map.add case.SessionId withAccess state.Cases }
        | CasebookEvent.CaseRefreshed(sessionId, q, a, observations) ->
            match Map.tryFind sessionId state.Cases with
            | Some existing ->
                let updated =
                    { existing with
                        Q = q
                        A = a
                        Observations = Observations.normalize observations
                        LastAccessOrder = state.AccessCounter }

                { AccessCounter = state.AccessCounter + 1L
                  Cases = Map.add sessionId updated state.Cases }
            | None -> state
        | CasebookEvent.CaseAccessed sessionId ->
            match Map.tryFind sessionId state.Cases with
            | Some existing ->
                let touched =
                    { existing with
                        LastAccessOrder = state.AccessCounter }

                { AccessCounter = state.AccessCounter + 1L
                  Cases = Map.add sessionId touched state.Cases }
            | None -> state
        | CasebookEvent.CaseEvicted sessionId ->
            { state with
                Cases = Map.remove sessionId state.Cases }

    // No history-fold API by design. CanonicalIntegrator is the sole history
    // enumerator and registers `apply` as this module's one-event oracle.

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
