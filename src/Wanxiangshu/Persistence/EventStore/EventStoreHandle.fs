namespace Wanxiangshu.Persistence.EventStore

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

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

    member _.ReadPayload(payloadRef: string) : Task<byte[] option> =
        task {
            match! store.ReadPayload(PayloadRef.create payloadRef) with
            | Ok bytesOpt -> return bytesOpt
            | Error _ -> return None
        }

    static member Create(store: IEventStore) = EventStoreHandle(store)
