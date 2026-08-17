namespace Wanxiangshu.Persistence.EventStore

open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation.Identity

/// Semantic owner for deterministic k-way ordering of writer streams.
[<RequireQualifiedAccess>]
module EventMergeSurface =

    let private str (value: obj) : string =
        if isNull value then "" else string value

    let private payloadJson (value: obj) : string =
        match value with
        | null -> "null"
        | :? string as text -> text
        | _ -> JS.JSON.stringify value

    let private ids (value: obj) : EventId list =
        if isNull value then
            []
        else
            unbox<string array> value |> Array.toList |> List.map EventId.create

    let private refs (value: obj) : PayloadRef list =
        if isNull value then
            []
        else
            unbox<string array> value |> Array.toList |> List.map PayloadRef.create

    let private eventOfJs (value: obj) : EventEnvelope =
        { EventId = EventId.create (str (value?id))
          StreamId = EventStreamId.create (str (value?stream))
          EventType = str (value?``type``)
          Parents = ids (value?parents)
          Payload = unbox<JsonValue> (JS.JSON.parse (payloadJson (value?payload)))
          PayloadRefs = refs (value?payloadRefs) }
        |> EventEnvelope.normalize

    let private eventToJs (event: EventEnvelope) : obj =
        let payload =
            CanonicalEventCodec.encode event
            |> (fun text -> text.TrimEnd('\n'))
            |> JS.JSON.parse

        box
            {| id = EventId.value event.EventId
               stream = EventStreamId.value event.StreamId
               ``type`` = event.EventType
               parents = event.Parents |> List.map EventId.value |> List.toArray
               payload = payload
               payloadRefs = event.PayloadRefs |> List.map PayloadRef.value |> List.toArray |}

    let private invalidToJs (error: StorageInvalid) : obj =
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

    /// Merge named JS-native writer streams. Writer names only break impossible
    /// duplicate ties; causal readiness and EventId determine the order.
    let merge (streams: obj array) : obj =
        let parsed =
            streams
            |> Array.toList
            |> List.map (fun pair ->
                let values = unbox<obj array> pair
                let writer = str values[0]
                let events = unbox<obj array> values[1] |> Array.toList |> List.map eventOfJs
                writer, events)

        match EventKWayMerge.merge parsed with
        | Ok events ->
            box
                {| ok = true
                   events = events |> List.map eventToJs |> List.toArray |}
        | Error error ->
            box
                {| ok = false
                   error = invalidToJs error |}
