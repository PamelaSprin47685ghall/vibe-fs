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

    let private storageInvalidToJs (error: StorageInvalid) : obj =
        match error with
        | StorageInvalid.IdentityCollision eid ->
            box {| code = "IdentityCollision"; eventId = EventId.value eid |}
        | StorageInvalid.NonCanonical reason -> box {| code = "NonCanonical"; reason = reason |}
        | StorageInvalid.MalformedEnvelope reason -> box {| code = "MalformedEnvelope"; reason = reason |}
        | StorageInvalid.MissingParent eid ->
            box {| code = "MissingParent"; eventId = EventId.value eid |}
        | StorageInvalid.CyclicParents -> box {| code = "CyclicParents" |}
        | StorageInvalid.MissingPayload ref ->
            box {| code = "MissingPayload"; payloadRef = PayloadRef.value ref |}
        | StorageInvalid.UnknownEventType t ->
            box {| code = "UnknownEventType"; eventType = t |}

    let private appendErrorToJs (error: AppendError) : obj =
        match error with
        | AppendError.StorageInvalid e ->
            box {| code = "StorageInvalid"; error = storageInvalidToJs e |}
        | AppendError.SemanticCut c ->
            box {| code = "SemanticCut"; cut = cutToJs c |}
        | AppendError.AppendFailed reason ->
            box {| code = "AppendFailed"; reason = reason |}

    /// Append JS-shaped events to a local store.
    /// Returns `{ ok: true, cuts: [...] }` or `{ ok: false, error: structured }`.
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
            | Error e -> return box {| ok = false; error = appendErrorToJs e |}
        }

    /// The canonical store ref (DURABLE-EVENTS-016).
    let canonicalStoreRef = StoreRef.canonical

    /// Try to read one event by id. Returns the JS-shaped envelope or null.
    let tryEvent (store: IEventStore) (eventId: string) : obj =
        match store.TryEvent(EventId.create eventId) with
        | None -> null
        | Some envelope -> envelopeToJs envelope

    /// Canonical JSON+LF bytes for one event (DURABLE-EVENTS-003).
    let encode (event: obj) : string =
        eventOfJs event |> CanonicalEventCodec.encode

    /// Same EventId + different canonical bytes → identity collision.
    /// Returns `{ ok: true }` or `{ ok: false, error: structured }`.
    let checkIdentity (left: obj) (right: obj) : obj =
        match CanonicalEventCodec.checkIdentity (eventOfJs left) (eventOfJs right) with
        | Ok() -> box {| ok = true |}
        | Error e -> box {| ok = false; error = storageInvalidToJs e |}

    /// Set-union by EventId with identity dedupe. Collision → fail closed.
    /// Returns `{ ok: true, events: [...] }` or `{ ok: false, error: structured }`.
    let mergeByIdentity (events: obj array) : obj =
        let parsed = events |> Array.toList |> List.map eventOfJs

        match CanonicalEventCodec.mergeByIdentity parsed with
        | Ok merged -> box {| ok = true; events = merged |> List.map envelopeToJs |> List.toArray |}
        | Error e -> box {| ok = false; error = storageInvalidToJs e |}

    /// K-way merge of named writer streams. `streams` is a JS array of
    /// `[writerName, events]` pairs. Returns `{ ok, events }` or
    /// `{ ok: false, error }`.
    let merge (streams: obj array) : obj =
        let parsed =
            streams
            |> Array.toList
            |> List.map (fun pair ->
                let arr = unbox<obj array> pair
                let writer = str arr[0]
                let events = unbox<obj array> arr[1] |> Array.toList |> List.map eventOfJs
                writer, events)

        match EventKWayMerge.merge parsed with
        | Ok events -> box {| ok = true; events = events |> List.map envelopeToJs |> List.toArray |}
        | Error e -> box {| ok = false; error = storageInvalidToJs e |}

    /// Heads for a stream. Returns a JS array of event id strings.
    let tryHeads (store: IEventStore) (streamId: string) : string array =
        store.TryHeads(EventStreamId.create streamId)
        |> List.map EventId.value
        |> List.toArray

    /// The single head for a stream, or null if there is not exactly one.
    let tryHead (store: IEventStore) (streamId: string) : obj =
        match store.TryHead(EventStreamId.create streamId) with
        | None -> null
        | Some head -> box (EventId.value head)
