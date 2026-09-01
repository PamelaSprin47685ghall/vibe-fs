namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Persistence.EventStore

[<Sealed>]
type EventStoreHandle =
    private new: store: IEventStore -> EventStoreHandle
    member internal Store: IEventStore
    member internal Dispose: unit -> unit
    static member internal Create: store: IEventStore -> EventStoreHandle
