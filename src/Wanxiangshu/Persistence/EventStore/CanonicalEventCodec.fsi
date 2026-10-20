namespace Wanxiangshu.Persistence.EventStore

/// Why a stored local line set fails its closure (wanxiangli/events `checkClosure`).
[<RequireQualifiedAccess>]
type StoredLineFault =
    | UnknownEventType of eventType: string
    | MissingPayload of payloadRef: PayloadRef
    | PayloadDigestMismatch of payloadRef: PayloadRef
    | UnreferencedPayload of payloadRef: PayloadRef

module CanonicalEventCodec =
    /// wanxiangli/events `EventInput` object for one envelope (library boundary only).
    val toLibraryEvent: envelope: EventEnvelope -> obj
    /// EventEnvelope of a wanxiangli/events `EventOutput` (library boundary only).
    val ofLibraryEvent: value: obj -> EventEnvelope
    /// StorageInvalid for a wanxiangli `CodecError`-shaped value (library boundary only).
    val invalidOfLibraryError: error: obj -> StorageInvalid
    val encode: envelope: EventEnvelope -> string
    val checkIdentity: left: EventEnvelope -> right: EventEnvelope -> Result<unit, StorageInvalid>
    val mergeByIdentity: events: EventEnvelope list -> Result<EventEnvelope list, StorageInvalid>
    val tryDecode: text: string -> Result<EventEnvelope, StorageInvalid>
    val tryDecodeUtf8Text: bytes: byte[] -> Result<string, StorageInvalid>
    val tryDecodeUtf8: bytes: byte[] -> Result<EventEnvelope, StorageInvalid>

    /// Closure of stored local lines (append batch or retained replay set):
    /// vocabulary over the whole set first, then strict inline payload closure
    /// line by line. No parent closure, no identity merge.
    val checkStoredClosure:
        isEventTypeKnown: (string -> bool) -> events: EventEnvelope list -> Result<unit, StoredLineFault>
