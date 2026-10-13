namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal

/// Registered Journal oracle: one EventEnvelope in, one ProjectionSet out.
[<RequireQualifiedAccess>]
module JournalIntegration =
    let rule: IntegrationRule =
        { Name = "Journal"
          Initial = box Fold.empty
          Accepts =
            fun envelope ->
                envelope.EventType = EventStoreJournalCodec.JournalEnvelopeEventType
                || envelope.EventType = Wanxiangshu.Mission.Lifecycle.LifecycleEventTypes.IncumbencyRetired
          // A Journal EventStreamId is the durable isolation boundary. A semantic
          // rejection in one session/child/process stream must not black out other
          // Journal streams while waiting for that stream's cut-tail.
          FaultScope = fun envelope -> EventStreamId.value envelope.StreamId
          Integrate =
            fun current envelope ->
                if envelope.EventType = Wanxiangshu.Mission.Lifecycle.LifecycleEventTypes.IncumbencyRetired then
                    let projection = unbox<ProjectionSet> current

                    match
                        Wanxiangshu.Mission.Lifecycle.LifecycleCodec.decodePayload
                            envelope.EventType
                            envelope.Payload
                    with
                    | Ok(
                        Wanxiangshu.Mission.Lifecycle.LifecycleEvent.IncumbencyRetiredExecution
                            { Outcome = Wanxiangshu.Mission.Lifecycle.RetirementOutcome.Delivered }
                      ) ->
                        let workId =
                            EventStreamId.value envelope.StreamId
                            |> fun value -> value.Replace("lifecycle/", "")

                        Ok(box (Fold.settleCompletedLife projection (SessionId.create workId)))
                    | _ -> Ok current
                else
                    match EventStoreJournalCodec.tryDecode envelope with
                    | Error error when FactCodec.isIgnoredLegacyDecodeError error -> Ok current
                    | Error error -> Error error
                    | Ok journalEnvelope ->
                        Fold.foldEnvelope (unbox<ProjectionSet> current) journalEnvelope
                        |> Result.map box
                        |> Result.mapError (fun rejection ->
                            sprintf "Journal fact '%s' rejected: %s" rejection.Fact rejection.Reason)
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }
