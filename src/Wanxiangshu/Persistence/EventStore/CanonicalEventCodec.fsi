namespace Wanxiangshu.Persistence.EventStore

module CanonicalEventCodec =
    val canonicalStoreRef: string
    val encode: envelope: EventEnvelope -> string
    val checkIdentity: left: EventEnvelope -> right: EventEnvelope -> Result<unit, StorageInvalid>
    val mergeByIdentity: events: EventEnvelope list -> Result<EventEnvelope list, StorageInvalid>
    val tryDecode: text: string -> Result<EventEnvelope, StorageInvalid>
    val tryDecodeUtf8: bytes: byte[] -> Result<EventEnvelope, StorageInvalid>
