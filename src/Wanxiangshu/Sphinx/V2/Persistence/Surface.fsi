namespace Wanxiangshu.Sphinx.V2.Persistence

open Wanxiangshu.Persistence.EventStore

/// Native producer/recovery boundary. Handles are opaque; no writer/history reads.
module Surface =
    val canonicalizeBody: obj -> obj
    val canonicalizeTransition: obj -> obj
    /// Pure sealed candidate. It is not a durable receipt and never publishes Current.
    val prepareTransition: handle: EventStoreHandle * digest: (string -> string) * raw: obj -> obj
    /// Reads only the shared canonical Current; invalid and forked inquiries are errors.
    val canonicalCurrent: handle: EventStoreHandle * digest: (string -> string) * inquiryId: string -> obj
    /// Observes the actual pure Driver proposal from the shared Current; no append/effect.
    val proposeAdvance: handle: EventStoreHandle * digest: (string -> string) * inquiryId: string -> obj
    /// Runs the actual control-command admission against cold/live canonical state.
    val admitCancel: handle: EventStoreHandle * raw: obj -> obj
