namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks

/// JS-native Casebook index boundary. Public entries contain only a shelfmark
/// and canonical question; durable session identity remains inside the owner.
module CasebookIndexSurface =

    /// Current frozen provider-visible snapshot, or `null` before first refresh.
    val tryGet: unit -> obj

    /// Mark the provider-visible index dirty for its next refresh.
    val invalidate: unit -> unit

    /// Stable provider-visible shelfmark for a durable Case identity.
    val shelfmarkFor: sessionId: string -> canonicalQuestion: string -> string

    /// Rebuild the provider-visible snapshot from unified EventStore Current.
    val refresh: store: obj -> capacity: int -> Task<obj>

    /// Resolve a shelfmark without exposing internal index records.
    val resolve: store: obj -> capacity: int -> shelfmark: string -> Task<obj>
