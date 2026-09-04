// WHAT[EPI-019]: durable codec for generic sphinx_inquiry_* inquiries.
// One stream per iq_ id, deterministic envelope ids chained by causal
// parents, payloads as canonical JSON. The canonical rule folds envelopes
// into a per-inquiry cursor map; boot materializes that Current into a fresh
// Registry. The Registry stays a replaceable hot cache, never the truth.

namespace Wanxiangshu.Sphinx

open System
open Thoth.Json
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.Core

module GenericDurability =

    let streamFor (inquiryId: string) : string = "sphinx-generic/" + inquiryId

    let envelopeId (inquiryId: string) (revision: int) : string = inquiryId + ":" + string revision

    let private encodeEnvelope
        (inquiryId: string)
        (revision: int)
        (payload: JsonValue)
        : Persistence.EventStore.EventEnvelope =
        Persistence.EventStore.EventEnvelope.normalize
            { EventId = Wanxiangshu.Foundation.Identity.EventId.create (envelopeId inquiryId revision)
              StreamId = Persistence.EventStore.EventStreamId.create (streamFor inquiryId)
              EventType = SphinxEventTypes.GenericInquiry
              Parents =
                if revision <= 0 then
                    []
                else
                    [ Wanxiangshu.Foundation.Identity.EventId.create (envelopeId inquiryId (revision - 1)) ]
              Payload = payload
              PayloadRefs = [] }

    let encodeStarted (entry: GecInquiry.GecInquiryEntry) : Persistence.EventStore.EventEnvelope =
        encodeEnvelope
            entry.InquiryId
            entry.InquiryRevision
            (Encode.object
                [ "inquiry", Encode.string entry.InquiryId
                  "kind", Encode.string "started"
                  "revision", Encode.int entry.InquiryRevision
                  "question", Encode.string entry.InquiryQuestion
                  "profile", Encode.string entry.InquiryProfile
                  "executionMode", Encode.string entry.InquiryExecutionMode
                  "pluginsJson", Encode.string (CoreHash.canonical entry.InquiryPlugins)
                  "budgetJson", Encode.string (CoreHash.canonical entry.InquiryBudget) ])

    let encodeSubmitted
        (entry: GecInquiry.GecInquiryEntry)
        (expectedRevision: int)
        (results: obj list)
        : Persistence.EventStore.EventEnvelope =
        encodeEnvelope
            entry.InquiryId
            entry.InquiryRevision
            (Encode.object
                [ "inquiry", Encode.string entry.InquiryId
                  "kind", Encode.string "submitted"
                  "revision", Encode.int entry.InquiryRevision
                  "expectedRevision", Encode.int expectedRevision
                  "resultsJson", Encode.string (CoreHash.canonical (results |> List.toArray)) ])

    let encodeCancelled (entry: GecInquiry.GecInquiryEntry) : Persistence.EventStore.EventEnvelope =
        encodeEnvelope
            entry.InquiryId
            entry.InquiryRevision
            (Encode.object
                [ "inquiry", Encode.string entry.InquiryId
                  "kind", Encode.string "cancelled"
                  "revision", Encode.int entry.InquiryRevision ])

    [<Emit("JSON.parse($0)")>]
    let private parseJson (text: string) : obj = jsNative

    let private parseResults (cursor: GenericIntegrator.GenericCursor) : obj list =
        cursor.ResultsJson
        |> List.map (fun resultsJson ->
            if String.IsNullOrWhiteSpace resultsJson then
                [||]
            else
                unbox<obj array> (parseJson resultsJson))
        |> List.collect Array.toList

    let private restoreEntry
        (inquiryId: string)
        (cursor: GenericIntegrator.GenericCursor)
        : GecInquiry.GecInquiryEntry =
        { InquiryId = inquiryId
          InquiryQuestion = cursor.Question
          InquiryProfile = cursor.Profile
          InquiryPlugins = parseJson cursor.PluginsJson
          InquiryExecutionMode = cursor.ExecutionMode
          InquiryBudget = parseJson cursor.BudgetJson
          InquiryRevision = cursor.Revision
          InquiryCancelled = cursor.Cancelled
          InquiryResults = parseResults cursor }

    let restore (current: GenericIntegrator.SphinxGenericCurrent) : Result<GecInquiry.Registry, string> =
        try
            let registry = GecInquiry.Registry()

            current
            |> Map.toList
            |> List.sortBy fst
            |> List.iter (fun (inquiryId, cursor) -> registry.Restore(restoreEntry inquiryId cursor))

            Ok registry
        with ex ->
            Error(sprintf "sphinx generic restore failed: %s" ex.Message)
