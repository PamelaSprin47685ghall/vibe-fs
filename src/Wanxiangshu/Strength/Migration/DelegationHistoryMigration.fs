namespace Wanxiangshu.Strength.Migration

open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence

type LegacyEnvelope =
    { EventId: string
      SourceStreamId: string
      EventType: string
      DecisionId: string
      BudgetEvidence: string option
      TargetProviderRun: string option
      FrameDigest: string option
      ByteLength: int option
      TracedStartInclusive: int64 option
      TracedEndExclusive: int64 option
      MaterialPayloads: string array }

type ImportFact =
    { DecisionId: string
      SourceStreamId: string
      SourceEventId: string
      ImportId: string
      OldBudgetEvidence: string option
      OutcomeKind: string
      TargetProviderRun: string option
      FrameDigest: string option
      ByteLength: int option
      TracedStartInclusive: int64 option
      TracedEndExclusive: int64 option
      MaterialPayloads: string array
      RelinquishReason: string option }

/// Decoder-level partial view of one legacy envelope payload: the fields that
/// live inside the payload object. Envelope identity (event id, stream, type)
/// and material refs come from the enclosing envelope JSON, not from here.
type internal LegacyPayload =
    { DecisionId: string
      BudgetEvidence: string option
      TargetProviderRun: string option
      FrameDigest: string option
      ByteLength: int option
      TracedStartInclusive: int64 option
      TracedEndExclusive: int64 option }

