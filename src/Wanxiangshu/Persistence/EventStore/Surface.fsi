namespace Wanxiangshu.Persistence.EventStore

open System
open System.Threading.Tasks

/// Process-local EventStore owner surface. JS callers receive unprefixed
/// operations; EventStoreHandle remains an opaque capability.
module Surface =
    val appendErrorToJs: error: AppendError -> obj

    /// Create a process-local writer capability. The caller owns its lifecycle.
    val create: commonDir: string * writerId: string -> EventStoreHandle

    val createWithCurrentCommitFault:
        commonDir: string * writerId: string * cause: obj * commitBeforeFailure: bool -> EventStoreHandle

    val createAppendFailureStore:
        baseHandle: EventStoreHandle * options: obj * onAppend: (obj -> unit) -> EventStoreHandle

    val createAppendPayloadStore:
        baseHandle: EventStoreHandle * malformed: bool * onAppend: (obj -> unit) -> EventStoreHandle

    /// Release a writer capability. Further operations fail rather than using a
    /// stale resource.
    val dispose: handle: EventStoreHandle -> unit

    /// Append JS-native events and return only the durable receipt.
    val append: handle: EventStoreHandle * events: obj array -> Task<obj>

    /// Read one durable event by identity. A missing event is `null`.
    val read: handle: EventStoreHandle * eventId: string -> obj

    /// Read every stream id that owns a durable head.
    val streams: handle: EventStoreHandle -> string array

    /// Read all structural heads for one stream.
    val heads: handle: EventStoreHandle * streamId: string -> string array

    /// Read the unique structural head, or `null` when the stream is forked/empty.
    val head: handle: EventStoreHandle * streamId: string -> obj

    val readPayload: handle: EventStoreHandle * payloadRef: string -> Task<obj>

    /// The canonical remote store ref owned by persistence infrastructure.
    val canonicalStoreRef: string
