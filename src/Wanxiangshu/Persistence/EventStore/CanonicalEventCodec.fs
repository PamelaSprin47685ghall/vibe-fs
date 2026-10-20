namespace Wanxiangshu.Persistence.EventStore

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// §5.0 canonical event bytes. The single implementation lives in
/// wanxiangli/events (CUT-18); this module is the typed F# projection of that
/// contract over EventEnvelope / StorageInvalid. D-04: this codec serves the
/// single-writer SQLite CAS protocol; multi-writer convergence (CUT-19) is a
/// separate protocol and is not expressed here.
[<RequireQualifiedAccess>]
type StoredLineFault =
    | UnknownEventType of eventType: string
    | MissingPayload of payloadRef: PayloadRef
    | PayloadDigestMismatch of payloadRef: PayloadRef
    | UnreferencedPayload of payloadRef: PayloadRef

module CanonicalEventCodec =

    [<Import("CanonicalEventCodec", "wanxiangli/events")>]
    let private codecClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    // Stateless library object; one shared instance.
    let private codec: obj = construct codecClass

    /// The wanxiangli/events `EventInput` shape of one envelope. Shared by the
    /// facades over wanxiangli libraries that take events (codec, convergence).
    let toLibraryEvent (envelope: EventEnvelope) : obj =
        let payloads =
            if Map.isEmpty envelope.Payloads then
                []
            else
                [ "payloads"
                  ==> (envelope.Payloads
                       |> Map.toList
                       |> List.map (fun (payloadRef, bytes) -> PayloadRef.value payloadRef, box bytes)
                       |> createObj) ]

        createObj (
            [ "id", box (EventId.value envelope.EventId)
              "stream", box (EventStreamId.value envelope.StreamId)
              "type", box envelope.EventType
              "parents", box (envelope.Parents |> List.map EventId.value |> Array.ofList)
              "payload", box envelope.Payload
              "payloadRefs", box (envelope.PayloadRefs |> List.map PayloadRef.value |> Array.ofList) ]
            @ payloads
        )

    let private payloadsOfJs (value: obj) : Map<PayloadRef, byte[]> =
        if isNull value then
            Map.empty
        else
            emitJsExpr value "Object.keys($0).map(function (key) { return [key, $0[key]]; })"
            |> unbox<(string * byte[]) array>
            |> Array.fold (fun acc (key, bytes) -> Map.add (PayloadRef.create key) bytes acc) Map.empty

    let ofLibraryEvent (value: obj) : EventEnvelope =
        { EventId = EventId.create (unbox<string> value?id)
          StreamId = EventStreamId.create (unbox<string> value?stream)
          EventType = unbox<string> value?``type``
          Parents = (unbox<string array> value?parents) |> Array.toList |> List.map EventId.create
          Payload = value?payload
          PayloadRefs =
            (unbox<string array> value?payloadRefs)
            |> Array.toList
            |> List.map PayloadRef.create
          Payloads = payloadsOfJs value?payloads }

    let private errorText (value: obj) : string =
        if isNull value then "" else string value

    /// Map a wanxiangli CodecError-shaped value back to StorageInvalid.
    let invalidOfLibraryError (error: obj) : StorageInvalid =
        match errorText (error?code) with
        | "IdentityCollision" -> StorageInvalid.IdentityCollision(EventId.create (unbox<string> error?eventId))
        | "MissingParent" -> StorageInvalid.MissingParent(EventId.create (unbox<string> error?eventId))
        | "CyclicParents" -> StorageInvalid.CyclicParents
        | "MissingPayload" -> StorageInvalid.MissingPayload(PayloadRef.create (unbox<string> error?payloadRef))
        | "UnknownEventType" -> StorageInvalid.UnknownEventType(unbox<string> error?eventType)
        | "NonCanonical" -> StorageInvalid.NonCanonical(unbox<string> error?reason)
        | _ -> StorageInvalid.MalformedEnvelope(unbox<string> error?reason)

    /// Canonical JSON text including exactly one trailing LF (§5.0).
    let encode (envelope: EventEnvelope) : string =
        unbox<string> (codec?encode (toLibraryEvent envelope))

    /// Same EventId with different canonical bytes → IdentityCollision (§5.3).
    /// Distinct EventIds are not a collision (Ok).
    let checkIdentity (left: EventEnvelope) (right: EventEnvelope) : Result<unit, StorageInvalid> =
        let outcome: obj = codec?checkIdentity (toLibraryEvent left, toLibraryEvent right)

        if unbox<bool> outcome?ok then
            Ok()
        else
            Error(invalidOfLibraryError outcome?error)

    /// Set-union by EventId with identity dedupe. Collision → fail closed.
    /// This is a pure identity utility; writer-stream ordering belongs to EventKWayMerge.
    let mergeByIdentity (events: EventEnvelope list) : Result<EventEnvelope list, StorageInvalid> =
        let outcome: obj =
            codec?mergeByIdentity (events |> List.map toLibraryEvent |> Array.ofList)

        if unbox<bool> outcome?ok then
            Ok((unbox<obj array> outcome?events) |> Array.toList |> List.map ofLibraryEvent)
        else
            Error(invalidOfLibraryError outcome?error)

    /// Decode canonical JSON+LF into EventEnvelope. Re-encode must match (§5.0).
    let tryDecode (text: string) : Result<EventEnvelope, StorageInvalid> =
        let outcome: obj = codec?tryDecode text

        if unbox<bool> outcome?ok then
            Ok(ofLibraryEvent outcome?event)
        else
            Error(invalidOfLibraryError outcome?error)

    let tryDecodeUtf8Text (bytes: byte[]) : Result<string, StorageInvalid> =
        let outcome: obj = codec?tryDecodeUtf8Text bytes

        if unbox<bool> outcome?ok then
            Ok(unbox<string> outcome?text)
        else
            Error(invalidOfLibraryError outcome?error)

    let tryDecodeUtf8 (bytes: byte[]) : Result<EventEnvelope, StorageInvalid> =
        let outcome: obj = codec?tryDecodeUtf8 bytes

        if unbox<bool> outcome?ok then
            Ok(ofLibraryEvent outcome?event)
        else
            Error(invalidOfLibraryError outcome?error)

    let private storedLineFaultOf (error: obj) : StoredLineFault =
        let payloadRef () =
            PayloadRef.create (unbox<string> error?payloadRef)

        match errorText (error?code) with
        | "UnknownEventType" -> StoredLineFault.UnknownEventType(unbox<string> error?eventType)
        | "MissingPayload" -> StoredLineFault.MissingPayload(payloadRef ())
        | "PayloadDigestMismatch" -> StoredLineFault.PayloadDigestMismatch(payloadRef ())
        | "UnreferencedPayload" -> StoredLineFault.UnreferencedPayload(payloadRef ())
        | code -> failwithf "wanxiangli/events checkClosure returned an unknown fault code: %s" code

    /// durable-events-012/027: the one closure rule for stored local lines lives in
    /// wanxiangli/events (CUT-19); this facade only projects its verdict.
    let checkStoredClosure
        (isEventTypeKnown: string -> bool)
        (events: EventEnvelope list)
        : Result<unit, StoredLineFault> =
        let outcome: obj =
            codec?checkClosure (events |> List.map toLibraryEvent |> Array.ofList, isEventTypeKnown)

        if unbox<bool> outcome?ok then
            Ok()
        else
            Error(storedLineFaultOf outcome?error)
