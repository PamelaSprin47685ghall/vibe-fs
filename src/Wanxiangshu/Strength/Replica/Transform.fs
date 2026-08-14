namespace Wanxiangshu.Strength.Replica
open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
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
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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

    let private providerResultsByCallId (rawMessage: obj) =
        ProviderWireCapture.decodeMessageView [ rawMessage ]
        |> fun view -> view.Messages
        |> List.collect (fun message -> message.Parts)
        |> List.choose (function
            | ProviderProjection.WireToolResult(callId, result) -> Some(ToolCallId.value callId, result)
            | _ -> None)
        |> Map.ofList

    let private collectHostCompleteBatches (rawMessages: obj list) : StrengthRequestBatch list =
        let rec loop remaining requestOrdinal collected =
            match remaining with
            | [] -> List.rev collected
            | rawMessage :: tail ->
                match SessionSnapshotPort.projectMessage rawMessage with
                | Some message when String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ->
                    let toolParts = message.ToolParts |> Array.toList

                    if List.isEmpty toolParts then
                        List.rev collected
                    elif
                        toolParts
                        |> List.exists (fun part ->
                            match part.State with
                            | SnapshotToolPartState.Pending -> true
                            | _ -> false)
                    then
                        List.rev collected
                    else
                        let results = providerResultsByCallId rawMessage

                        if Map.count results <> List.length toolParts then
                            List.rev collected
                        else
                            let exchanges =
                                toolParts
                                |> List.choose (fun part ->
                                    Map.tryFind (ToolCallId.value part.ToolCallId) results
                                    |> Option.map (fun result ->
                                        { ToolName = part.ToolName.Trim().ToLowerInvariant()
                                          CanonicalArguments = part.InputCanonical
                                          CanonicalResult = result }))

                            if List.length exchanges <> List.length toolParts then
                                List.rev collected
                            else
                                let nextOrdinal = requestOrdinal + 1

                                loop
                                    tail
                                    nextOrdinal
                                    ({ RequestOrdinal = nextOrdinal
                                       Exchanges = exchanges }
                                     :: collected)
                | _ -> loop tail requestOrdinal collected

        loop rawMessages 0 []

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
            match ProviderWireDecode.projectionSessionIdFromMessages output with
            | None -> return StrengthReplicaTransformOutcome.NotReplica
            | Some sessionIdText ->
                let replicaSessionId = SessionId.create sessionIdText

                match runtime.TryFindByReplica replicaSessionId with
                | None -> return StrengthReplicaTransformOutcome.NotReplica
                | Some binding ->
                    let rawMessages = ProviderWireDecode.messagesFromTransformOutput output
                    let currentWire = ProviderWireCapture.decodeMessageView rawMessages
                    let hostBatches = collectHostCompleteBatches rawMessages

                    let batches =
                        if List.isEmpty hostBatches then
                            StrengthBatchCollector.collectCompleteBatches currentWire.Messages
                        else
                            hostBatches

                    let completed = List.length batches

                    if completed >= StrengthBudget.requestLimit binding.Budget then
                        runtime.Retire replicaSessionId |> ignore
                        let! _ = sessions.AbortSession replicaSessionId
                        return StrengthReplicaTransformOutcome.Retired("provider-request-budget-reached", batches)
                    else
                        let localFrame =
                            match batches with
                            | [] -> Ok None
                            | _ -> StrengthFrame.tryBuild sha256 binding.MaxFrameBytes batches |> Result.map Some

                        match localFrame with
                        | Error error ->
                            runtime.Retire replicaSessionId |> ignore
                            let! _ = sessions.AbortSession replicaSessionId

                            return
                                StrengthReplicaTransformOutcome.Retired(
                                    sprintf "invalid-replica-frame:%A" error,
                                    batches
                                )
                        | Ok frame ->
                            let intents =
                                [ yield
                                      ProjectionIntent.useStrengthMirror
                                          binding.DecisionId
                                          binding.TargetProviderRun
                                          binding.SemanticDigest
                                          binding.LocalizedMirrorMessages
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

                                return
                                    StrengthReplicaTransformOutcome.Retired(
                                        sprintf "projection-conflict:%A" conflict,
                                        batches
                                    )
                            | Ok ordered ->
                                let rendered =
                                    ProjectionRenderer.renderMessagesWithHostIds
                                        sha256
                                        (snapshotOf currentWire)
                                        currentWire.Messages
                                        ordered

                                match
                                    ProjectionMessageEdit.tryApplyStrengthRenderedMessages sessionIdText sha256 rendered
                                with
                                | Error error ->
                                    runtime.Retire replicaSessionId |> ignore
                                    let! _ = sessions.AbortSession replicaSessionId
                                    return StrengthReplicaTransformOutcome.Retired(error, batches)
                                | Ok replacement ->
                                    HostMessageProjection.replaceMessagesInPlace output replacement
                                    return StrengthReplicaTransformOutcome.Ready batches
        }
