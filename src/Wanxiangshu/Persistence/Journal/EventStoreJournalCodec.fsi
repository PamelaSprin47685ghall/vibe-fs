namespace Wanxiangshu.Persistence.Journal

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

module EventStoreJournalCodec =
    val JournalEnvelopeEventType: string
    val encodeStreamId: stream: StreamId -> EventStreamId
    val tryDecodeStreamId: streamId: EventStreamId -> Result<StreamId, string>
    val encode: parents: EventId list -> payloadRefs: PayloadRef list -> envelope: Envelope -> EventEnvelope
    val tryDecode: event: EventEnvelope -> Result<Envelope, string>
