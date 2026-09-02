namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// CASE-006 Host Bookkeeper — freeze replayed observations, run one CaseRefresh
/// child session, stability-verify, then publish InspectorCaseRefreshed.
/// Missing session port or transaction Error keeps the old Case.
module CasebookBookkeeper =

    /// Returns Ok true when a Refreshed event was published; Ok false when
    /// Fresh / no-case (nothing to do). Error on store, transaction, or
    /// stability-verify failure — the old Case is left intact.
    val refreshStale: store: IEventStore -> root: string -> sessionId: string -> Task<Result<bool, string>>
