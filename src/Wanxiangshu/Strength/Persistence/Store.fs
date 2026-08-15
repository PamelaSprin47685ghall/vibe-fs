namespace Wanxiangshu.Strength.Persistence

open Wanxiangshu.Repository.Investigation.Semble

open System.Text
open System.Threading.Tasks
open Thoth.Json
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation.Identity

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

    let private streamIdFor decisionId =
        EventStreamId.create ("strength/" + decisionText decisionId)

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

    let encodeFrameBundlePayload (bundle: StrengthFrameBundle) : byte[] =
        let encodeExchange (exchange: StrengthToolExchange) =
            Encode.object
                [ "tool_name", Encode.string exchange.ToolName
                  "arguments", Encode.string exchange.CanonicalArguments
                  "result", Encode.string exchange.CanonicalResult ]

        let encodeBatch (batch: StrengthRequestBatch) =
            Encode.object
                [ "request_ordinal", Encode.int batch.RequestOrdinal
                  "exchanges", Encode.list (List.map encodeExchange batch.Exchanges) ]

        Encode.object
            [ "version", Encode.int 1
              "digest", Encode.string bundle.Digest
              "byte_length", Encode.int bundle.ByteLength
              "batches", Encode.list (List.map encodeBatch bundle.Batches) ]
        |> Encode.toString 0
        |> Encoding.UTF8.GetBytes

    let private validateBundleDigest (digest: string) (byteLength: int) (bundle: StrengthFrameBundle) =
        if bundle.Digest <> digest || bundle.ByteLength <> byteLength then
            Error "Strength frame payload digest/length mismatch"
        else
            Ok bundle

    let decodeFrameBundlePayload (sha256: string -> string) (content: byte[]) : Result<StrengthFrameBundle, string> =
        let exchangeDecoder =
            Decode.object (fun get ->
                { ToolName = get.Required.Field "tool_name" Decode.string
                  CanonicalArguments = get.Required.Field "arguments" Decode.string
                  CanonicalResult = get.Required.Field "result" Decode.string })

        let batchDecoder =
            Decode.object (fun get ->
                { RequestOrdinal = get.Required.Field "request_ordinal" Decode.int
                  Exchanges = get.Required.Field "exchanges" (Decode.list exchangeDecoder) })

        let decoder =
            Decode.object (fun get ->
                get.Required.Field "version" Decode.int,
                get.Required.Field "digest" Decode.string,
                get.Required.Field "byte_length" Decode.int,
                get.Required.Field "batches" (Decode.list batchDecoder))

        Encoding.UTF8.GetString content
        |> Decode.fromString decoder
        |> Result.bind (fun (version, digest, byteLength, batches) ->
            if version <> 1 then
                Error(sprintf "unsupported Strength frame payload version: %d" version)
            else
                StrengthFrame.tryBuild sha256 byteLength batches
                |> Result.mapError (sprintf "invalid Strength frame payload: %A")
                |> Result.bind (validateBundleDigest digest byteLength))

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

        Decode.fromValue "$" decoder payload
        |> Result.bind (fun (owner, decision, target, replica, budgetText, anchor, digest, byteLength) ->
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
                ))

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
                get.Required.Field "decision_id" Decode.string, get.Required.Field "target_provider_run" Decode.string)

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

    let private requireFrameMatches (prepared: StrengthCandidatePrepared) (bundle: StrengthFrameBundle) =
        if bundle.Digest <> prepared.FrameDigest then
            Error "Strength Prepared frame digest does not match payload"
        elif bundle.ByteLength <> prepared.ByteLength then
            Error "Strength Prepared byte length does not match payload"
        else
            Ok()

    let loadFrameBundle
        (store: IEventStore)
        (sha256: string -> string)
        (prepared: StrengthCandidatePrepared)
        : Task<Result<StrengthFrameBundle, string>> =
        taskResult {
            match prepared.MaterialPayloads with
            | [ payloadRef ] ->
                let! payloadOpt = store.ReadPayload payloadRef

                let! bytes =
                    payloadOpt
                    |> Option.map Ok
                    |> Option.defaultValue (
                        Error(sprintf "missing Strength frame payload: %s" (PayloadRef.value payloadRef))
                    )

                let! bundle = decodeFrameBundlePayload sha256 bytes
                do! requireFrameMatches prepared bundle
                return bundle
            | [] -> return! Error "Strength Prepared has no frame payload"
            | _ -> return! Error "Strength Prepared has ambiguous frame payload closure"
        }

    let private appendReceiptResult eventId (receipt: AppendReceipt) =
        match AppendReceipt.cutFor eventId receipt with
        | Some cut -> Error(AppendError.SemanticCut cut)
        | None -> Ok()

    let append
        (store: IEventStore)
        (sha256: string -> string)
        (event: StrengthEvent)
        : Task<Result<unit, AppendError>> =
        task {
            let envelope = toEnvelope sha256 event

            match! store.Append [ envelope ] with
            | Ok receipt -> return appendReceiptResult envelope.EventId receipt
            | Error error -> return Error error
        }

    let private publishReceiptResult event eventId (receipt: AppendReceipt) =
        match AppendReceipt.cutFor eventId receipt with
        | Some cut -> Error(PublishError.SemanticCut cut)
        | None -> Ok event

    /// STRENGTH-006: payload bytes become local content-addressed PayloadRefs
    /// before the Prepared event is appended. Git object identity is absent from
    /// the runtime path; remote sync blobifies these files later.
    let private appendToPublish =
        function
        | AppendError.StorageInvalid error -> PublishError.StorageInvalid error
        | AppendError.SemanticCut cut -> PublishError.SemanticCut cut
        | AppendError.AppendFailed reason -> PublishError.PublishFailed reason

    let publishWithPayloads
        (store: IEventStore)
        (sha256: string -> string)
        (contents: byte[] list)
        (buildEvent: PayloadRef list -> StrengthEvent)
        : Task<Result<StrengthEvent, PublishError>> =
        let writePayloads: Task<Result<PayloadRef list, PublishError>> =
            contents
            |> TaskResultList.traverseM (fun bytes ->
                store.WritePayload bytes |> TaskResult.mapError PublishError.PublishFailed)
            |> TaskValue.map (Result.map PayloadRefs.canonicalize)

        taskResult {
            let! payloadRefs = writePayloads
            let event = buildEvent payloadRefs
            let envelope = toEnvelope sha256 event
            let! receipt = store.Append [ envelope ] |> TaskResult.mapError appendToPublish
            return! publishReceiptResult event envelope.EventId receipt
        }

    /// Small facades keep callers on the durable adapter rather than reaching
    /// through to a separately rebuilt in-memory registry.
    let isPromoted decisionId projection =
        StrengthProjection.isPromoted decisionId projection

    let tryTraceRange decisionId projection =
        StrengthProjection.tryTraceRange decisionId projection

    let tryDecisionForTarget targetRun projection =
        StrengthProjection.tryDecisionForTarget targetRun projection
