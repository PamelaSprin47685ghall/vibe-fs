namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// CASE-006 / KR-006 / KR-015: Host Bookkeeper — single-pass diff refresh.
/// Missing session port or transaction Error refuses refresh; an ambiguous
/// append retains its evidence without claiming that Current stayed unchanged.
module CasebookBookkeeper =

    /// Returns Ok true when a Refreshed event was published; Ok false when
    /// no diff or no-case. Error retains the store or transaction failure;
    /// an unknown append cannot authorize a success report or blind retry.
    val refreshStale:
        store: IEventStore -> root: string -> sessionId: string -> Task<Result<bool, CasebookMutationError>>
