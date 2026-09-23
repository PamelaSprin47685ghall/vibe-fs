namespace Wanxiangshu.Sphinx.V2.Persistence

open System
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core

/// The v2 batch ↔ canonical envelope codec.
///
/// WHAT[sphinx-v2-019]: one inquiry transition becomes one canonical EventEnvelope.
/// That is a deliberate choice made because the snapshot proved the store is used but
/// did not prove `Append [e1;e2;e3]` is business-atomic. Putting the whole transition in
/// a single envelope makes the unit of durability and the unit of semantics the same
/// thing, so a partial transition can never become the accepted current.
module Codec =

    /// The registered canonical event type. Derived from the Core vocabulary so the
    /// reducer, the codec and the shared whitelist cannot drift apart.
    let transitionEventType = SphinxV2EventTypes.transition

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

    let private inquiryStream (inquiryId: InquiryId) : EventStreamId =
        EventStreamId.create ("sphinx-v2/" + InquiryId.value inquiryId)

    /// The event body tag the Integrator routes on; the payload carries the bytes the
    /// reducer rebuilds the body from.
    let bodyTag (body: InquiryEventBody) : string =
        match body with
        | InquiryEventBody.InquiryCreated _ -> "InquiryCreated"
        | InquiryEventBody.GoalAmended _ -> "GoalAmended"
        | InquiryEventBody.SnapshotRegistered _ -> "SnapshotRegistered"
        | InquiryEventBody.DecisionScopeOpened _ -> "DecisionScopeOpened"
        | InquiryEventBody.RoundOpened _ -> "RoundOpened"
        | InquiryEventBody.WorkPlanned _ -> "WorkPlanned"
        | InquiryEventBody.RoundClosed _ -> "RoundClosed"
        | InquiryEventBody.BudgetReserved _ -> "BudgetReserved"
        | InquiryEventBody.UsageSettled _ -> "UsageSettled"
        | InquiryEventBody.ReservationReleased _ -> "ReservationReleased"
        | InquiryEventBody.UsageOverrunRecorded _ -> "UsageOverrunRecorded"
        | InquiryEventBody.DispatchRequested _ -> "DispatchRequested"
        | InquiryEventBody.DispatchReceiptRecorded _ -> "DispatchReceiptRecorded"
        | InquiryEventBody.WorkAttemptTransitioned _ -> "WorkAttemptTransitioned"
        | InquiryEventBody.HostTerminalRecorded _ -> "HostTerminalRecorded"
        | InquiryEventBody.ResultAccepted _ -> "ResultAccepted"
        | InquiryEventBody.InterpretationPending _ -> "InterpretationPending"
        | InquiryEventBody.InterpretationApplied _ -> "InterpretationApplied"
        | InquiryEventBody.InterpretationFailed _ -> "InterpretFailed"
        | InquiryEventBody.GraphPatched _ -> "GraphPatched"
        | InquiryEventBody.CertificateSlotsPatched _ -> "CertificateSlotsPatched"
        | InquiryEventBody.CertificateInvalidated _ -> "CertificateInvalidated"
        | InquiryEventBody.DecisionRecorded _ -> "DecisionRecorded"
        | InquiryEventBody.AnswerPrepared _ -> "AnswerPrepared"
        | InquiryEventBody.AnswerCommitted _ -> "AnswerCommitted"
        | InquiryEventBody.CancelRequested _ -> "CancelRequested"
        | InquiryEventBody.InquiryCancelled _ -> "InquiryCancelled"
        | InquiryEventBody.InquirySuspended _ -> "InquirySuspended"
        | InquiryEventBody.InquiryFailed _ -> "InquiryFailed"
        | InquiryEventBody.InquiryStatusChanged _ -> "InquiryStatusChanged"

    /// Each body's canonical bytes are carried through so the Integrator can rebuild
    /// exactly what the Runtime produced.
    let private bodyCanonical (body: InquiryEventBody) : string =
        let payload = body |> box |> unbox<obj>
        CanonicalJson.canonicalJson payload

    /// The wire form of a batch.
    let toWire (batch: TransitionBatch) : TransitionBatchWire =
        { SchemaVersion = batch.SchemaVersion
          Inquiry = InquiryId.value batch.InquiryId
          PreviousRevision = string (Revision.value batch.PreviousRevision)
          PreviousHead = batch.PreviousHead |> Option.map Wanxiangshu.Sphinx.V2.Core.EventId.value
          Revision = string (Revision.value batch.Revision)
          CommandId = batch.CommandId
          CommandFingerprint = batch.CommandFingerprint
          PostStateFingerprint = batch.PostStateFingerprint
          Events = batch.Events |> List.map (fun body -> { Tag = bodyTag body; Payload = bodyCanonical body }) }

    /// Encodes one transition as one canonical envelope. The envelope id is derived
    /// from the inquiry, the revision and the command, so the same transition always
    /// produces the same id — which makes an append retry idempotent at the store.
    let encode
        (digest: string -> string)
        (batch: TransitionBatch)
        (previousHead: Identity.EventId option)
        : EventEnvelope =
        let wire = toWire batch

        let payloadText = CanonicalJson.canonicalJson wire

        let derivation =
            {| inquiry = wire.Inquiry
               revision = wire.Revision
               commandId = wire.CommandId
               commandFingerprint = wire.CommandFingerprint |}

        let eventId =
            Identity.EventId.create ("ev" + digest (CanonicalJson.canonicalJson derivation))

        // The payload is canonical JSON already; parsing it back is exact.
        let payloadValue = emitJsExpr payloadText "JSON.parse($0)" |> unbox<JsonValue>

        { EventId = eventId
          StreamId = inquiryStream batch.InquiryId
          EventType = transitionEventType
          Parents = previousHead |> Option.toList
          Payload = payloadValue
          PayloadRefs = [] }
