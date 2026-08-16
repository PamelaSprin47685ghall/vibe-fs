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
        match value with
        | null -> "null"
        | :? string as s -> s
        | _ -> JS.JSON.stringify value

    let private parentIds (value: obj) : EventId list =
        let arr = unbox<string array> value
        arr |> Array.toList |> List.map EventId.create

    let private payloadRefsOf (value: obj) : PayloadRef list =
        match value with
        | null -> []
        | _ ->
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

    let private envelopeToJs (envelope: EventEnvelope) : obj =
        let payloadObject =
            CanonicalEventCodec.encode envelope
            |> (fun s -> s.TrimEnd('\n'))
            |> JS.JSON.parse

        box
            {| id = EventId.value envelope.EventId
               stream = EventStreamId.value envelope.StreamId
               ``type`` = envelope.EventType
               parents = envelope.Parents |> List.map EventId.value |> List.toArray
               payload = payloadObject
               payloadRefs = envelope.PayloadRefs |> List.map PayloadRef.value |> List.toArray |}

    let private cutToJs (cut: SemanticCut) : obj =
        box
            {| failedEventId = EventId.value cut.FailedEventId
               rule = cut.Rule
               cutEventId = EventId.value cut.CutEventId
               reason = cut.Reason |}

    /// Append JS-shaped events to a local store.
    /// Returns `{ ok: true, cuts: [...] }` or `{ ok: false, error: string }`.
    /// Cuts are the semantic-cut tail-reset events the integrator appended.
    let append (store: IEventStore) (events: obj array) : System.Threading.Tasks.Task<obj> =
        task {
            let parsed = events |> Array.toList |> List.map eventOfJs

            let! (result: Result<AppendReceipt, AppendError>) = store.Append(parsed)

            match result with
            | Ok receipt ->
                return
                    box
                        {| ok = true
                           cuts = receipt.Cuts |> List.map cutToJs |> List.toArray |}
            | Error e -> return box {| ok = false; error = e.ToString() |}
        }

    /// Try to read one event by id. Returns the JS-shaped envelope or null.
    let tryEvent (store: IEventStore) (eventId: string) : obj =
        match store.TryEvent(EventId.create eventId) with
        | None -> null
        | Some envelope -> envelopeToJs envelope
