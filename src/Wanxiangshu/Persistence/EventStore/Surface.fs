namespace Wanxiangshu.Persistence.EventStore

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation.Identity

/// Process-local EventStore owner surface. JS callers receive unprefixed
/// operations; EventStoreHandle remains an opaque capability.
module Surface =

    let private str (value: obj) : string =
        if isNull value then "" else string value

    let private payloadJson (value: obj) : string =
        match value with
        | null -> "null"
        | :? string as text -> text
        | _ -> JS.JSON.stringify value

    let private parentIds (value: obj) : EventId list =
        if isNull value then
            []
        else
            unbox<string array> value |> Array.toList |> List.map EventId.create

    let private payloadRefsOf (value: obj) : PayloadRef list =
        if isNull value then
            []
        else
            unbox<string array> value |> Array.toList |> List.map PayloadRef.create

    let private eventOfJs (value: obj) : EventEnvelope =
        { EventId = EventId.create (str (value?id))
          StreamId = EventStreamId.create (str (value?stream))
          EventType = str (value?``type``)
          Parents = parentIds (value?parents)
          Payload = unbox<JsonValue> (JS.JSON.parse (payloadJson (value?payload)))
          PayloadRefs = payloadRefsOf (value?payloadRefs) }
        |> EventEnvelope.normalize

    let private envelopeToJs (envelope: EventEnvelope) : obj =
        let payloadObject =
            CanonicalEventCodec.encode envelope
            |> (fun text -> text.TrimEnd('\n'))
            |> JS.JSON.parse
            |> fun eventObject -> eventObject?payload

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
        | StorageInvalid.IdentityCollision eventId ->
            box
                {| code = "IdentityCollision"
                   eventId = EventId.value eventId |}
        | StorageInvalid.NonCanonical reason ->
            box
                {| code = "NonCanonical"
                   reason = reason |}
        | StorageInvalid.MalformedEnvelope reason ->
            box
                {| code = "MalformedEnvelope"
                   reason = reason |}
        | StorageInvalid.MissingParent eventId ->
            box
                {| code = "MissingParent"
                   eventId = EventId.value eventId |}
        | StorageInvalid.CyclicParents -> box {| code = "CyclicParents" |}
        | StorageInvalid.MissingPayload payloadRef ->
            box
                {| code = "MissingPayload"
                   payloadRef = PayloadRef.value payloadRef |}
        | StorageInvalid.UnknownEventType eventType ->
            box
                {| code = "UnknownEventType"
                   eventType = eventType |}

    let private appendErrorToJs (error: AppendError) : obj =
        match error with
        | AppendError.StorageInvalid invalid ->
            box
                {| code = "StorageInvalid"
                   error = storageInvalidToJs invalid |}
        | AppendError.SemanticCut cut ->
            box
                {| code = "SemanticCut"
                   cut = cutToJs cut |}
        | AppendError.AppendFailed reason ->
            box
                {| code = "AppendFailed"
                   reason = reason |}

    /// Create a process-local writer capability. The caller owns its lifecycle.
    let create (commonDir: string, writerId: string) : EventStoreHandle =
        EventStoreHandle.Create(EventStore.createLocal commonDir writerId (CanonicalIntegrator.create ()))

    /// Release a writer capability. Further operations fail rather than using a
    /// stale resource.
    let dispose (handle: EventStoreHandle) : unit = handle.Dispose()

    /// Append JS-native events and return only the durable receipt.
    let append (handle: EventStoreHandle, events: obj array) : Task<obj> =
        task {
            let parsed = events |> Array.toList |> List.map eventOfJs
            let! result = handle.Store.Append parsed

            return
                match result with
                | Ok receipt ->
                    box
                        {| ok = true
                           cuts = receipt.Cuts |> List.map cutToJs |> List.toArray |}
                | Error error ->
                    box
                        {| ok = false
                           error = appendErrorToJs error |}
        }

    /// Read one durable event by identity. A missing event is `null`.
    let read (handle: EventStoreHandle, eventId: string) : obj =
        match handle.Store.TryEvent(EventId.create eventId) with
        | None -> null
        | Some envelope -> envelopeToJs envelope

    /// Read all structural heads for one stream.
    let heads (handle: EventStoreHandle, streamId: string) : string array =
        handle.Store.TryHeads(EventStreamId.create streamId)
        |> List.map EventId.value
        |> List.toArray

    /// Read the unique structural head, or `null` when the stream is forked/empty.
    let head (handle: EventStoreHandle, streamId: string) : obj =
        match handle.Store.TryHead(EventStreamId.create streamId) with
        | None -> null
        | Some eventId -> box (EventId.value eventId)

    /// The canonical remote store ref owned by persistence infrastructure.
    let canonicalStoreRef = StoreRef.canonical
