namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Persistence.EventStore

[<Sealed>]
type EventStoreHandle =
    private new: store: IEventStore -> EventStoreHandle
    member Store: IEventStore
    member Dispose: unit -> unit
    static member Create: store: IEventStore -> EventStoreHandle
