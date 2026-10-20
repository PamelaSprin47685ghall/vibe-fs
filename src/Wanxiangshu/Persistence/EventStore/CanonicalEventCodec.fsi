namespace Wanxiangshu.Persistence.EventStore

module CanonicalEventCodec =
    /// wanxiangli/events `EventInput` object for one envelope (library boundary only).
    val toLibraryEvent: envelope: EventEnvelope -> obj
    /// StorageInvalid for a wanxiangli `CodecError`-shaped value (library boundary only).
    val invalidOfLibraryError: error: obj -> StorageInvalid
    val encode: envelope: EventEnvelope -> string
    val checkIdentity: left: EventEnvelope -> right: EventEnvelope -> Result<unit, StorageInvalid>
    val mergeByIdentity: events: EventEnvelope list -> Result<EventEnvelope list, StorageInvalid>
    val tryDecode: text: string -> Result<EventEnvelope, StorageInvalid>
    val tryDecodeUtf8Text: bytes: byte[] -> Result<string, StorageInvalid>
    val tryDecodeUtf8: bytes: byte[] -> Result<EventEnvelope, StorageInvalid>
