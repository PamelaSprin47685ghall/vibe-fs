namespace Wanxiangshu.Repository.Knowledge.Casebook

/// Durable Casebook event vocabulary. The Casebook store, its integration rule
/// and the persistence vocabulary join these names instead of restating them.
module CasebookEventTypes =
    val Captured: string
    val Refreshed: string
    val Accessed: string
    val Evicted: string
    val all: string list
    val isCasebookEvent: eventType: string -> bool
