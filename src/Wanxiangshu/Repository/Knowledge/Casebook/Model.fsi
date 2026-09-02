namespace Wanxiangshu.Repository.Knowledge.Casebook

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

    /// CASE-003: derive the canonical identity of an observation.
    val ofObservation: observation: Observation -> ObservationIdentity

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
    val normalize: observations: Observation list -> Observation list

    /// CASE-003: replay classification — every stored observation must match
    /// the replayed result set exactly (same normalized set).
    val classifyReplay: stored: Observation list -> replayed: Observation list -> ReplayResult

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

    /// CASE-008: empty projection state.
    val emptyState: State

    /// CASE-008: empty case map (projection shorthand).
    val empty: Map<string, Case>

    /// CASE-008: apply one CasebookEvent to the projection state.
    val apply: state: State -> event: CasebookEvent -> State

    /// CASE-008: LRU eviction — keep the capacity most-recently-accessed
    /// Cases; the evicted session ids are returned so the caller can append
    /// InspectorCaseEvicted facts (tombstones are events too).
    val evict: capacity: int -> cases: Map<string, Case> -> Map<string, Case> * string list
