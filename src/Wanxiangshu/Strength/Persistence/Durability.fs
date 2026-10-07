namespace Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection

[<RequireQualifiedAccess>]
module StrengthDurability =

    let create (store: IEventStore) : StrengthDurabilityPort =
        let loadProjection () =
            task {
                match store.TryCurrent "Strength" with
                | Some value -> return Ok(unbox<StrengthProjection> value)
                | None -> return Ok StrengthProjection.empty
            }

        let loadFrameBundle = StrengthStore.loadFrameBundle store HostDigest.sha256Hex

        let publishPrepared (request: StrengthPreparedRequest) =
            task {
                let payload = StrengthStore.encodeFrameBundlePayload request.Bundle

                let buildPrepared refs =
                    StrengthEvents.prepared
                        request.OwnerSessionId
                        request.DecisionId
                        request.TargetProviderRun
                        request.ReplicaSessionId
                        request.AnchorDigest
                        request.Bundle.Digest
                        request.Bundle.ByteLength
                        refs

                match! StrengthStore.publishWithPayloads store HostDigest.sha256Hex [ payload ] buildPrepared with
                | Ok _ -> return StrengthPreparedPublish.Published
                | Error(PublishError.StorageInvalid error) ->
                    return StrengthPreparedPublish.StorageInvalid(sprintf "%A" error)
                | Error(PublishError.SemanticCut cut) -> return StrengthPreparedPublish.Rejected cut.Reason
                | Error(PublishError.AppendSettlementFailed(eventId, failure)) ->
                    return StrengthPreparedPublish.SettlementFailed(eventId, failure)
                | Error error -> return StrengthPreparedPublish.Rejected(sprintf "%A" error)
            }

        let append event =
            task {
                match! StrengthStore.append store HostDigest.sha256Hex event with
                | Ok _ -> return StrengthDurableAppend.Applied
                | Error(_, AppendError.SemanticCut cut) -> return StrengthDurableAppend.SemanticRejected cut.Reason
                | Error(_, AppendError.StorageInvalid error) ->
                    return StrengthDurableAppend.StorageInvalid(sprintf "%A" error)
                | Error(_, AppendError.AppendFailed reason) ->
                    return StrengthDurableAppend.StorageFailed(sprintf "%A" (AppendError.AppendFailed reason))
                | Error(eventId, failure) -> return StrengthDurableAppend.SettlementFailed(eventId, failure)
            }

        { LoadProjection = loadProjection
          LoadFrameBundle = loadFrameBundle
          PublishPrepared = publishPrepared
          Append = append }
