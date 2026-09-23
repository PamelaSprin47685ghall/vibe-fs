namespace Wanxiangshu.Sphinx.V2.Core

/// A schema reference plus the exact canonical payload bytes it governs.
///
/// The hash is a real SHA-256 of the canonical schema document, not a label such as
/// "sphinx-schema-v2" — WHAT[sphinx-v2-015] requires two different schema documents
/// to be distinguishable by their reference alone, otherwise a schema edit is
/// invisible to the reducer.
type SchemaRef =
    { Id: string
      Hash: string }

/// An immutable, schema-bound payload. `CanonicalPayload` is the canonical JSON text
/// produced from the typed value; the same bytes are what travel on the wire and
/// what participate in hashes, so a round trip cannot silently reorder map keys.
type JsonEnvelope =
    { Schema: SchemaRef
      CanonicalPayload: string }

type EnvelopeError = { Code: string; Message: string }

module JsonEnvelope =
    val tryOfCanonical: SchemaRef -> string -> Result<JsonEnvelope, EnvelopeError>
    val ofCanonical: SchemaRef -> string -> JsonEnvelope
