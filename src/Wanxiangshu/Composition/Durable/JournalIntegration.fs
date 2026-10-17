namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal
open Fable.Core
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation
open Wanxiangshu.Context.Companion
open Wanxiangshu.Composition.Durable.Fact

/// Registered Journal oracle: one EventEnvelope in, one ProjectionSet out.
[<RequireQualifiedAccess>]
module JournalIntegration =
    [<Emit("new TextDecoder('utf-8', { fatal: true, ignoreBOM: true }).decode($0)")>]
    let private decodeUtf8 (_bytes: byte array) : string = jsNative

    let private decodeMaterial bytes =
        try
            Ok(decodeUtf8 bytes)
        with error ->
            Error("XTrace material is not valid UTF-8: " + error.Message)

    let private readMaterial (envelope: EventEnvelope) reference digest =
        result {
            let reference = BlobRef.value reference
            do! Result.requireTrue "XTrace material has an invalid payload reference" (reference.StartsWith("blobs/"))
            let handle = reference.Substring(6)
            let payload = PayloadRef.create handle

            do!
                Result.requireTrue
                    "XTrace material is outside the declared payload closure"
                    (List.contains payload envelope.PayloadRefs)

            let! bytes =
                Map.tryFind payload envelope.Payloads
                |> Result.requireSome "XTrace material payload is missing"

            let! body = decodeMaterial bytes
            let actual = Wanxiangshu.Host.HostDigest.sha256Hex body

            do!
                Result.requireTrue
                    "XTrace material digest mismatch"
                    (actual = handle && actual = BlobDigest.value digest)

            return body
        }

    let private materialOf envelope fact =
        match fact with
        | Agent(AgentFact.Companion(CompanionFactCases.XTracePartAppended part)) ->
            readMaterial envelope part.TextRef part.TextDigest |> Result.map Some
        | Agent(AgentFact.Companion(CompanionFactCases.TerminalOutputCaptured terminal)) ->
            readMaterial envelope terminal.TextRef terminal.TextDigest |> Result.map Some
        | _ -> Ok None

    let rule: IntegrationRule =
        { Name = "Journal"
          Initial = box Fold.empty
          Accepts = fun envelope -> envelope.EventType = EventStoreJournalCodec.JournalEnvelopeEventType
          // A Journal EventStreamId is the durable isolation boundary. A semantic
          // rejection in one session/child/process stream must not black out other
          // Journal streams while waiting for that stream's cut-tail.
          FaultScope = fun envelope -> EventStreamId.value envelope.StreamId
          Integrate =
            fun context current envelope ->
                match EventStoreJournalCodec.tryDecode envelope with
                | Error error when FactCodec.isIgnoredLegacyDecodeError error -> Ok current
                | Error error -> Error error
                | Ok journalEnvelope ->
                    materialOf envelope journalEnvelope.Fact
                    |> Result.bind (fun material ->
                        Fold.foldEnvelopeWithMaterial
                            (Some(envelope.EventId, (fun eventId -> EventCausalContext.contains eventId context)))
                            material
                            (unbox<ProjectionSet> current)
                            journalEnvelope
                        |> Result.mapError (fun rejection ->
                            sprintf "Journal fact '%s' rejected: %s" rejection.Fact rejection.Reason))
                    |> Result.map box
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }
