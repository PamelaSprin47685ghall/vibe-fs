namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// CASE-006 / KR-006 / KR-015: Host Bookkeeper — single-pass diff refresh.
/// Missing session port or transaction Error keeps the old Case.
module CasebookBookkeeper =

    /// Returns Ok true when a Refreshed event was published; Ok false when
    /// no diff or no-case. Error on store or transaction failure — the old Case is left intact.
    val refreshStale: store: IEventStore -> root: string -> sessionId: string -> Task<Result<bool, string>>
