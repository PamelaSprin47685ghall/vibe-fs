namespace Wanxiangshu.Infrastructure.Persist

open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
module StrengthDurability =

    let create (raw: IGitRawStore) (store: IEventStore) : StrengthDurabilityPort =
        let loadProjection () =
            StrengthStore.loadProjection raw (store.OpenSnapshot())

        let loadFrameBundle = StrengthStore.loadFrameBundle raw HostDigest.sha256Hex

        let publishPrepared request =
            let payload = StrengthStore.encodeFrameBundlePayload request.Bundle

            let buildPrepared refs =
                StrengthEvents.prepared
                    request.OwnerSessionId
                    request.DecisionId
                    request.TargetProviderRun
                    request.ReplicaSessionId
                    request.Budget
                    request.AnchorDigest
                    request.Bundle.Digest
                    request.Bundle.ByteLength
                    refs

            match StrengthStore.publishWithPayloads store HostDigest.sha256Hex [ payload ] buildPrepared with
            | Ok _ -> StrengthPreparedPublish.Published
            | Error(PublishError.StorageInvalid error) -> StrengthPreparedPublish.StorageInvalid(sprintf "%A" error)
            | Error error -> StrengthPreparedPublish.Rejected(sprintf "%A" error)

        let append event =
            StrengthStore.append store HostDigest.sha256Hex event
            |> Result.map (fun _ -> ())
            |> Result.mapError (sprintf "%A")

        { LoadProjection = loadProjection
          LoadFrameBundle = loadFrameBundle
          PublishPrepared = publishPrepared
          Append = append }
