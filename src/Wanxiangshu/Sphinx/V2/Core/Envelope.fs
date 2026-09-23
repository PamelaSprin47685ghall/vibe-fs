namespace Wanxiangshu.Sphinx.V2.Core

open System

/// A schema reference plus the exact canonical payload bytes it governs.
///
/// WHAT[sphinx-v2-015]: the hash is a real SHA-256 of the canonical schema document,
/// not a label such as "sphinx-schema-v2". Two different schema documents must be
/// distinguishable by their reference alone, otherwise a schema edit is invisible to
/// the reducer and to replay.
type SchemaRef = { Id: string; Hash: string }

/// An immutable, schema-bound payload. `CanonicalPayload` is the canonical JSON text
/// produced from the typed value; the same bytes travel on the wire and participate
/// in hashes, so a round trip cannot silently reorder map keys.
type JsonEnvelope =
    { Schema: SchemaRef
      CanonicalPayload: string }

type EnvelopeError = { Code: string; Message: string }

module Envelope =

    let private isTrimmedNonBlank (value: string) =
        not (String.IsNullOrWhiteSpace value) && value.Trim() = value

    let tryCreate id hash : Result<SchemaRef, EnvelopeError> =
        if not (isTrimmedNonBlank id) then
            Error
                { Code = "invalid-schema-ref"
                  Message = "schema id must be a non-blank trimmed string" }
        elif not (isTrimmedNonBlank hash) then
            Error
                { Code = "invalid-schema-ref"
                  Message = "schema hash must be a non-blank trimmed string" }
        else
            Ok { Id = id; Hash = hash }

    let create id hash : SchemaRef =
        match tryCreate id hash with
        | Ok schema -> schema
        | Error message -> invalidArg (nameof hash) message.Message

module JsonEnvelope =

    /// Canonical payload bytes are accepted verbatim. The reducer never re-encodes a
    /// payload it received: re-encoding is exactly where a map key order or a float
    /// spelling would drift under the same reference.
    let tryOfCanonical (schema: SchemaRef) (canonicalPayload: string) : Result<JsonEnvelope, EnvelopeError> =
        if String.IsNullOrWhiteSpace canonicalPayload then
            Error
                { Code = "invalid-envelope"
                  Message = "canonical payload must not be blank" }
        else
            Ok
                { Schema = schema
                  CanonicalPayload = canonicalPayload }

    let ofCanonical (schema: SchemaRef) (canonicalPayload: string) : JsonEnvelope =
        match tryOfCanonical schema canonicalPayload with
        | Ok envelope -> envelope
        | Error message -> invalidArg (nameof canonicalPayload) message.Message
