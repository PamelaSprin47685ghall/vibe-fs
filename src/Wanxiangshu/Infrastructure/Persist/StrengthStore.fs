namespace Wanxiangshu.Infrastructure.Persist

open Thoth.Json
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// STRENGTH-006/007/008/017: Persist adapter for Strength facts.
///
/// There is no feature-owned journal/ref/blob namespace. Large material is first
/// written to the existing Git raw object store and then named only by the
/// EventEnvelope.PayloadRefs closure. One decision owns one EventStore stream;
/// Prepared -> Promoted -> Traced/Abandoned parent edges make restart fold
/// deterministic and let the generic store reject missing causal predecessors.
[<RequireQualifiedAccess>]
module StrengthStore =

    let private decisionText decisionId = StrengthDecisionId.value decisionId

    let private kindOf =
        function
        | StrengthEvent.Prepared _ -> StrengthEventTypes.CandidatePrepared
        | StrengthEvent.Promoted _ -> StrengthEventTypes.CandidatePromoted
        | StrengthEvent.Traced _ -> StrengthEventTypes.FramesTraced
        | StrengthEvent.Abandoned _ -> StrengthEventTypes.CandidateAbandoned

    let private decisionOf =
        function
        | StrengthEvent.Prepared prepared -> prepared.DecisionId
        | StrengthEvent.Promoted promoted -> promoted.DecisionId
        | StrengthEvent.Traced traced -> traced.DecisionId
        | StrengthEvent.Abandoned abandoned -> abandoned.DecisionId

    /// Fixed identity per decision+fact-kind. A second payload for the same fact
    /// therefore becomes EventStore IdentityCollision instead of a second truth.
    let eventIdFor (sha256: string -> string) (decisionId: StrengthDecisionId) (eventType: string) : EventId =
        String.concat "\u001f" [ "strength-event-v1"; decisionText decisionId; eventType ]
        |> sha256
        |> EventId.create

    let private streamIdFor decisionId = EventStreamId.create ("strength/" + decisionText decisionId)

    let private parentsFor sha256 event =
        let decisionId = decisionOf event
        let id eventType = eventIdFor sha256 decisionId eventType

        match event with
        | StrengthEvent.Prepared _ -> []
        | StrengthEvent.Promoted _ -> [ id StrengthEventTypes.CandidatePrepared ]
        | StrengthEvent.Traced _ -> [ id StrengthEventTypes.CandidatePromoted ]
        | StrengthEvent.Abandoned _ -> [ id StrengthEventTypes.CandidatePrepared ]

    let private encodePayload =
        function
        | StrengthEvent.Prepared prepared ->
            Encode.object
                [ "owner_session_id", Encode.string (SessionId.value prepared.OwnerSessionId)
                  "decision_id", Encode.string (decisionText prepared.DecisionId)
                  "target_provider_run", Encode.string (ProviderRunIdentity.value prepared.TargetProviderRun)
                  "replica_session_id", Encode.string (SessionId.value prepared.ReplicaSessionId)
                  "budget", Encode.string (StrengthBudget.wire prepared.Budget)
                  "anchor_digest", Encode.string prepared.AnchorDigest
                  "frame_digest", Encode.string prepared.FrameDigest
                  "byte_length", Encode.int prepared.ByteLength ]
        | StrengthEvent.Promoted promoted ->
            Encode.object
                [ "owner_session_id", Encode.string (SessionId.value promoted.OwnerSessionId)
                  "decision_id", Encode.string (decisionText promoted.DecisionId)
                  "target_provider_run", Encode.string (ProviderRunIdentity.value promoted.TargetProviderRun)
                  "frame_digest", Encode.string promoted.FrameDigest ]
        | StrengthEvent.Traced traced ->
            Encode.object
                [ "decision_id", Encode.string (decisionText traced.DecisionId)
                  "start_inclusive", Encode.int64 traced.StartInclusive
                  "end_exclusive", Encode.int64 traced.EndExclusive ]
        | StrengthEvent.Abandoned abandoned ->
            Encode.object
                [ "decision_id", Encode.string (decisionText abandoned.DecisionId)
                  "target_provider_run", Encode.string (ProviderRunIdentity.value abandoned.TargetProviderRun) ]

    let private payloadRefsOf =
        function
        | StrengthEvent.Prepared prepared -> prepared.MaterialPayloads
        | StrengthEvent.Promoted promoted -> promoted.MaterialPayloads
        | StrengthEvent.Traced _
        | StrengthEvent.Abandoned _ -> []

    let toEnvelope (sha256: string -> string) (event: StrengthEvent) : EventEnvelope =
        let eventType = kindOf event
        let decisionId = decisionOf event

        EventEnvelope.normalize
            { EventId = eventIdFor sha256 decisionId eventType
              StreamId = streamIdFor decisionId
              EventType = eventType
              Parents = parentsFor sha256 event
              Payload = encodePayload event
              PayloadRefs = payloadRefsOf event }

    let private decodePrepared payload refs : Result<StrengthEvent, string> =
        let decoder =
            Decode.object (fun get ->
                get.Required.Field "owner_session_id" Decode.string,
                get.Required.Field "decision_id" Decode.string,
                get.Required.Field "target_provider_run" Decode.string,
                get.Required.Field "replica_session_id" Decode.string,
                get.Required.Field "budget" Decode.string,
                get.Required.Field "anchor_digest" Decode.string,
                get.Required.Field "frame_digest" Decode.string,
                get.Required.Field "byte_length" Decode.int)

        match Decode.fromValue "$" decoder payload with
        | Error error -> Error error
        | Ok(owner, decision, target, replica, budgetText, anchor, digest, byteLength) ->
            match StrengthBudget.parse budgetText with
            | None -> Error(sprintf "invalid Strength budget: %s" budgetText)
            | Some budget ->
                Ok(
                    StrengthEvents.prepared
                        (SessionId.create owner)
                        (StrengthDecisionId.create decision)
                        (ProviderRunIdentity.create target)
                        (SessionId.create replica)
                        budget
                        anchor
                        digest
                        byteLength
                        refs
                )

    let private decodePromoted payload refs : Result<StrengthEvent, string> =
        let decoder =
            Decode.object (fun get ->
                get.Required.Field "owner_session_id" Decode.string,
                get.Required.Field "decision_id" Decode.string,
                get.Required.Field "target_provider_run" Decode.string,
                get.Required.Field "frame_digest" Decode.string)

        match Decode.fromValue "$" decoder payload with
        | Error error -> Error error
        | Ok(owner, decision, target, digest) ->
            Ok(
                StrengthEvents.promoted
                    (SessionId.create owner)
                    (StrengthDecisionId.create decision)
                    (ProviderRunIdentity.create target)
                    digest
                    refs
            )

    let private decodeTraced payload : Result<StrengthEvent, string> =
        let decoder =
            Decode.object (fun get ->
                get.Required.Field "decision_id" Decode.string,
                get.Required.Field "start_inclusive" Decode.int64,
                get.Required.Field "end_exclusive" Decode.int64)

        Decode.fromValue "$" decoder payload
        |> Result.map (fun (decision, startInclusive, endExclusive) ->
            StrengthEvents.traced (StrengthDecisionId.create decision) startInclusive endExclusive)

    let private decodeAbandoned payload : Result<StrengthEvent, string> =
        let decoder =
            Decode.object (fun get ->
                get.Required.Field "decision_id" Decode.string,
                get.Required.Field "target_provider_run" Decode.string)

        Decode.fromValue "$" decoder payload
        |> Result.map (fun (decision, target) ->
            StrengthEvents.abandoned (StrengthDecisionId.create decision) (ProviderRunIdentity.create target))

    let tryDecodeEnvelope (envelope: EventEnvelope) : Result<StrengthEvent, string> =
        match envelope.EventType with
        | eventType when eventType = StrengthEventTypes.CandidatePrepared ->
            decodePrepared envelope.Payload envelope.PayloadRefs
        | eventType when eventType = StrengthEventTypes.CandidatePromoted ->
            decodePromoted envelope.Payload envelope.PayloadRefs
        | eventType when eventType = StrengthEventTypes.FramesTraced -> decodeTraced envelope.Payload
        | eventType when eventType = StrengthEventTypes.CandidateAbandoned -> decodeAbandoned envelope.Payload
        | other -> Error(sprintf "not a Strength event: %s" other)

    /// Write raw bytes into the unified object database. The object may exist
    /// before publication; it becomes durable semantic closure only once an
    /// EventEnvelope PayloadRef reaches refs/wanxiang/store.
    let storePayload (raw: IGitRawStore) (content: byte[]) : PayloadRef =
        raw.WriteBlob content |> GitObjectId.value |> PayloadRef.create

    let tryReadPayload (raw: IGitRawStore) (payloadRef: PayloadRef) : byte[] option =
        raw.ReadObject(GitObjectId.create (PayloadRef.value payloadRef))

    let append
        (store: IEventStore)
        (sha256: string -> string)
        (event: StrengthEvent)
        : Result<StoreSnapshot, AppendError> =
        let envelope = toEnvelope sha256 event
        store.Append(store.OpenSnapshot(), [ envelope ])

    let private loadEnvelopes (raw: IGitRawStore) (snapshot: StoreSnapshot) : Result<EventEnvelope list, string> =
        match EventStoreMergeSpec.merge raw (MergeInput.ofList [ snapshot ]) with
        | Error(MergeError.StorageInvalid detail) -> Error(sprintf "storage invalid: %A" detail)
        | Ok events ->
            events
            |> List.filter (fun envelope -> StrengthEventTypes.isStrengthEvent envelope.EventType)
            |> Ok

    let loadEvents (raw: IGitRawStore) (snapshot: StoreSnapshot) : Result<StrengthEvent list, string> =
        match loadEnvelopes raw snapshot with
        | Error error -> Error error
        | Ok [] -> Ok []
        | Ok envelopes ->
            match EventStoreFold.fold envelopes with
            | Error(FoldError.StorageInvalid detail) -> Error(sprintf "storage invalid: %A" detail)
            | Ok genericProjection when not (List.isEmpty genericProjection.Conflicts) ->
                Error(sprintf "strength stream conflict: %A" genericProjection.Conflicts)
            | Ok genericProjection ->
                let byId = envelopes |> List.map (fun envelope -> EventId.value envelope.EventId, envelope) |> Map.ofList

                let rec decode remaining acc =
                    match remaining with
                    | [] -> Ok(List.rev acc)
                    | eventId :: tail ->
                        match Map.tryFind (EventId.value eventId) byId with
                        | None -> Error(sprintf "Strength fold order missing event: %s" (EventId.value eventId))
                        | Some envelope ->
                            match tryDecodeEnvelope envelope with
                            | Error error -> Error error
                            | Ok event -> decode tail (event :: acc)

                decode genericProjection.FoldOrder []

    let loadProjection (raw: IGitRawStore) (snapshot: StoreSnapshot) : Result<StrengthProjection, string> =
        match loadEvents raw snapshot with
        | Error error -> Error error
        | Ok events ->
            match StrengthProjection.fold events with
            | Ok projection -> Ok projection
            | Error conflict -> Error(sprintf "strength projection conflict: %A" conflict)

    /// Small facades keep callers on the durable adapter rather than reaching
    /// through to a separately rebuilt in-memory registry.
    let isPromoted decisionId projection = StrengthProjection.isPromoted decisionId projection
    let tryTraceRange decisionId projection = StrengthProjection.tryTraceRange decisionId projection
    let tryDecisionForTarget targetRun projection = StrengthProjection.tryDecisionForTarget targetRun projection
