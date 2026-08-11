namespace Wanxiangshu.Infrastructure.Persist

open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.OpenCode

[<RequireQualifiedAccess>]
module StrengthDurability =

    let create (raw: IGitRawStore) (store: IEventStore) : StrengthDurabilityPort =
        { LoadProjection =
            fun () -> StrengthStore.loadProjection raw (store.OpenSnapshot())
          LoadFrameBundle = StrengthStore.loadFrameBundle raw HostDigest.sha256Hex
          PublishPrepared =
            fun owner decision target replica budget anchorDigest bundle ->
                let payload = StrengthStore.encodeFrameBundlePayload bundle

                match
                    StrengthStore.publishWithPayloads
                        store
                        HostDigest.sha256Hex
                        [ payload ]
                        (fun refs ->
                            StrengthEvents.prepared
                                owner
                                decision
                                target
                                replica
                                budget
                                anchorDigest
                                bundle.Digest
                                bundle.ByteLength
                                refs)
                with
                | Ok _ -> StrengthPreparedPublish.Published
                | Error(PublishError.StorageInvalid error) ->
                    StrengthPreparedPublish.StorageInvalid(sprintf "%A" error)
                | Error error -> StrengthPreparedPublish.Rejected(sprintf "%A" error)
          Append =
            fun event ->
                StrengthStore.append store HostDigest.sha256Hex event
                |> Result.map (fun _ -> ())
                |> Result.mapError (sprintf "%A") }