[<RequireQualifiedAccess>]
module DelegationHistoryMigration =

    let deriveImportId (sha256: string -> string) (sourceEventId: string) (contractRevision: int) : string =
        String.concat "\u001f" [ "delegation-import-v1"; sourceEventId; string contractRevision ]
        |> sha256

    let private envelopeDecoder =
        Decode.object (fun get ->
            get.Required.Field "id" Decode.string,
            get.Required.Field "stream" Decode.string,
            get.Required.Field "type" Decode.string)

    let private preparedEnvelopeDecoder =
        Decode.object (fun get ->
            get.Required.Field "id" Decode.string,
            get.Required.Field "stream" Decode.string,
            get.Required.Field "payloadRefs" (Decode.list Decode.string),
            get.Required.Field
                "payload"
                (Decode.object (fun payload ->
                    { DecisionId = payload.Required.Field "decision_id" Decode.string
                      BudgetEvidence = payload.Optional.Field "budget" Decode.string
                      TargetProviderRun = Some(payload.Required.Field "target_provider_run" Decode.string)
                      FrameDigest = Some(payload.Required.Field "frame_digest" Decode.string)
                      ByteLength = Some(payload.Required.Field "byte_length" Decode.int)
                      TracedStartInclusive = None
                      TracedEndExclusive = None })))

    let private promotedEnvelopeDecoder =
        Decode.object (fun get ->
            get.Required.Field "id" Decode.string,
            get.Required.Field "stream" Decode.string,
            get.Required.Field "payloadRefs" (Decode.list Decode.string),
            get.Required.Field
                "payload"
                (Decode.object (fun payload ->
                    { DecisionId = payload.Required.Field "decision_id" Decode.string
                      BudgetEvidence = None
                      TargetProviderRun = Some(payload.Required.Field "target_provider_run" Decode.string)
                      FrameDigest = Some(payload.Required.Field "frame_digest" Decode.string)
                      ByteLength = None
                      TracedStartInclusive = None
                      TracedEndExclusive = None })))

    let private tracedEnvelopeDecoder =
        Decode.object (fun get ->
            get.Required.Field "id" Decode.string,
            get.Required.Field "stream" Decode.string,
            get.Required.Field "payloadRefs" (Decode.list Decode.string),
            get.Required.Field
                "payload"
                (Decode.object (fun payload ->
                    { DecisionId = payload.Required.Field "decision_id" Decode.string
                      BudgetEvidence = None
                      TargetProviderRun = None
                      FrameDigest = None
                      ByteLength = None
                      TracedStartInclusive = Some(payload.Required.Field "start_inclusive" Decode.int64)
                      TracedEndExclusive = Some(payload.Required.Field "end_exclusive" Decode.int64) })))

    let private abandonedEnvelopeDecoder =
        Decode.object (fun get ->
            get.Required.Field "id" Decode.string,
            get.Required.Field "stream" Decode.string,
            get.Required.Field "payloadRefs" (Decode.list Decode.string),
            get.Required.Field
                "payload"
                (Decode.object (fun payload ->
                    { DecisionId = payload.Required.Field "decision_id" Decode.string
                      BudgetEvidence = None
                      TargetProviderRun = Some(payload.Required.Field "target_provider_run" Decode.string)
                      FrameDigest = None
                      ByteLength = None
                      TracedStartInclusive = None
                      TracedEndExclusive = None })))

    let private toEnvelope
        (eventType: string)
        (eventId: string)
        (streamId: string)
        (payload: LegacyPayload)
        (refs: string array)
        =
        { EventId = eventId
          SourceStreamId = streamId
          EventType = eventType
          DecisionId = payload.DecisionId
          BudgetEvidence = payload.BudgetEvidence
          TargetProviderRun = payload.TargetProviderRun
          FrameDigest = payload.FrameDigest
          ByteLength = payload.ByteLength
          TracedStartInclusive = payload.TracedStartInclusive
          TracedEndExclusive = payload.TracedEndExclusive
          MaterialPayloads = refs }

    let private tryDecodePayload preparedOrPromoted envelopeJson eventType =
        match Decode.fromString preparedOrPromoted envelopeJson with
        | Ok(eventId, streamId, refs, payload) ->
            Some(toEnvelope eventType eventId streamId payload (refs |> List.toArray))
        | Error _ -> None

    let private decodeLegacyPayload eventType envelopeJson =
        match eventType with
        | "StrengthCandidatePrepared" -> tryDecodePayload preparedEnvelopeDecoder envelopeJson eventType
        | "StrengthCandidatePromoted" -> tryDecodePayload promotedEnvelopeDecoder envelopeJson eventType
        | "StrengthFramesTraced" -> tryDecodePayload tracedEnvelopeDecoder envelopeJson eventType
        | "StrengthCandidateAbandoned" -> tryDecodePayload abandonedEnvelopeDecoder envelopeJson eventType
        | _ -> None

    let readLegacyEnvelope (envelopeJson: string) : LegacyEnvelope option =
        match Decode.fromString envelopeDecoder envelopeJson with
        | Error _ -> None
        | Ok(_, _, eventType) -> decodeLegacyPayload eventType envelopeJson

    let private resolveTracedBounds (traced: LegacyEnvelope option) =
        match traced with
        | Some value -> value.TracedStartInclusive, value.TracedEndExclusive
        | None -> None, None

    let private planPreparedImport
        baseFact
        (envelope: LegacyEnvelope)
        (promoted: LegacyEnvelope option)
        tracedStart
        tracedEnd
        =
        match promoted with
        | Some _ ->
            { baseFact with
                OutcomeKind = "adopted"
                TargetProviderRun = envelope.TargetProviderRun
                FrameDigest = envelope.FrameDigest
                ByteLength = envelope.ByteLength
                MaterialPayloads = envelope.MaterialPayloads
                TracedStartInclusive = tracedStart
                TracedEndExclusive = tracedEnd }
        | None ->
            { baseFact with
                RelinquishReason = Some "prepared-without-promotion"
                TargetProviderRun = envelope.TargetProviderRun }

    let private planTracedImport
        baseFact
        (envelope: LegacyEnvelope)
        (promoted: LegacyEnvelope option)
        preparedByteLength
        =
        match promoted with
        | Some promotedEnvelope ->
            { baseFact with
                OutcomeKind = "adopted"
                TargetProviderRun = promotedEnvelope.TargetProviderRun
                FrameDigest = promotedEnvelope.FrameDigest
                ByteLength = preparedByteLength
                MaterialPayloads = promotedEnvelope.MaterialPayloads
                TracedStartInclusive = envelope.TracedStartInclusive
                TracedEndExclusive = envelope.TracedEndExclusive }
        | None -> failwith "legacy FramesTraced without its Promoted material inside the retained window"

    let private planEnvelopeImport
        baseFact
        (envelope: LegacyEnvelope)
        (promoted: LegacyEnvelope option)
        preparedByteLength
        tracedStart
        tracedEnd
        =
        match envelope.EventType with
        | "StrengthCandidatePrepared" -> planPreparedImport baseFact envelope promoted tracedStart tracedEnd
        | "StrengthCandidatePromoted" ->
            { baseFact with
                OutcomeKind = "adopted"
                TargetProviderRun = envelope.TargetProviderRun
                FrameDigest = envelope.FrameDigest
                ByteLength = preparedByteLength
                MaterialPayloads = envelope.MaterialPayloads
                TracedStartInclusive = tracedStart
                TracedEndExclusive = tracedEnd }
        | "StrengthFramesTraced" -> planTracedImport baseFact envelope promoted preparedByteLength
        | "StrengthCandidateAbandoned" ->
            { baseFact with
                RelinquishReason = Some "abandoned-before-promotion"
                TargetProviderRun = envelope.TargetProviderRun }
        | other -> failwith (sprintf "unexpected legacy envelope type: %s" other)

    let planDecision
        (sha256: string -> string)
        (contractRevision: int)
        (envelopes: LegacyEnvelope array)
        : ImportFact array =
        if envelopes.Length = 0 then
            Array.empty
        else
            let promoted =
                envelopes
                |> Array.tryFind (fun envelope -> envelope.EventType = "StrengthCandidatePromoted")

            let traced =
                envelopes
                |> Array.tryFind (fun envelope -> envelope.EventType = "StrengthFramesTraced")

            let prepared =
                envelopes
                |> Array.tryFind (fun envelope -> envelope.EventType = "StrengthCandidatePrepared")

            let budgetEvidence =
                prepared |> Option.bind (fun envelope -> envelope.BudgetEvidence)

            /// The frame material length lives only on the decision's Prepared
            /// envelope; Promoted and Traced inherit it so an adopted import
            /// carries the same complete material whichever legacy event of
            /// the decision it was planned from.
            let preparedByteLength =
                prepared |> Option.bind (fun envelope -> envelope.ByteLength)

            let tracedStart, tracedEnd = resolveTracedBounds traced

            let import (envelope: LegacyEnvelope) : ImportFact =
                let baseFact =
                    { DecisionId = envelope.DecisionId
                      SourceStreamId = envelope.SourceStreamId
                      SourceEventId = envelope.EventId
                      ImportId = deriveImportId sha256 envelope.EventId contractRevision
                      OldBudgetEvidence = budgetEvidence
                      OutcomeKind = "relinquished"
                      TargetProviderRun = None
                      FrameDigest = None
                      ByteLength = None
                      TracedStartInclusive = None
                      TracedEndExclusive = None
                      MaterialPayloads = [||]
                      RelinquishReason = None }

                planEnvelopeImport baseFact envelope promoted preparedByteLength tracedStart tracedEnd

            envelopes |> Array.map import

    let private typedOutcome (fact: ImportFact) : DelegationImportOutcome =
        match fact.OutcomeKind with
        | "adopted" ->
            DelegationImportOutcome.Adopted
                { TargetProviderRun = ProviderRunIdentity.create (Option.defaultValue "" fact.TargetProviderRun)
                  FrameDigest = Option.defaultValue "" fact.FrameDigest
                  ByteLength = Option.defaultValue 0 fact.ByteLength
                  MaterialPayloads = fact.MaterialPayloads |> Array.toList |> List.map PayloadRef.create
                  TracedStartInclusive = fact.TracedStartInclusive
                  TracedEndExclusive = fact.TracedEndExclusive }
        | "relinquished" ->
            DelegationImportOutcome.Relinquished
                { TargetProviderRun = fact.TargetProviderRun |> Option.map ProviderRunIdentity.create
                  Reason = Option.defaultValue "" fact.RelinquishReason }
        | other -> failwith (sprintf "unknown import outcome kind: %s" other)

    let importEventJs (sha256: string -> string) (fact: ImportFact) : obj =
        let event =
            StrengthEvents.historyImported
                (StrengthDecisionId.create fact.DecisionId)
                fact.SourceStreamId
                fact.SourceEventId
                fact.ImportId
                fact.OldBudgetEvidence
                (typedOutcome fact)

        let envelope = StrengthStore.toEnvelope sha256 event

        let payloadObject =
            CanonicalEventCodec.encode envelope
            |> (fun text -> text.TrimEnd '\n')
            |> JS.JSON.parse
            |> fun eventObject -> eventObject?payload

        box
            {| id = EventId.value envelope.EventId
               stream = EventStreamId.value envelope.StreamId
               ``type`` = envelope.EventType
               parents = envelope.Parents |> List.map EventId.value |> List.toArray
               payload = payloadObject
               payloadRefs = envelope.PayloadRefs |> List.map PayloadRef.value |> List.toArray |}
