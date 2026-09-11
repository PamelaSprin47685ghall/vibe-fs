namespace Wanxiangshu.Persistence.EventStore

/// Opaque capability for one process-local EventStore writer.
/// The underlying F# store and Integrator never cross the semantic boundary.
[<Sealed>]
type EventStoreHandle private (store: IEventStore) =
    // DSL-MUTABLE: resource — one-shot physical writer disposal latch
    let mutable disposed = false

    member _.Store =
        if disposed then
            invalidOp "EventStore handle is disposed"

        store

    member _.Dispose() = disposed <- true

    static member Create(store: IEventStore) = EventStoreHandle(store)
