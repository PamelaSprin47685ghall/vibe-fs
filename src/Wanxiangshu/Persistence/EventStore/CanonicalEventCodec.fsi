namespace Wanxiangshu.Persistence.EventStore

module CanonicalEventCodec =
    val encode: envelope: EventEnvelope -> string
    val checkIdentity: left: EventEnvelope -> right: EventEnvelope -> Result<unit, StorageInvalid>
    val mergeByIdentity: events: EventEnvelope list -> Result<EventEnvelope list, StorageInvalid>
    val tryDecode: text: string -> Result<EventEnvelope, StorageInvalid>
    val tryDecodeUtf8Text: bytes: byte[] -> Result<string, StorageInvalid>
    val tryDecodeUtf8: bytes: byte[] -> Result<EventEnvelope, StorageInvalid>
