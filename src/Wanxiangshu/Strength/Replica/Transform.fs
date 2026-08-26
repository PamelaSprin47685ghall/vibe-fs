// primary_owner: strength — Strength.Contract — MOVE — Strength replica transform surface
namespace Wanxiangshu.Strength.Replica

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection

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

    let private providerResultsByCallId (rawMessages: obj list) =
        ProviderWireCapture.decodeMessageView rawMessages
        |> fun view -> view.Messages
        |> List.collect (fun message -> message.Parts)
        |> List.choose (function
            | ProviderProjection.WireToolResult(callId, result) -> Some(ToolCallId.value callId, result)
            | _ -> None)
        |> Map.ofList

    let private isPendingToolPart (part: SessionToolPart) =
        match part.State with
        | SnapshotToolPartState.Pending -> true
        | _ -> false

    let private hasPendingTool (toolParts: SessionToolPart list) =
        toolParts |> List.exists isPendingToolPart

    let private exchangeOfPart (results: Map<string, string>) (part: SessionToolPart) =
        Map.tryFind (ToolCallId.value part.ToolCallId) results
        |> Option.map (fun result ->
            { ToolName = part.ToolName.Trim().ToLowerInvariant()
              CanonicalArguments = part.InputCanonical
              CanonicalResult = result })

    [<RequireQualifiedAccess>]
    type private HostBatchStep =
        | Skip
        | Stop
        | Take of StrengthRequestBatch

    let private classifyAssistantBatch
        (results: Map<string, string>)
        (requestOrdinal: int)
        (rawMessage: obj)
        (message: SessionMessage)
        : HostBatchStep =
        let toolParts = message.ToolParts |> Array.toList
        let exchanges = toolParts |> List.choose (exchangeOfPart results)

        if List.isEmpty toolParts then
            HostBatchStep.Stop
        elif hasPendingTool toolParts then
            HostBatchStep.Stop
        elif List.length exchanges <> List.length toolParts then
            HostBatchStep.Stop
        else
            HostBatchStep.Take
                { RequestOrdinal = requestOrdinal + 1
                  Exchanges = exchanges }

    let private classifyHostMessage
        (results: Map<string, string>)
        (requestOrdinal: int)
        (rawMessage: obj)
        : HostBatchStep =
        match SessionSnapshotPort.projectMessage rawMessage with
        | Some message when String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ->
            classifyAssistantBatch results requestOrdinal rawMessage message
        | _ -> HostBatchStep.Skip

    let rec private continueHostBatch
        (results: Map<string, string>)
        (remaining: obj list)
        (requestOrdinal: int)
        (collected: StrengthRequestBatch list)
        : StrengthRequestBatch list =
        match remaining with
        | [] -> List.rev collected
        | rawMessage :: tail -> stepHostBatch results tail requestOrdinal collected rawMessage

    and private stepHostBatch
        (results: Map<string, string>)
        (tail: obj list)
        (requestOrdinal: int)
        (collected: StrengthRequestBatch list)
        (rawMessage: obj)
        : StrengthRequestBatch list =
        match classifyHostMessage results requestOrdinal rawMessage with
        | HostBatchStep.Skip -> continueHostBatch results tail requestOrdinal collected
        | HostBatchStep.Stop -> List.rev collected
        | HostBatchStep.Take batch -> continueHostBatch results tail batch.RequestOrdinal (batch :: collected)

    let private collectHostCompleteBatches (rawMessages: obj list) : StrengthRequestBatch list =
        let results = providerResultsByCallId rawMessages
        continueHostBatch results rawMessages 0 []

    let private snapshotOf wire =
        { CurrentProjection = ProviderProjection.toSemantic wire
          CommittedPrefix = None
          BlogFrames = []
          TransportMessages = Set.empty
          HostReanchor = None }

    let private batchesForReplica (rawMessages: obj list) (currentWire: ProviderProjection.ProviderWireProjection) =
        let wireBatches = StrengthBatchCollector.collectCompleteBatches currentWire.Messages

        if not (List.isEmpty wireBatches) then
            wireBatches
        else
            collectHostCompleteBatches rawMessages

    let private localFrameOf
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (batches: StrengthRequestBatch list)
        : Result<StrengthFrameBundle option, StrengthFrameError> =
        match batches with
        | [] -> Ok None
        | _ -> StrengthFrame.tryBuild sha256 binding.MaxFrameBytes batches |> Result.map Some

    let private replicaIntents
        (binding: StrengthReplicaBinding)
        (frame: StrengthFrameBundle option)
        : ProjectionIntent list =
        [ yield
              ProjectionIntent.useStrengthMirror
                  binding.DecisionId
                  binding.TargetProviderRun
                  binding.SemanticDigest
                  binding.LocalizedMirrorMessages
          match frame with
          | Some bundle -> yield ProjectionIntent.strengthReplicaLocal binding.OwnerSessionId binding.DecisionId bundle
          | None -> () ]

    let private retireWith
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replicaSessionId: SessionId)
        (reason: string)
        (batches: StrengthRequestBatch list)
        : Task<StrengthReplicaTransformOutcome> =
        task {
            // Physical identity remains live until the Host reports the child
            // terminal/deletion. This transform only closes semantic admission
            // and aborts before K+1 can leave the process; retiring here races
            // already-queued Host transforms into the Ordinary branch.
            let! _ = sessions.AbortSession replicaSessionId
            return StrengthReplicaTransformOutcome.Retired(reason, batches)
        }

    let private applyPlanned
        (sha256: string -> string)
        (sessionIdText: string)
        (output: obj)
        (currentWire: ProviderProjection.ProviderWireProjection)
        (ordered: ProjectionIntent list)
        (batches: StrengthRequestBatch list)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replicaSessionId: SessionId)
        : Task<StrengthReplicaTransformOutcome> =
        let rendered =
            ProjectionRenderer.renderMessagesWithHostIds sha256 (snapshotOf currentWire) currentWire.Messages ordered

        match ProjectionMessageEdit.tryApplyStrengthRenderedMessages sessionIdText sha256 rendered with
        | Error error -> retireWith runtime sessions replicaSessionId error batches
        | Ok replacement ->
            task {
                HostMessageProjection.replaceMessagesInPlace output replacement
                return StrengthReplicaTransformOutcome.Ready batches
            }

    let private applyWithFrame
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (sessionIdText: string)
        (output: obj)
        (currentWire: ProviderProjection.ProviderWireProjection)
        (frame: StrengthFrameBundle option)
        (batches: StrengthRequestBatch list)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replicaSessionId: SessionId)
        : Task<StrengthReplicaTransformOutcome> =
        match ProjectionPlanner.plan (replicaIntents binding frame) with
        | Error conflict ->
            retireWith runtime sessions replicaSessionId (sprintf "projection-conflict:%A" conflict) batches
        | Ok ordered ->
            applyPlanned sha256 sessionIdText output currentWire ordered batches runtime sessions replicaSessionId

    let private applyUnderBudget
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (sessionIdText: string)
        (output: obj)
        (currentWire: ProviderProjection.ProviderWireProjection)
        (batches: StrengthRequestBatch list)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replicaSessionId: SessionId)
        : Task<StrengthReplicaTransformOutcome> =
        match localFrameOf sha256 binding batches with
        | Error error -> retireWith runtime sessions replicaSessionId (sprintf "invalid-replica-frame:%A" error) batches
        | Ok frame ->
            applyWithFrame
                sha256
                binding
                sessionIdText
                output
                currentWire
                frame
                batches
                runtime
                sessions
                replicaSessionId

    let private applyBatches
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (sessionIdText: string)
        (output: obj)
        (currentWire: ProviderProjection.ProviderWireProjection)
        (batches: StrengthRequestBatch list)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replicaSessionId: SessionId)
        : Task<StrengthReplicaTransformOutcome> =
        if List.length batches >= StrengthBudget.requestLimit binding.Budget then
            retireWith runtime sessions replicaSessionId "provider-request-budget-reached" batches
        else
            applyUnderBudget sha256 binding sessionIdText output currentWire batches runtime sessions replicaSessionId

    let private applyWithBinding
        (sha256: string -> string)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (output: obj)
        (binding: StrengthReplicaBinding)
        (sessionIdText: string)
        (replicaSessionId: SessionId)
        : Task<StrengthReplicaTransformOutcome> =
        task {
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output
            let currentWire = ProviderWireCapture.decodeMessageView rawMessages
            let batches = batchesForReplica rawMessages currentWire

            return!
                applyBatches sha256 binding sessionIdText output currentWire batches runtime sessions replicaSessionId
        }

    let private applyWithSessionId
        (sha256: string -> string)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (output: obj)
        (sessionIdText: string)
        : Task<StrengthReplicaTransformOutcome> =
        let replicaSessionId = SessionId.create sessionIdText

        match runtime.TryFindByReplica replicaSessionId with
        | None -> task { return StrengthReplicaTransformOutcome.NotReplica }
        | Some binding -> applyWithBinding sha256 runtime sessions output binding sessionIdText replicaSessionId

    let apply
        (sha256: string -> string)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (output: obj)
        : Task<StrengthReplicaTransformOutcome> =
        task {
            match ProviderWireDecode.projectionSessionIdFromMessages output with
            | None -> return StrengthReplicaTransformOutcome.NotReplica
            | Some sessionIdText -> return! applyWithSessionId sha256 runtime sessions output sessionIdText
        }
