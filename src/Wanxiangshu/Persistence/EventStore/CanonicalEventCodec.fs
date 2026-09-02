namespace Wanxiangshu.Persistence.EventStore

open System.Text
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation.Identity

/// §5.0 canonical event bytes: UTF-8, no BOM, single trailing LF,
/// recursive Unicode-codepoint key order, parents / payload_refs set-normalized.
module CanonicalEventCodec =

    /// Persist-owned canonical store ref (refs/wanxiang/store).
    let canonicalStoreRef = StoreRef.canonical

    let private normalizeJson (value: obj) : obj =
        emitJsExpr
            value
            """
            (function (value) {
                function normalize(current) {
                    if (Array.isArray(current)) {
                        return current.map(normalize);
                    }

                    if (current !== null && typeof current === "object") {
                        const result = {};
                        Object.keys(current).sort().forEach(function (key) {
                            result[key] = normalize(current[key]);
                        });
                        return result;
                    }

                    return current;
                }

                return normalize(value);
            })($0)
            """

    let private envelopeObject (envelope: EventEnvelope) : obj =
        let normalized = EventEnvelope.normalize envelope

        createObj
            [ "event_id" ==> EventId.value normalized.EventId
              "stream_id" ==> EventStreamId.value normalized.StreamId
              "event_type" ==> normalized.EventType
              "parents" ==> (normalized.Parents |> List.map EventId.value |> Array.ofList)
              "payload" ==> normalized.Payload
              "payload_refs"
              ==> (normalized.PayloadRefs |> List.map PayloadRef.value |> Array.ofList) ]

    /// Canonical JSON text including exactly one trailing LF (§5.0).
    let encode (envelope: EventEnvelope) : string =
        let json = JS.JSON.stringify (normalizeJson (envelopeObject envelope))
        json + "\n"

    /// Same EventId with different canonical bytes → IdentityCollision (§5.3).
    /// Distinct EventIds are not a collision (Ok).
    let checkIdentity (left: EventEnvelope) (right: EventEnvelope) : Result<unit, StorageInvalid> =
        if left.EventId <> right.EventId then Ok()
        elif encode left = encode right then Ok()
        else Error(StorageInvalid.IdentityCollision left.EventId)

    let private mergeOne (head: EventEnvelope) (tail: EventEnvelope list) (acc: Map<string, EventEnvelope * string>) =
        let normalized = EventEnvelope.normalize head
        let id = EventId.value normalized.EventId
        let bytes = encode normalized

        match Map.tryFind id acc with
        | Some(_, existingBytes) when existingBytes = bytes -> Ok(tail, acc)
        | Some _ -> Error(StorageInvalid.IdentityCollision normalized.EventId)
        | None -> Ok(tail, Map.add id (normalized, bytes) acc)

    /// Set-union by EventId with identity dedupe. Collision → fail closed.
    /// This is a pure identity utility; writer-stream ordering belongs to EventKWayMerge.
    let mergeByIdentity (events: EventEnvelope list) : Result<EventEnvelope list, StorageInvalid> =
        let rec loop remaining (acc: Map<string, EventEnvelope * string>) =
            match remaining with
            | [] ->
                acc
                |> Map.toList
                |> List.sortBy fst
                |> List.map (fun (_, (envelope, _)) -> envelope)
                |> Ok
            | head :: tail -> mergeOne head tail acc |> Result.bind (fun (next, updated) -> loop next updated)

        loop events Map.empty

    [<Emit("""
    (function (value) {
      function orderedTree(current) {
        if (Array.isArray(current)) {
          for (let i = 0; i < current.length; i += 1) {
            if (!orderedTree(current[i])) return false;
          }
          return true;
        }

        if (current !== null && typeof current === 'object') {
          const keys = Object.keys(current);
          for (let i = 1; i < keys.length; i += 1) {
            if (keys[i - 1] > keys[i]) return false;
          }
          for (const key of keys) {
            if (!orderedTree(current[key])) return false;
          }
        }
        return true;
      }

      function sortedUnique(values) {
        for (let i = 1; i < values.length; i += 1) {
          if (!(values[i - 1] < values[i])) return false;
        }
        return true;
      }

      const allowed = new Set(['event_id', 'event_type', 'parents', 'payload', 'payload_refs', 'stream_id']);
      return Object.keys(value).every(key => allowed.has(key))
        && orderedTree(value)
        && sortedUnique(value.parents)
        && sortedUnique(value.payload_refs);
    })($0)
    """)>]
    let private hasCanonicalStructure (parsed: obj) : bool = jsNative

    [<Emit("JSON.stringify($0) + '\\n' === $1")>]
    let private hasCanonicalJsonText (parsed: obj) (text: string) : bool = jsNative

    let private ensureCanonicalParsed (text: string) (parsed: obj) =
        if hasCanonicalStructure parsed && hasCanonicalJsonText parsed text then
            Ok()
        else
            Error(StorageInvalid.NonCanonical "event bytes are not §5.0 canonical")

    let private decodeParsed (text: string) (parsed: obj) : Result<EventEnvelope, StorageInvalid> =
        let hasShape: bool =
            emitJsExpr
                parsed
                "!!$0 && typeof $0 === 'object' && typeof $0.event_id === 'string' && typeof $0.stream_id === 'string' && typeof $0.event_type === 'string' && Array.isArray($0.parents) && Array.isArray($0.payload_refs)"

        if not hasShape then
            Error(StorageInvalid.MalformedEnvelope "event JSON missing required fields")
        else
            ensureCanonicalParsed text parsed
            |> Result.map (fun () ->
                { EventId = EventId.create (unbox<string> parsed?event_id)
                  StreamId = EventStreamId.create (unbox<string> parsed?stream_id)
                  EventType = unbox<string> parsed?event_type
                  Parents = (unbox<string[]> parsed?parents) |> Array.toList |> List.map EventId.create
                  Payload = parsed?payload
                  PayloadRefs =
                    (unbox<string[]> parsed?payload_refs)
                    |> Array.toList
                    |> List.map PayloadRef.create })

    let private tryDecodeCanonical (text: string) : Result<EventEnvelope, StorageInvalid> =
        try
            let body = text.Substring(0, text.Length - 1)
            let parsed = JS.JSON.parse body
            decodeParsed text parsed
        with ex ->
            Error(StorageInvalid.MalformedEnvelope ex.Message)

    /// Decode canonical JSON+LF into EventEnvelope. Re-encode must match (§5.0).
    let tryDecode (text: string) : Result<EventEnvelope, StorageInvalid> =
        if isNull text then
            Error(StorageInvalid.MalformedEnvelope "null event text")
        elif not (text.EndsWith("\n")) || text.EndsWith("\n\n") then
            Error(StorageInvalid.NonCanonical "event bytes must end with exactly one LF")
        else
            tryDecodeCanonical text

    let tryDecodeUtf8 (bytes: byte[]) : Result<EventEnvelope, StorageInvalid> =
        tryDecode (Encoding.UTF8.GetString bytes)
