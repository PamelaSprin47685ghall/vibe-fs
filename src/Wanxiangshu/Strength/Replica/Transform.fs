namespace Wanxiangshu.Strength.Replica

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Strength.Projection

[<RequireQualifiedAccess>]
type StrengthReplicaTransformOutcome =
    | NotReplica
    | Ready of completedBatches: StrengthRequestBatch list
    | Retired of reason: string * completedBatches: StrengthRequestBatch list

/// STRENGTH-003/004/009/014: transform program for the InternalLeaf replica.
/// It bypasses Work recovery/Companion writers, replaces the physical child
/// transcript with the frozen owner mirror, and replays only this decision's
/// completed prior batches. The runtime admission owns the request budget: a
/// request that was not admitted retires here before any physical send.
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

    /// The Host session-shaped encoding: ONE assistant message whose parts are
    /// completed `tool` parts. `ProviderWireDecode` decodes such a part as a
    /// `WireToolResult` only, so the message reaches this adapter with results and
    /// no preceding call batch -- exactly how the owner's own transcript looks.
    /// Such a message is a self-contained completed exchange: the part already
    /// carries the call identity, and the result is the only evidence that exists.
    /// Emitting it verbatim keeps the replica's material byte-identical to what the
    /// Host really recorded. A result with no pending batch on any OTHER message
    /// shape is still an orphan and stays rejected.
    let private isHostCompletedToolMessage
        (message: ProviderProjection.WireMessage)
        (results: (ToolCallId * string) list)
        =
        // Reachable only for a calls-free message carrying results, so the
        // evidence that separates the session shape from an orphan is the role:
        // the Host folds a completed call into the SAME assistant response that
        // produced it, whereas a logical `tool` message only ever holds the
        // result half and therefore still requires a preceding call batch.
        // A Host session-shaped part legitimately sits beside reasoning/text
        // parts, so the non-tool parts are preserved rather than rejected.
        not (List.isEmpty results)
        && String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)

    let private emitHostCompletedExchange
        (sessionId: string)
        (sha256: string -> string)
        (index: int)
        (message: ProviderProjection.WireMessage)
        (hostId: string option)
        (results: (ToolCallId * string) list)
        : Result<obj, string> =
        let nonToolParts =
            message.Parts
            |> List.filter (function
                | ProviderProjection.WireToolResult _ -> false
                | _ -> true)

        match ProjectionMessageEdit.HostWireEncoding.tryEncodeNonToolParts nonToolParts with
        | Error error -> Error error
        | Ok encoded ->
            let completed =
                results
                |> List.map (fun (callId, result) ->
                    ProjectionMessageEdit.HostWireEncoding.completedToolPart callId "" "{}" result)

            Ok(
                ProjectionMessageEdit.HostWireEncoding.rawMessage
                    sessionId
                    sha256
                    index
                    message
                    hostId
                    "assistant"
                    (encoded @ completed)
            )

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

        let handleToolResults results =
            if Option.isSome pendingBatch || not (isHostCompletedToolMessage message results) then
                finishResultBatch sessionId sha256 pendingBatch message results
                |> Result.bind (fun raw -> loop tail None (raw :: acc))
            else
                emitHostCompletedExchange sessionId sha256 index message hostId results
                |> Result.bind (fun raw -> loop tail None (raw :: acc))

        match List.isEmpty calls, List.isEmpty results with
        | false, false -> Error "Strength Host adapter refuses a message mixing tool calls and results"
        | false, true ->
            startCallBatch pendingBatch message index hostId calls
            |> Result.bind (fun nextBatch -> loop tail nextBatch acc)
        | true, false -> handleToolResults results
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

    let private isLogicalCallMessage (raw: obj) =
        ProviderWireDecode.rawPartsOf raw
        |> List.exists (fun part ->
            ProviderWireDecode.firstString part [ "type" ]
            |> Option.exists (fun kind -> kind = "tool-call"))

    let private encodeOwnerBatch (sha256: string -> string) (call: obj) (resultMessage: obj) =
        result {
            let! sessionId =
                ProviderWireDecode.firstString (ProviderWireDecode.infoObject call) [ "sessionID" ]
                |> Result.requireSome "Strength owner tool batch has no Host session id"

            let raw = [ call; resultMessage ]
            let wire = ProviderWireCapture.decodeMessageView raw

            let rendered =
                { Messages = wire.Messages
                  HostMessageIds = raw |> List.map ProviderWireDecode.hostMessageId
                  HostIsPhysical = [ false; false ] }

            return! tryApplyRenderedMessages sessionId sha256 rendered
        }

    /// Keep logical call/result rows intact through XTrace and context projection.
    /// Only at the final Host boundary fold them into native completed tool parts.
    let tryEncodeOwnerMessages (sha256: string -> string) (rawMessages: obj list) : Result<obj list, string> =
        let rec encode remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | call :: result :: tail when isLogicalCallMessage call ->
                encodeOwnerBatch sha256 call result
                |> Result.bind (fun encoded -> encode tail (List.rev encoded @ acc))
            | [ last ] when isLogicalCallMessage last -> Error "Strength owner tool batch is incomplete"
            | raw :: tail -> encode tail (raw :: acc)

        if rawMessages |> List.exists isLogicalCallMessage then
            encode rawMessages []
        else
            Ok rawMessages

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

    let private filterReadonlyBatches (batches: StrengthRequestBatch list) : StrengthRequestBatch list =
        batches
        |> List.choose (fun batch ->
            let allowedExchanges =
                batch.Exchanges
                |> List.filter (fun exchange -> StrengthFrame.isProjectionTool exchange.ToolName)

            if List.isEmpty allowedExchanges then
                None
            else
                Some
                    { batch with
                        Exchanges = allowedExchanges })
        |> List.mapi (fun index batch ->
            { batch with
                RequestOrdinal = index + 1 })

    let private currentDecisionMessages (rawMessages: obj list) =
        let rec loop remaining current =
            match remaining with
            | [] -> current
            | raw :: tail when
                ProviderWireDecode.firstString (ProviderWireDecode.infoObject raw) [ "role" ] = Some "user"
                && (ProviderWireDecode.hostMessageId raw |> Option.isSome)
                ->
                loop tail tail
            | _ :: tail -> loop tail current

        loop rawMessages rawMessages

    let private batchesForReplica (rawMessages: obj list) (currentWire: ProviderProjection.ProviderWireProjection) =
        let decisionMessages = currentDecisionMessages rawMessages

        let decisionWire =
            if Object.ReferenceEquals(decisionMessages, rawMessages) then
                currentWire
            else
                ProviderWireCapture.decodeMessageView decisionMessages

        let wireBatches =
            StrengthBatchCollector.collectCompleteBatches decisionWire.Messages

        let candidateBatches =
            if not (List.isEmpty wireBatches) then
                wireBatches
            else
                collectHostCompleteBatches decisionMessages

        filterReadonlyBatches candidateBatches

    let private localFrameOf
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (batches: StrengthRequestBatch list)
        : Result<StrengthFrameBundle option, StrengthFrameError> =
        match batches with
        | [] -> Ok None
        | _ -> StrengthFrame.tryBuild sha256 batches |> Result.map Some

    /// The main <-> predictor bijection is a class, not a local derivation:
    /// see TwinBijection.restore for the statement and its properties.
    /// The twin's request for this decision.
    ///
    ///   R_n = sigma(C_{n-1}, main_n)
    ///
    /// sigma walks the two histories together: the owner's tool exchanges are
    /// re-emitted at their owner positions, and the replica's OWN speech is
    /// restored in the gaps the child recorded. Speech is the material main never
    /// receives (the projection emits calls and results only), and its position is
    /// knowable solely from the child — which is exactly why it must come from
    /// there and not from main. Dropping it, or appending it at the end, would make
    /// the provider see a different sequence than the one it cached.
    ///
    /// Byte stability: while the owner's sealed region is unchanged the owner's
    /// messages re-emit identically and the child's speech keeps its gaps, so
    /// R_{n-1} stays a prefix of R_n up to the first position main actually
    /// rewrote. A fresh child has no exchanges and no speech, so the request is the
    /// owner transcript alone — today's behaviour, byte for byte.
    let private replicaIntents
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (childMessages: ProviderProjection.WireMessage list)
        (frame: StrengthFrameBundle option)
        : Result<ProjectionIntent list, StrengthProjectionIntentError> =
        result {
            let owner = binding.LocalizedMirrorMessages

            let aligned = TwinBijection.restore childMessages owner

            let! mirror =
                aligned
                |> List.map (fun message ->
                    { Message = message
                      HostMessageId = None
                      HostIsPhysical = false })
                |> StrengthProjectionIntent.projectionMirror

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
            // The retire reason is the only place a replica's early end is
            // explained; without it an aborted child is indistinguishable from a
            // crash. Emit it before the abort so operators can diagnose the
            // decision instead of guessing from raw Host cancellation lines.
            Diagnostic.emit
                "strength-replica-retired"
                [ "replica_session_id", SessionId.value replicaSessionId; "result", reason ]

            // Physical identity remains live until the Host reports the child
            // terminal/deletion. This transform only closes semantic admission
            // and aborts before the unadmitted request can leave the process;
            // retiring here races already-queued Host transforms into the
            // Ordinary branch.
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
        (childMessages: ProviderProjection.WireMessage list)
        (frame: StrengthFrameBundle option)
        (batches: StrengthRequestBatch list)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replicaSessionId: SessionId)
        : Task<StrengthReplicaTransformOutcome> =
        let planned =
            result {
                let! intents =
                    replicaIntents sha256 binding childMessages frame
                    |> Result.mapError (sprintf "projection-intent-refused:%A")

                return!
                    ProjectionPlanner.plan intents
                    |> Result.mapError (sprintf "projection-conflict:%A")
            }

        match planned with
        | Error error -> retireWith runtime sessions replicaSessionId error batches
        | Ok ordered ->
            applyPlanned sha256 sessionIdText output currentWire ordered batches runtime sessions replicaSessionId

    // Mirrors the frozen owner conversation plus this decision's completed
    // batches for a request the runtime admission already allowed.
    let private applyUnderBudget
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (sessionIdText: string)
        (output: obj)
        (currentWire: ProviderProjection.ProviderWireProjection)
        (childMessages: ProviderProjection.WireMessage list)
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
                childMessages
                frame
                batches
                runtime
                sessions
                replicaSessionId

    /// DELEGATE-5.3: the outbound request gate is the live registry's real
    /// admission, never the visible batch count. `outboundRequest` marks a new
    /// physical request; the registry's verdict decides, and a request that is
    /// not admitted retires before any physical N+1 send.
    let private applyBatches
        (sha256: string -> string)
        (binding: StrengthReplicaBinding)
        (sessionIdText: string)
        (output: obj)
        (currentWire: ProviderProjection.ProviderWireProjection)
        (childMessages: ProviderProjection.WireMessage list)
        (batches: StrengthRequestBatch list)
        (outboundRequest: bool)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (replicaSessionId: SessionId)
        : Task<StrengthReplicaTransformOutcome> =
        // DELEGATE-5.3: the live registry owns the request budget. A new
        // outbound request asks it for admission and retires before any
        // physical N+1 send when refused; mirroring an already-admitted request
        // again consumes nothing.
        let admitted = (not outboundRequest) || runtime.TryAdmitRequest replicaSessionId

        if not admitted then
            retireWith runtime sessions replicaSessionId "provider-request-budget-reached" batches
        else
            applyUnderBudget
                sha256
                binding
                sessionIdText
                output
                currentWire
                childMessages
                batches
                runtime
                sessions
                replicaSessionId

    let private applyWithBinding
        (sha256: string -> string)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (output: obj)
        (binding: StrengthReplicaBinding)
        (sessionIdText: string)
        (replicaSessionId: SessionId)
        (outboundRequest: bool)
        : Task<StrengthReplicaTransformOutcome> =
        task {
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output
            let currentWire = ProviderWireCapture.decodeMessageView rawMessages
            let batches = batchesForReplica rawMessages currentWire

            // The child's own history; the alignment decides which parts of it are
            // the replica's own material.
            let childMessages = currentWire.Messages

            return!
                applyBatches
                    sha256
                    binding
                    sessionIdText
                    output
                    currentWire
                    childMessages
                    batches
                    outboundRequest
                    runtime
                    sessions
                    replicaSessionId
        }

    let private applyWithSessionId
        (sha256: string -> string)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (output: obj)
        (sessionIdText: string)
        (outboundRequest: bool)
        : Task<StrengthReplicaTransformOutcome> =
        let replicaSessionId = SessionId.create sessionIdText

        match runtime.TryFindByReplica replicaSessionId with
        | None -> task { return StrengthReplicaTransformOutcome.NotReplica }
        | Some binding ->
            applyWithBinding sha256 runtime sessions output binding sessionIdText replicaSessionId outboundRequest

    let apply
        (sha256: string -> string)
        (runtime: StrengthRuntime)
        (sessions: ISessionHostPort)
        (output: obj)
        (outboundRequest: bool)
        : Task<StrengthReplicaTransformOutcome> =
        task {
            match ProviderWireDecode.projectionSessionIdFromMessages output with
            | None -> return StrengthReplicaTransformOutcome.NotReplica
            | Some sessionIdText ->
                return! applyWithSessionId sha256 runtime sessions output sessionIdText outboundRequest
        }
