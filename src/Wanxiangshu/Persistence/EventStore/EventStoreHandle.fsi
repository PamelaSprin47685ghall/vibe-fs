namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

[<Sealed>]
type EventStoreHandle =
    private new: store: IEventStore -> EventStoreHandle
    member Store: IEventStore
    member Dispose: unit -> unit
    member ReadPayload: payloadRef: string -> Task<byte[] option>
    static member Create: store: IEventStore -> EventStoreHandle
