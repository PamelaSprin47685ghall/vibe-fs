namespace Wanxiangshu.Sphinx.V2.Persistence

open System
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core

/// The Sphinx v2 canonical integration rule.
///
/// WHAT[sphinx-v2-009]: this is the only v2 fold. It reads accepted envelopes, decodes
/// each transition batch, runs the single Core reducer, and publishes the resulting
/// state as `Current`. There is no second history: the old results-only registry, the
/// stage cache and the caller-held Current are all gone, and a handle is a durable
/// reference rather than a parallel copy.
///
/// WHAT[sphinx-v2-016]: the fold is deterministic. Replaying the same envelope sequence
/// always produces the same state, so the `Current` here is exactly what a cold restart
/// rebuilds — no network, no fresh randomness, no model call.

[<RequireQualifiedAccess>]
module Integrator =

    /// The `Current` key v2 inquiries are published under.
    let currentKey = "SphinxV2"

    /// The published state: one inquiry state per inquiry id.
    let private empty: Map<InquiryId, InquiryState> = Map.empty

    let private eventWireDecoder: Decoder<Codec.EventBodyWire> =
        Decode.object (fun get ->
            { Codec.EventBodyWire.Tag = get.Required.Field "tag" Decode.string
              Payload = get.Required.Field "payload" Decode.string })

    let private wireDecoder: Decoder<Codec.TransitionBatchWire> =
        Decode.object (fun get ->
            { Codec.TransitionBatchWire.SchemaVersion = get.Required.Field "schemaVersion" Decode.string
              Inquiry = get.Required.Field "inquiry" Decode.string
              PreviousRevision = get.Required.Field "previousRevision" Decode.string
              PreviousHead = get.Optional.Field "previousHead" Decode.string
              Revision = get.Required.Field "revision" Decode.string
              CommandId = get.Required.Field "commandId" Decode.string
              CommandFingerprint = get.Required.Field "commandFingerprint" Decode.string
              PostStateFingerprint = get.Optional.Field "postStateFingerprint" Decode.string
              Events = get.Required.Field "events" (Decode.list eventWireDecoder) })

    /// Decodes the batch payload. The payload is the canonical JSON the Codec produced,
    /// so parsing it is exact.
    let tryDecodeWire (envelope: EventEnvelope) : Result<Codec.TransitionBatchWire, string> =
        let text = CanonicalJson.canonicalJson envelope.Payload

        match Decode.fromString wireDecoder text with
        | Ok wire -> Ok wire
        | Error error -> Error(sprintf "sphinx v2 batch decode failed: %s" error)

    /// Rebuilds one typed event body from its wire form.
    ///
    /// Only the bodies the fold can reconstruct from durable bytes are handled. An
    /// unknown tag is a refusal: guessing a body would let a replayed batch diverge
    /// from the transition that was actually recorded.
    let bodyOf (wire: Codec.EventBodyWire) : InquiryEventBody =
        let textOf (value: JsonValue) : string =
            match Decode.fromValue "body-field" Decode.string value with
            | Ok decoded -> decoded
            | Error _ -> ""

        let payloadOf () =
            Decode.fromString Decode.value wire.Payload

        match wire.Tag, payloadOf () with
        | "CancelRequested", Ok value -> InquiryEventBody.CancelRequested(textOf value)
        | "InquiryCancelled", Ok value -> InquiryEventBody.InquiryCancelled(textOf value)
        | "InquirySuspended", Ok value -> InquiryEventBody.InquirySuspended(textOf value)
        | "InquiryFailed", Ok value -> InquiryEventBody.InquiryFailed(textOf value)
        | other, _ -> failwith (sprintf "v2 body %s requires the sealed typed payload" other)

    /// The batch's typed events, one per wire body in the order the Runtime sealed them.
    let typedEvents (envelope: EventEnvelope) (wire: Codec.TransitionBatchWire) : InquiryEvent list =
        wire.Events
        |> List.mapi (fun index body ->
            { Id = Wanxiangshu.Sphinx.V2.Core.EventId.create (Identity.EventId.value envelope.EventId)
              InquiryId = InquiryId.create wire.Inquiry
              Revision = Revision.create (int64 index)
              Parent = None
              BatchIndex = index
              Body = bodyOf body })

    /// Folds a batch onto its own history. A continuation and a creation go through the
    /// same reducer; the only difference is whether a prior state exists to continue.
    let private replayBatch (prior: InquiryState option) (events: InquiryEvent list) : Result<InquiryState, string> =
        let step (state: InquiryState option) (event: InquiryEvent) : Result<InquiryState option, string> =
            let advanced =
                match state with
                | Some applied ->
                    { event with
                        Revision = Revision.next applied.Revision }
                | None -> event

            match Reducer.apply state advanced with
            | Ok next -> Ok(Some next)
            | Error fault -> Error fault.Message

        match
            events
            |> List.fold (fun state event -> Result.bind (fun carried -> step carried event) state) (Ok prior)
        with
        | Ok(Some folded) -> Ok folded
        | Ok None -> Error "transition batch produced no state"
        | Error fault -> Error fault

    /// Folds one envelope into the published state.
    let private integrateOne
        (states: Map<InquiryId, InquiryState>)
        (envelope: EventEnvelope)
        : Result<Map<InquiryId, InquiryState>, string> =
        match tryDecodeWire envelope with
        | Error reason -> Error reason
        | Ok wire ->
            let inquiryId = InquiryId.create wire.Inquiry
            let prior = states |> Map.tryFind inquiryId
            let events = typedEvents envelope wire

            replayBatch prior events
            |> Result.map (fun folded -> states |> Map.add inquiryId folded)

    /// The v2 integration rule.
    let rule: IntegrationRule =
        { Name = "SphinxV2"
          Initial = box empty
          FaultScope = fun _ -> "global"
          Accepts = fun envelope -> envelope.EventType = Codec.transitionEventType
          Integrate =
            fun current envelope ->
                let states = unbox<Map<InquiryId, InquiryState>> current
                integrateOne states envelope |> Result.map box
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    /// Reads one published inquiry state. `None` means the inquiry is not in the durable
    /// record; it never means "empty inquiry".
    let tryState (current: obj) (inquiryId: InquiryId) : InquiryState option =
        match current with
        | null -> None
        | _ -> unbox<Map<InquiryId, InquiryState>> current |> Map.tryFind inquiryId
