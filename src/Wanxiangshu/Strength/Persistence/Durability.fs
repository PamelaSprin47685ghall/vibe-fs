namespace Wanxiangshu.Strength.Persistence
open Wanxiangshu.Repository.Investigation.Semble

open System.Threading.Tasks
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
open Wanxiangshu.Host
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

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

        let publishPrepared request =
            task {
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

                match! StrengthStore.publishWithPayloads store HostDigest.sha256Hex [ payload ] buildPrepared with
                | Ok _ -> return StrengthPreparedPublish.Published
                | Error(PublishError.StorageInvalid error) ->
                    return StrengthPreparedPublish.StorageInvalid(sprintf "%A" error)
                | Error error -> return StrengthPreparedPublish.Rejected(sprintf "%A" error)
            }

        let append event =
            task {
                match! StrengthStore.append store HostDigest.sha256Hex event with
                | Ok _ -> return Ok()
                | Error err -> return Error(sprintf "%A" err)
            }

        { LoadProjection = loadProjection
          LoadFrameBundle = loadFrameBundle
          PublishPrepared = publishPrepared
          Append = append }
