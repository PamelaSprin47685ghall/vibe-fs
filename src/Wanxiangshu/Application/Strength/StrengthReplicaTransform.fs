namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

[<RequireQualifiedAccess>]
type StrengthReplicaTransformOutcome =
    | NotReplica
    | Ready of completedBatches: StrengthRequestBatch list
    | Retired of reason: string * completedBatches: StrengthRequestBatch list

/// STRENGTH-003/004/009/014: transform program for the InternalLeaf replica.
/// It bypasses Work recovery/Companion writers, replaces the physical child
/// transcript with the frozen owner mirror, and replays only this decision's
/// completed prior batches. Reaching K aborts before the next provider request.
[<RequireQualifiedAccess>]
module StrengthReplicaTransform =

    let private snapshotOf wire =
        { CurrentProjection = ProviderProjection.toSemantic wire
          CommittedPrefix = None
          BlogFrames = []
          TransportMessages = Set.empty
          HostReanchor = None }

    let apply
        (sha256: string -> string)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (output: obj)
        : Task<StrengthReplicaTransformOutcome> =
        task {
            match Projection.projectionSessionIdFromMessages output with
            | None -> return StrengthReplicaTransformOutcome.NotReplica
            | Some sessionIdText ->
                let replicaSessionId = SessionId.create sessionIdText

                match runtime.TryFindByReplica replicaSessionId with
                | None -> return StrengthReplicaTransformOutcome.NotReplica
                | Some binding ->
                    let rawMessages = Projection.messagesFromTransformOutput output
                    let currentWire = Projection.decodeMessageView rawMessages
                    let batches = StrengthBatchCollector.collectCompleteBatches currentWire.Messages
                    let completed = List.length batches

                    if completed >= StrengthBudget.requestLimit binding.Budget then
                        runtime.Retire replicaSessionId |> ignore
                        let! _ = sessions.AbortSession replicaSessionId
                        return StrengthReplicaTransformOutcome.Retired("provider-request-budget-reached", batches)
                    else
                        let localFrame =
                            match batches with
                            | [] -> Ok None
                            | _ ->
                                StrengthFrame.tryBuild sha256 binding.MaxFrameBytes batches
                                |> Result.map Some

                        match localFrame with
                        | Error error ->
                            runtime.Retire replicaSessionId |> ignore
                            let! _ = sessions.AbortSession replicaSessionId
                            return StrengthReplicaTransformOutcome.Retired(sprintf "invalid-replica-frame:%A" error, batches)
                        | Ok frame ->
                            let intents =
                                [ yield
                                      ProjectionIntent.useStrengthMirror
                                          binding.DecisionId
                                          binding.TargetProviderRun
                                          binding.SemanticDigest
                                          binding.MirrorMessages
                                  match frame with
                                  | Some bundle ->
                                      yield
                                          ProjectionIntent.strengthReplicaLocal
                                              binding.OwnerSessionId
                                              binding.DecisionId
                                              bundle
                                  | None -> () ]

                            match ProjectionPlanner.plan intents with
                            | Error conflict ->
                                runtime.Retire replicaSessionId |> ignore
                                let! _ = sessions.AbortSession replicaSessionId
                                return StrengthReplicaTransformOutcome.Retired(sprintf "projection-conflict:%A" conflict, batches)
                            | Ok ordered ->
                                let rendered =
                                    ProjectionRenderer.renderMessagesWithHostIds
                                        sha256
                                        (snapshotOf currentWire)
                                        currentWire.Messages
                                        ordered

                                match Projection.tryApplyRenderedMessages sessionIdText sha256 rendered with
                                | Error error ->
                                    runtime.Retire replicaSessionId |> ignore
                                    let! _ = sessions.AbortSession replicaSessionId
                                    return StrengthReplicaTransformOutcome.Retired(error, batches)
                                | Ok replacement ->
                                    output?messages <- List.toArray replacement
                                    return StrengthReplicaTransformOutcome.Ready batches
        }
