namespace Wanxiangshu.Persistence.Journal

open System

/// JS-native owner surface for decode-only journal fact compatibility.
/// Decoded facts remain inside the production codec; callers observe bytes and
/// explicit decode outcomes only.
[<RequireQualifiedAccess>]
module FactCodecSurface =
    /// Legacy pre-0.5.0 migration marker message.
    val pre050MigrationMessage: string

    /// Detect legacy fallback fields in a canonical fact line.
    val containsLegacyFallbackFields: line: string -> bool

    /// Detect legacy score-vector entry in a canonical fact line.
    val containsLegacyScoreVectorEntry: line: string -> bool

    /// Encode one JS-native fact to canonical fact bytes.
    val encode: fact: obj -> string

    /// Decode one line and return normalized bytes plus its semantic case.
    val decode: line: string -> obj
