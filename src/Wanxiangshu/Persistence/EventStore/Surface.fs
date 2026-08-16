namespace Wanxiangshu.Persistence.EventStore

open System
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation.Identity

/// JS-native semantic surface for EventStore durable append primitives
/// (DURABLE-EVENTS-001/004/005/006). Tests speak in plain JS events; the
/// F# EventEnvelope / FSharpList / CanonicalIntegrator stay inside.
[<RequireQualifiedAccess>]
module EventStoreSurface =

    let private str (value: obj) : string = string value

    let private payloadJson (value: obj) : string =
        if isNull value then
            "null"
        else
            match value with
            | :? string as s -> s
            | _ -> JS.JSON.stringify value

    let private parentIds (value: obj) : EventId list =
        let arr = unbox<string array> value
        arr |> Array.toList |> List.map EventId.create

    let private payloadRefsOf (value: obj) : PayloadRef list =
        if isNull value then
            []
        else
            unbox<string array> value
            |> Array.toList
            |> List.map PayloadRef.create

    let private eventOfJs (value: obj) : EventEnvelope =
        { EventId = EventId.create (str (value?id))
          StreamId = EventStreamId.create (str (value?stream))
          EventType = str (value?``type``)
          Parents = parentIds (value?parents)
          Payload = unbox<JsonValue> (JS.JSON.parse (payloadJson (value?payload)))
          PayloadRefs = payloadRefsOf (value?payloadRefs) }
        |> EventEnvelope.normalize

    /// Create a local EventStore instance for one writer. `commonDir` is the
    /// Git common directory; the caller owns temp directory lifecycle.
    /// Returns a JS object `{ commonDir, store }` so the caller can locate
    /// the writer file for verification and clean up the temp directory.
    let createLocalStore (commonDir: string) (writerId: string) : obj =
        let integrator = CanonicalIntegrator.create ()
        box
            {| commonDir = commonDir
               store = EventStore.createLocal commonDir writerId integrator |}

    /// Append JS-shaped events to a local store.
    /// Returns `{ ok: true }` or `{ ok: false, error: string }`.
    let append (store: IEventStore) (events: obj array) : System.Threading.Tasks.Task<obj> =
        task {
            let parsed = events |> Array.toList |> List.map eventOfJs

            let! (result: Result<AppendReceipt, AppendError>) = store.Append(parsed)

            match result with
            | Ok _ -> return box {| ok = true |}
            | Error e -> return box {| ok = false; error = e.ToString() |}
        }
