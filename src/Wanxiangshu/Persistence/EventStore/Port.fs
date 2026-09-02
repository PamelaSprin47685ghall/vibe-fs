namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// Application-facing local EventStore capability. Physical files, Git, codec,
/// replay and construction stay behind the composition boundary.
type IEventStore =
    abstract Append: events: EventEnvelope list -> Task<Result<AppendReceipt, AppendError>>
    abstract WritePayload: content: byte[] -> Task<Result<PayloadRef, string>>
    abstract ReadPayload: payloadRef: PayloadRef -> Task<Result<byte[] option, string>>
    abstract TryCurrent: key: string -> obj option
    abstract TryEvent: eventId: EventId -> EventEnvelope option
    abstract TryHeads: streamId: EventStreamId -> EventId list
    abstract TryHead: streamId: EventStreamId -> EventId option
    abstract AllHeads: unit -> EventId list
