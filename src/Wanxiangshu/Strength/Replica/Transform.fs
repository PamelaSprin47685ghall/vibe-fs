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

    let private collectToolCalls (parts: ProviderProjection.WirePart list) =
        parts
        |> List.choose (function
            | ProviderProjection.WireToolCall(callId, name, args) -> Some(callId, name, args)
            | _ -> None)

    let private collectToolResults (parts: ProviderProjection.WirePart list) =
        parts
        |> List.choose (function
            | ProviderProjection.WireToolResult(callId, result) -> Some(callId, result)
            | _ -> None)

    let private nonCallParts (parts: ProviderProjection.WirePart list) =
        parts
        |> List.filter (function
            | ProviderProjection.WireToolCall _ -> false
            | _ -> true)

    let private requireDistinctIds (ids: string list) (error: string) : Result<unit, string> =
        if Set.count (Set.ofList ids) <> List.length ids then
            Error error
        else
            Ok()

    let private requireAssistantRole (role: string) : Result<unit, string> =
        if String.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) then
            Ok()
        else
            Error "Strength tool calls must originate from an assistant message"

    let private requireToolRole (role: string) : Result<unit, string> =
        if String.Equals(role, "tool", StringComparison.OrdinalIgnoreCase) then
            Ok()
        else
            Error "Strength tool results must originate from a logical tool message"

    type private StrengthCallBatch =
        Map<string, ToolCallId * string * string> * obj list * int * ProviderProjection.WireMessage * string option

    let private startCallBatch
        (pendingBatch: StrengthCallBatch option)
        (message: ProviderProjection.WireMessage)
        (index: int)
        (hostId: string option)
        (calls: (ToolCallId * string * string) list)
        : Result<StrengthCallBatch option, string> =
        if Option.isSome pendingBatch then
            Error "Strength Host adapter saw a new tool batch before the previous batch completed"
        else
            result {
                do! requireAssistantRole message.Role

                let! regularParts =
                    ProjectionMessageEdit.HostWireEncoding.tryEncodeNonToolParts (nonCallParts message.Parts)

                do!
                    requireDistinctIds
                        (calls |> List.map (fun (id, _, _) -> ToolCallId.value id))
                        "Strength Host adapter refuses duplicate tool call ids in one batch"

                let pendingCalls =
                    calls
                    |> List.map (fun (callId, name, args) -> ToolCallId.value callId, (callId, name, args))
                    |> Map.ofList

                return Some(pendingCalls, regularParts, index, message, hostId)
            }

    let private completeOneResult
        (pendingCalls: Map<string, ToolCallId * string * string>)
        (callId: ToolCallId, resultCanonical: string)
        : Result<obj, string> =
        match Map.tryFind (ToolCallId.value callId) pendingCalls with
        | None -> Error "Strength Host adapter found an orphan tool result"
        | Some(_, name, args) ->
            Ok(ProjectionMessageEdit.HostWireEncoding.completedToolPart callId name args resultCanonical)

    let private requireResultPartsOnly
        (message: ProviderProjection.WireMessage)
        (results: (ToolCallId * string) list)
        : Result<unit, string> =
        if List.length results <> List.length message.Parts then
            Error "Strength tool result message contains non-result parts"
        else
            Ok()

    let private requireBatchCardinality pendingCalls (results: (ToolCallId * string) list) =
        if Map.count pendingCalls <> List.length results then
            Error "Strength Host adapter requires every tool call/result in the request batch"
        else
            Ok()

    let private requirePendingBatch (pendingBatch: StrengthCallBatch option) =
        match pendingBatch with
        | None -> Error "Strength Host adapter found tool results without a preceding call batch"
        | Some batch -> Ok batch

    let private finishResultBatch
        (sessionId: string)
        (sha256: string -> string)
        (pendingBatch: StrengthCallBatch option)
        (message: ProviderProjection.WireMessage)
        (results: (ToolCallId * string) list)
        : Result<obj, string> =
        result {
            let! pendingCalls, regularParts, callIndex, callMessage, callHostId = requirePendingBatch pendingBatch

            do! requireToolRole message.Role
            do! requireResultPartsOnly message results
            do! requireBatchCardinality pendingCalls results

            do!
                requireDistinctIds
                    (results |> List.map (fun (id, _) -> ToolCallId.value id))
                    "Strength Host adapter refuses duplicate tool result ids in one batch"

            let! completed = results |> List.traverseResultM (completeOneResult pendingCalls)

            return
                ProjectionMessageEdit.HostWireEncoding.rawMessage
                    sessionId
                    sha256
                    callIndex
                    callMessage
                    callHostId
                    "assistant"
                    (regularParts @ completed)
        }

    let private emitRegularMessage
        (sessionId: string)
        (sha256: string -> string)
        (pendingBatch: StrengthCallBatch option)
        (index: int)
        (message: ProviderProjection.WireMessage)
        (hostId: string option)
        : Result<obj, string> =
        if Option.isSome pendingBatch then
            Error "Strength Host adapter requires tool results immediately after the tool-call message"
        else
            result {
                let! parts = ProjectionMessageEdit.HostWireEncoding.tryEncodeNonToolParts message.Parts

                return
                    ProjectionMessageEdit.HostWireEncoding.rawMessage
                        sessionId
                        sha256
                        index
                        message
                        hostId
                        message.Role
                        parts
            }

    let private continueRenderedMessage
        (sessionId: string)
        (sha256: string -> string)
        (loop:
            (int * (ProviderProjection.WireMessage * string option * bool)) list
                -> StrengthCallBatch option
                -> obj list
                -> Result<obj list, string>)
        (tail: (int * (ProviderProjection.WireMessage * string option * bool)) list)
        (pendingBatch: StrengthCallBatch option)
        (acc: obj list)
        (index: int)
        (message: ProviderProjection.WireMessage)
        (hostId: string option)
        : Result<obj list, string> =
        let calls = collectToolCalls message.Parts
        let results = collectToolResults message.Parts

        match List.isEmpty calls, List.isEmpty results with
        | false, false -> Error "Strength Host adapter refuses a message mixing tool calls and results"
        | false, true ->
            startCallBatch pendingBatch message index hostId calls
            |> Result.bind (fun nextBatch -> loop tail nextBatch acc)
        | true, false ->
            finishResultBatch sessionId sha256 pendingBatch message results
            |> Result.bind (fun raw -> loop tail None (raw :: acc))
        | true, true ->
            emitRegularMessage sessionId sha256 pendingBatch index message hostId
            |> Result.bind (fun raw -> loop tail None (raw :: acc))

    /// Adapt the logical call/result rows to native completed Host tool parts.
    let tryApplyRenderedMessages
        (sessionId: string)
        (sha256: string -> string)
        (rendered: RenderedMessages)
        : Result<obj list, string> =
        let triples =
            List.zip3 rendered.Messages rendered.HostMessageIds rendered.HostIsPhysical
            |> List.mapi (fun index triple -> index, triple)

        let rec encodeMessages remaining pending acc =
            match remaining, pending with
            | [], None -> Ok(List.rev acc)
            | [], Some _ -> Error "Strength Host adapter ended with an incomplete tool batch"
            | (index, (message, hostId, _)) :: tail, pendingBatch ->
                continueRenderedMessage sessionId sha256 encodeMessages tail pendingBatch acc index message hostId

        encodeMessages triples None []

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
        { CurrentProjection = ProviderProjection.toSemantic wire }

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
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (frame: StrengthFrameBundle option)
        : Result<ProjectionIntent list, StrengthProjectionIntentError> =
        result {
            let! mirror =
                binding.LocalizedMirrorMessages
                |> List.map (fun message ->
                    { Message = message
                      HostMessageId = None
                      HostIsPhysical = false })
                |> StrengthProjectionIntent.projectionMirror binding.DecisionId

            match frame with
            | None -> return [ mirror ]
            | Some bundle ->
                let! local =
                    StrengthProjectionIntent.replicaLocal sha256 binding.OwnerSessionId binding.DecisionId bundle

                return [ mirror; local ]
        }

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
            ProjectionRenderer.renderMessagesWithHostIds (snapshotOf currentWire) currentWire.Messages ordered

        match tryApplyRenderedMessages sessionIdText sha256 rendered with
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
        let planned =
            result {
                let! intents =
                    replicaIntents sha256 binding frame
                    |> Result.mapError (sprintf "projection-intent-refused:%A")

                return!
                    ProjectionPlanner.plan intents
                    |> Result.mapError (sprintf "projection-conflict:%A")
            }

        match planned with
        | Error error -> retireWith runtime sessions replicaSessionId error batches
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
