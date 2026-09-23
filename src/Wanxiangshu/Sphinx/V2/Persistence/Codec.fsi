namespace Wanxiangshu.Sphinx.V2.Persistence

open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core

/// The v2 batch ↔ canonical envelope codec.
///
/// WHAT[sphinx-v2-019]: one inquiry transition becomes one canonical EventEnvelope.
/// That is a deliberate choice made because the snapshot proved the store is used but
/// did not prove `Append [e1; e2; e3]` is business-atomic. Putting the whole transition
/// in a single envelope makes the unit of durability and the unit of semantics the same
/// thing, so a partial transition can never become the accepted current.
module Codec =

    /// The registered canonical event type. Only this type is accepted by the v2 rule.
    val transitionEventType: string

    /// The wire form of one event body. Tag and payload are separate so the Integrator
    /// can route without re-deriving which event it is.
    type EventBodyWire = { Tag: string; Payload: string }

    /// The wire form of one transition batch.
    type TransitionBatchWire =
        { SchemaVersion: string
          Inquiry: string
          PreviousRevision: string
          PreviousHead: string option
          Revision: string
          CommandId: string
          CommandFingerprint: string
          PostStateFingerprint: string option
          Events: EventBodyWire list }

    /// The event body tag the Integrator routes on.
    val bodyTag: InquiryEventBody -> string

    /// The wire form of a batch.
    val toWire: TransitionBatch -> TransitionBatchWire

    /// Encodes one transition as one canonical envelope. The same transition always
    /// yields the same envelope id, so a retried append is idempotent at the store.
    val encode: (string -> string) -> TransitionBatch -> Wanxiangshu.Foundation.Identity.EventId option -> EventEnvelope
