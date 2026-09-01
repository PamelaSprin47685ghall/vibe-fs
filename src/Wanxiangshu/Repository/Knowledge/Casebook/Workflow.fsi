namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// CASE-009: feature gating — the product surface lives only when the marker
/// directory exists. Disabling closes schema, execution, capture, archive and
/// Bookkeeper; it never touches the unified store (Persist owns that).
module CasebookFeature =

    /// The opt-in marker (§3.1): directory existence only; `.keep` contents are
    /// never interpreted.
    val MarkerDirectory: string

    /// True when the Casebook marker directory exists under the workspace root.
    val isEnabled: workspaceRoot: string -> bool

/// CASE-003/004/005: the Casebook workflow — archive, fetch, freshness check.
/// Archive failure is NOT an Inspector call failure: every function returns a
/// Result and the caller decides how to surface it.
module CasebookWorkflow =

    /// Archive one Inspector result. Structural parent selection belongs to the
    /// canonical Integrator/store, not to a feature-owned history scan.
    val archiveInspectorResult: store: IEventStore -> case: Case -> Task<Result<unit, string>>

    /// Fetch one Case by session id (CASE-004).
    val fetchCase: store: IEventStore -> capacity: int -> sessionId: string -> Task<Result<Case option, string>>

    /// CASE-004/005: freshness is a hint, never a proof — exact normalized
    /// equality of stored vs replayed observations.
    val checkFreshness: stored: Case -> replayed: Observation list -> ReplayResult

    /// CASE-006: the full refresh decision — fetch the Case, replay against
    /// the current worktree, and report whether a Bookkeeper revision is
    /// needed (Stale) or the old answer still matches (Fresh / no-case).
    val needsRefresh:
        store: IEventStore -> capacity: int -> sessionId: string -> root: string -> Task<Result<bool, string>>

    /// Append a CaseRefreshed event for the given session.
    val refreshCase:
        store: IEventStore ->
        sessionId: string ->
        q: string ->
        a: string ->
        observations: Observation list ->
            Task<Result<unit, string>>

    /// CASE-010: exactly-one CaseFinalize — a reusable Inspector scope archives
    /// at most once (ReuseScope close → freeze draft → one finalize). A second
    /// finalize for the same session id is refused.
    val finalizeCase: store: IEventStore -> case: Case -> Task<Result<unit, string>>

    /// CASE-007: append InspectorCaseAccessed; structural parent comes from Current.
    val touchCaseAccess: store: IEventStore -> sessionId: string -> Task<Result<unit, string>>
