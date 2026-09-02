namespace Wanxiangshu.Persistence.Journal

open System
open Fable.Core

/// JS-native owner surface for Journal.Envelope ↔ universal EventEnvelope codec.
/// No Journal DU, typed ID, or Fable collection crosses this boundary.
[<RequireQualifiedAccess>]
module JournalCodecSurface =
    /// Canonical event type used for journal envelope events.
    val JournalEnvelopeEventType: string

    /// Encode one JS-native journal envelope into a JS-native event object.
    val encode: parents: string array -> payloadRefs: string array -> envelope: obj -> obj

    /// Decode one JS-native event object into a normalized journal envelope.
    val decode: event: obj -> obj

    /// Encode a JS stream descriptor into a canonical stream id string.
    val encodeStreamId: stream: obj -> string

    /// Decode a canonical stream id string into a JS stream descriptor.
    val decodeStreamId: streamId: string -> obj

    /// Serialize a JS-native envelope into canonical journal bytes.
    val serialize: envelope: obj -> string

    /// Deserialize canonical journal bytes into a JS result object.
    val deserialize: line: string -> obj

    /// Compare two JS-native envelopes by canonical sort key.
    val compareSortKey: left: obj -> right: obj -> int

    /// K-way merge of multiple JS-native envelope streams.
    val kWayMerge: streams: obj array -> obj array

    /// Decode one JS-native event object into a normalized journal envelope.
    val decodeEventEnvelope: event: obj -> obj
