namespace Wanxiangshu.Persistence.EventStore

open System
open System.Threading.Tasks

/// Process-local EventStore owner surface. JS callers receive unprefixed
/// operations; EventStoreHandle remains an opaque capability.
module Surface =
    /// Create a process-local writer capability. The caller owns its lifecycle.
    val create: commonDir: string * writerId: string -> EventStoreHandle

    /// Release a writer capability. Further operations fail rather than using a
    /// stale resource.
    val dispose: handle: EventStoreHandle -> unit

    /// Append JS-native events and return only the durable receipt.
    val append: handle: EventStoreHandle * events: obj array -> Task<obj>

    /// Read one durable event by identity. A missing event is `null`.
    val read: handle: EventStoreHandle * eventId: string -> obj

    /// Read all structural heads for one stream.
    val heads: handle: EventStoreHandle * streamId: string -> string array

    /// Read the unique structural head, or `null` when the stream is forked/empty.
    val head: handle: EventStoreHandle * streamId: string -> obj

    /// The canonical remote store ref owned by persistence infrastructure.
    val canonicalStoreRef: string
