namespace Wanxiangshu.Persistence.EventStore

open Fable.Core

/// Semantic owner for canonical EventEnvelope bytes and identity laws.
[<RequireQualifiedAccess>]
module EventCodecSurface =
    /// Encode one JS-native event as canonical JSON followed by exactly one LF.
    val encode: event: obj -> string

    /// Decode canonical bytes without exposing EventEnvelope or Fable values.
    val decode: bytes: string -> obj

    /// Decode raw UTF-8 bytes to text without parsing an EventEnvelope.
    val decodeUtf8Text: bytes: byte[] -> obj

    /// Decode raw UTF-8 bytes before parsing; malformed byte sequences fail closed.
    val decodeUtf8: bytes: byte[] -> obj

    /// Compare identity bytes for two JS-native events.
    val checkIdentity: left: obj -> right: obj -> obj

    /// Set-union events by EventId with canonical-byte collision detection.
    val mergeByIdentity: events: obj array -> obj
