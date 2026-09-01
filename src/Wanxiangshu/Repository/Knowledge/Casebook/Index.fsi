namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// Process-local Casebook index frozen for the current provider epoch.
/// Provider entries expose only a shelfmark plus canonical Q; durable session
/// identity remains an internal lookup key and never crosses the Casebook wire.
module CasebookIndex =

    /// Public provider-facing case index entry.
    type Entry = { Shelfmark: string; Question: string }

    /// A frozen index snapshot with a monotonic epoch.
    type Snapshot = { Epoch: int64; Cases: Entry list }

    /// Read the current frozen snapshot, if any.
    val tryGet: unit -> Snapshot option

    /// Force the next successful refresh to advance epoch (Captured/Refreshed/Evicted).
    val invalidate: unit -> unit

    /// Stable public locator. The suffix is a one-way catalog discriminator,
    /// never the durable session identity itself.
    val shelfmarkFor: sessionId: string -> canonicalQuestion: string -> string

    /// Resolve a public shelfmark to its internal Case without exposing the
    /// durable session key.
    val resolve: store: IEventStore -> capacity: int -> shelfmark: string -> Task<Result<Case option, string>>

    /// Rebuild from the unified EventStore projection. Epoch advances when the
    /// provider-visible index changes or an explicit invalidation occurred.
    val refresh: store: IEventStore -> capacity: int -> Task<Snapshot>
