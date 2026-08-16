namespace Wanxiangshu.Strength

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Resources
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// JS-native owner surface for Strength semantics.
///
/// Strength records, unions, identities, collections and live registries remain
/// private to their owners. Tests cross this module with JSON-shaped values and
/// opaque handles only; Fable representation is never a contract.
module StrengthSurface =

    type private EventHandle(event: StrengthEvent) =
        member _.Value = event

    type private ProjectionHandle(projection: StrengthProjection) =
        member _.Value = projection

    type private EnvelopeHandle(envelope: EventEnvelope) =
        member _.Value = envelope

    type private DurabilityHandle(port: StrengthDurabilityPort) =
        member _.Value = port

    type private RuntimeHandle(runtime: StrengthRuntime) =
        member _.Value = runtime

    type private PredictorHandle(state: StrengthPredictorState) =
        member val State = state with get, set

    let private isUndefined (value: obj) : bool =
        emitJsExpr value "$0 === undefined"

    let private isNullish (value: obj) = isNull value || isUndefined value

    let private arrayOf (value: obj) : obj array =
        if isNullish value then [||] else unbox<obj array> value

    let private textOf (value: obj) =
        if isNullish value then "" else string value

    let private optionalText (value: obj) =
        if isNullish value then None else Some(string value)

    let private roleOf (value: obj) =
        Roles.tryParseRole (textOf value) |> Option.defaultValue Role.Coder

    let private tierOf (value: obj) =
        Roles.tryParseTier (textOf value) |> Option.defaultValue AgentTier.Deep

    let private budgetOf (value: obj) =
        StrengthBudget.parse (textOf value) |> Option.defaultValue StrengthBudget.K0

    let private requestKindOf (value: obj) =
        match textOf value with
        | "work-main" -> ProviderRequestKind.WorkMain
        | "blogger-main" -> ProviderRequestKind.BloggerMain
        | "blogger-squash" -> ProviderRequestKind.BloggerSquash
        | "interaction-repair" -> ProviderRequestKind.InteractionRepair
        | "strength-replica" -> ProviderRequestKind.StrengthReplica
        | _ -> ProviderRequestKind.WorkMain

    let private roleLabel role = Roles.roleLabel role

    let private permissionLabel permission =
        match permission with
        | ToolPermission.Fork -> "Fork"
        | ToolPermission.Join -> "Join"
        | ToolPermission.Horizon -> "Horizon"
        | ToolPermission.TodoWrite -> "TodoWrite"
        | ToolPermission.Fission -> "Fission"
        | ToolPermission.Read -> "Read"
        | ToolPermission.Write -> "Write"
        | ToolPermission.Edit -> "Edit"
        | ToolPermission.Glob -> "Glob"
        | ToolPermission.Grep -> "Grep"
        | ToolPermission.Move -> "Move"
        | ToolPermission.Remove -> "Remove"
        | ToolPermission.Inspect -> "Inspect"
        | ToolPermission.Behavior -> "Behavior"
        | ToolPermission.Exec -> "Exec"
        | ToolPermission.Pty -> "Pty"
        | ToolPermission.Network -> "Network"
        | ToolPermission.Judge -> "Judge"
        | ToolPermission.Chronicle -> "Chronicle"
        | ToolPermission.Fetch -> "Fetch"
        | ToolPermission.Finality -> "Finality"
        | ToolPermission.BashHoneypot -> "BashHoneypot"
        | ToolPermission.Sphinx -> "Sphinx"

    let private permissionsToJs permissions =
        permissions |> Set.toList |> List.map permissionLabel |> List.sort |> List.toArray

    let private partOf (value: obj) : MessagePart =
        match textOf value?kind with
        | "text" -> MessagePart.Text(textOf value?text)
        | "reasoning" -> MessagePart.Reasoning(textOf value?text)
        | "tool-call" -> MessagePart.ToolCall(textOf value?callId, textOf value?name, textOf value?args)
        | "tool-result" -> MessagePart.ToolResult(textOf value?callId, textOf value?result)
        | _ -> MessagePart.Activity(textOf value?kind)

    let private wirePartOf (value: obj) : ProviderProjection.WirePart =
        match textOf value?kind with
        | "text" -> ProviderProjection.WireText(textOf value?text)
        | "reasoning" -> ProviderProjection.WireReasoning(textOf value?text)
        | "tool-call" ->
            ProviderProjection.WireToolCall(ToolCallId.create (textOf value?callId), textOf value?name, textOf value?args)
        | "tool-result" ->
            ProviderProjection.WireToolResult(ToolCallId.create (textOf value?callId), textOf value?result)
        | "media" ->
            ProviderProjection.WireMedia(optionalText value?mediaType, textOf value?contentDigest)
        | other -> failwithf "StrengthSurface: unknown wire part kind %s" other

    let private wireMessageOf (value: obj) : ProviderProjection.WireMessage =
        { Role = textOf value?role
          Parts = arrayOf value?parts |> Array.toList |> List.map wirePartOf }

    let private wirePartToJs (part: ProviderProjection.WirePart) : obj =
        match part with
        | ProviderProjection.WireText value -> box {| kind = "text"; text = value |}
        | ProviderProjection.WireReasoning value -> box {| kind = "reasoning"; text = value |}
        | ProviderProjection.WireToolCall(id, name, args) ->
            box {| kind = "tool-call"; callId = ToolCallId.value id; name = name; args = args |}
        | ProviderProjection.WireToolResult(id, result) ->
            box {| kind = "tool-result"; callId = ToolCallId.value id; result = result |}
        | ProviderProjection.WireMedia(mediaType, digest) ->
            box {| kind = "media"; mediaType = Option.toObj mediaType; contentDigest = digest |}

    let private wireMessageToJs (message: ProviderProjection.WireMessage) : obj =
        box {| role = message.Role; parts = message.Parts |> List.map wirePartToJs |> List.toArray |}

    let private messagesOf (value: obj) =
        arrayOf value |> Array.toList |> List.map wireMessageOf

    let private exchangesOf (value: obj) : StrengthToolExchange list =
        arrayOf value
        |> Array.toList
        |> List.map (fun exchange ->
            { ToolName = textOf exchange?toolName
              CanonicalArguments = textOf exchange?canonicalArguments
              CanonicalResult = textOf exchange?canonicalResult })

    let private batchesOf (value: obj) : StrengthRequestBatch list =
        arrayOf value
        |> Array.toList
        |> List.map (fun batch ->
            { RequestOrdinal = int (textOf batch?requestOrdinal)
              Exchanges = exchangesOf batch?exchanges })

    let private bundleOf (value: obj) : StrengthFrameBundle =
        { Batches = batchesOf value?batches
          Digest = textOf value?digest
          ByteLength = int (textOf value?byteLength) }

    let private exchangeToJs (exchange: StrengthToolExchange) : obj =
        box
            {| toolName = exchange.ToolName
               canonicalArguments = exchange.CanonicalArguments
               canonicalResult = exchange.CanonicalResult |}

    let private batchToJs (batch: StrengthRequestBatch) : obj =
        box
            {| requestOrdinal = batch.RequestOrdinal
               exchanges = batch.Exchanges |> List.map exchangeToJs |> List.toArray |}

    let private bundleToJs (bundle: StrengthFrameBundle) : obj =
        box
            {| batches = bundle.Batches |> List.map batchToJs |> List.toArray
               digest = bundle.Digest
               byteLength = bundle.ByteLength |}

    let private errorName error =
        match error with
        | StrengthFrameError.EmptyBundle -> "EmptyBundle"
        | StrengthFrameError.EmptyBatch _ -> "EmptyBatch"
        | StrengthFrameError.InvalidRequestOrdinal _ -> "InvalidRequestOrdinal"
        | StrengthFrameError.UnsupportedTool _ -> "UnsupportedTool"
        | StrengthFrameError.ByteLimitExceeded _ -> "ByteLimitExceeded"

    let private resultToJs valueOf errorOf result =
        match result with
        | Ok value -> box {| ok = true; value = valueOf value |}
        | Error error -> box {| ok = false; error = errorOf error |}

    /// Build one deterministic frame bundle from plain request batches.
    let frameTryBuild (sha256: string -> string) (maxBytes: int) (batches: obj array) : obj =
        StrengthFrame.tryBuild sha256 maxBytes (batchesOf batches)
        |> resultToJs bundleToJs errorName

    /// Localize owner wire ids into decision-local ids without changing semantics.
    let frameTryLocalizeMirror
        (sha256: string -> string)
        (decisionId: string)
        (semanticDigest: string)
        (messages: obj array)
        : obj =
        let errorName =
            function
            | StrengthMirrorError.DuplicateToolCallId _ -> "DuplicateToolCallId"
            | StrengthMirrorError.OrphanToolResultId _ -> "OrphanToolResultId"
            | StrengthMirrorError.MediaCannotCrossSession -> "MediaCannotCrossSession"

        StrengthFrame.tryLocalizeMirror sha256 (StrengthDecisionId.create decisionId) semanticDigest (messagesOf messages)
        |> resultToJs (List.map wireMessageToJs >> List.toArray) errorName

    let frameWireToolCallId
        (sha256: string -> string)
        (ownerSessionId: string)
        (decisionId: string)
        (requestOrdinal: int)
        (exchangeOrdinal: int)
        (semanticDigest: string)
        : string =
        StrengthFrame.wireToolCallId
            sha256
            (SessionId.create ownerSessionId)
            (StrengthDecisionId.create decisionId)
            requestOrdinal
            exchangeOrdinal
            semanticDigest

    let collectCompleteBatches (messages: obj array) : obj array =
        StrengthBatchCollector.collectCompleteBatches (messagesOf messages)
        |> List.map batchToJs
        |> List.toArray

    let renderWire (messages: obj array) : string =
        let wire: ProviderProjection.ProviderWireProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = messagesOf messages }

        wire |> ProviderProjection.renderWire

    let renderSemantic (messages: obj array) : string =
        let wire: ProviderProjection.ProviderWireProjection =
            { ProviderId = None
              ModelId = None
              Variant = None
              Tools = []
              System = []
              Messages = messagesOf messages }

        wire
        |> ProviderProjection.toSemantic
        |> ProviderProjection.renderSemantic

    let costEstimate
        (p1: float)
        (p2: float)
        (savedDeep1: float)
        (savedDeep2: float)
        (fast1: float)
        (fast2: float)
        (byte1: float)
        (byte2: float)
        (delay1: float)
        (delay2: float)
        (risk1: float)
        (risk2: float)
        : obj =
        let estimate = StrengthCostModel.estimate p1 p2 savedDeep1 savedDeep2 fast1 fast2 byte1 byte2 delay1 delay2 risk1 risk2
        box {| V0 = estimate.V0; V1 = estimate.V1; V2 = estimate.V2 |}

    let private opportunityOf (value: obj) : StrengthOpportunity =
        { IsRootWork = unbox<bool> value?isRootWork
          RequestKind = requestKindOf value?requestKind
          CanonicalRole = roleOf value?canonicalRole
          SelectedTier = tierOf value?selectedTier
          SelectedAgent = textOf value?selectedAgent
          EffectiveAgent = textOf value?effectiveAgent
          IsFallbackRetry = unbox<bool> value?isFallbackRetry
          HasPrefixProbe = unbox<bool> value?hasPrefixProbe
          IsReviewerOrFinality = unbox<bool> value?isReviewerOrFinality
          IsAttachedOrInternalLeaf = unbox<bool> value?isAttachedOrInternalLeaf
          OwnerCancelled = unbox<bool> value?ownerCancelled
          TargetProviderRunBound = unbox<bool> value?targetProviderRunBound
          EventStoreHealthy = unbox<bool> value?eventStoreHealthy
          HostCanaryHealthy = unbox<bool> value?hostCanaryHealthy
          FastPeerAvailable = unbox<bool> value?fastPeerAvailable
          CostModelAvailable = unbox<bool> value?costModelAvailable }

    let private predictionOf (value: obj) =
        { P1 = float value?P1
          P2 = float value?P2
          EvidenceCount = int value?evidenceCount }

    let private estimateOf (value: obj) =
        { V0 = float value?V0; V1 = float value?V1; V2 = float value?V2 }

    let private policyConfigOf (value: obj) =
        { K1Margin = float value?K1Margin
          K2Margin = float value?K2Margin
          K2MinimumEvidence = int value?K2MinimumEvidence }

    let policyDecide (opportunity: obj) (control: bool) (shadow: bool) (prediction: obj) (estimate: obj) (config: obj) : obj =
        match StrengthPolicy.decideFromFacts
            (opportunityOf opportunity)
            control
            shadow
            (predictionOf prediction)
            (estimateOf estimate)
            (policyConfigOf config) with
        | StrengthDecision.Skip reason -> box {| kind = "Skip"; reason = reason; budget = "K0" |}
        | StrengthDecision.ControlHoldout -> box {| kind = "ControlHoldout"; budget = "K0" |}
        | StrengthDecision.Speculate(budget, value) ->
            box
                {| kind = "Speculate"
                   budget = StrengthBudget.wire budget
                   estimate = {| V0 = value.V0; V1 = value.V1; V2 = value.V2 |} |}

    let policyControlBucket (sha256: string -> string) (policyVersion: string) (authorityRoot: string) (targetRun: string) =
        StrengthPolicy.controlBucket sha256 policyVersion authorityRoot targetRun

    let policyIsControlHoldout (rateBasisPoints: int) (bucket: int) =
        StrengthPolicy.isControlHoldout rateBasisPoints bucket

    let readonlyCapabilities (role: string) (requestKind: string) : string array =
        PromptAuthority.toolCapabilitiesFor (roleOf (box role)) (requestKindOf (box requestKind))
        |> permissionsToJs

    /// StrengthReplica readonly capability labels for a canonical role.
    /// Kept as the short owner name consumed by policy and authority laws.
    let capabilities (role: string) : string array =
        readonlyCapabilities role "strength-replica"

    /// Prompt identity remains role-owned and cannot inherit Strength metadata.
    let systemPromptIdForRole (role: string) : string =
        PromptAuthority.systemPromptIdFor (roleOf (box role)) |> SystemPromptId.value

    let systemPromptForRole (role: string) : string =
        let prompts = RuntimeResources.current().Prompts
        match roleOf (box role) with
        | Role.Manager -> prompts.ManagerSystemPrompt
        | Role.Coder -> prompts.CoderSystemPrompt
        | Role.DevOps -> prompts.DevopsSystemPrompt
        | Role.Inspector -> prompts.InspectorSystemPrompt
        | Role.Reviewer -> prompts.ReviewerSystemPrompt
        | Role.Browser -> prompts.BrowserSystemPrompt
        | Role.Inquiry -> prompts.InquirySystemPrompt
        | Role.Orchestrator -> prompts.OrchestratorSystemPrompt
        | Role.Distiller -> prompts.DistillerSystemPrompt
        | Role.Blogger -> prompts.BloggerSystemPrompt

    let clearsFailureCountOnSuccess (requestKind: string) =
        ProviderRequestKind.clearsFailureCountOnSuccess (requestKindOf (box requestKind))

    let mayCarryProbe (requestKind: string) =
        ProviderRequestKind.mayCarryProbe (requestKindOf (box requestKind))

    let associationFacts (ownerSessionId: string) : obj =
        let ownership = StrengthReplicaAssociationHints.ownership (SessionId.create ownerSessionId)
        let owner, attachment =
            match ownership with
            | SessionOwnership.Attached(owner, AttachmentKind.StrengthReplica) -> SessionId.value owner, "StrengthReplica"
            | _ -> "", ""

        box
            {| satelliteCases = [| "Companion" |]
               hasReplicaSatellite = false
               attachmentCases = [| "Companion"; "SyncInspector"; "SyncCoder"; "Bookkeeper"; "StrengthReplica" |]
               executionClass =
                match StrengthReplicaAssociationHints.executionClass with
                | SessionExecutionClass.InternalLeaf -> "InternalLeaf"
                | SessionExecutionClass.Work -> "Work"
               ownerSessionId = owner
               attachment = attachment
               strengthReplicaAttachment = StrengthReplicaAssociationHints.isStrengthReplicaAttachment AttachmentKind.StrengthReplica
               companionAttachment = StrengthReplicaAssociationHints.isStrengthReplicaAttachment AttachmentKind.Companion |}

    let private decisionOfResult (value: obj) =
        match textOf value?kind with
        | "Committed" -> StrengthAppendOutcome.Committed
        | "Rejected" -> StrengthAppendOutcome.Rejected
        | _ -> StrengthAppendOutcome.CommitUnknown

    let private durableEvidenceOf (value: obj) =
        match textOf value with
        | "Matches" -> StrengthDurableEvidence.Matches
        | "Absent" -> StrengthDurableEvidence.Absent
        | "Conflicts" -> StrengthDurableEvidence.Conflicts
        | _ -> StrengthDurableEvidence.Unknown

    let private commitDecisionName decision =
        match decision with
        | StrengthCommitDecision.Proceed -> "Proceed"
        | StrengthCommitDecision.FallBackK0 -> "FallBackK0"
        | StrengthCommitDecision.RetryAppend -> "RetryAppend"
        | StrengthCommitDecision.FailClosed -> "FailClosed"

    let commitResolvePrepared (appendOutcome: string) (evidence: string) =
        StrengthCommit.resolvePrepared (decisionOfResult (box {| kind = appendOutcome |})) (durableEvidenceOf (box evidence))
        |> commitDecisionName

    let commitResolvePromotion (appendOutcome: string) (evidence: string) =
        StrengthCommit.resolvePromotion (decisionOfResult (box {| kind = appendOutcome |})) (durableEvidenceOf (box evidence))
        |> commitDecisionName

    let promotionDecide (targetRun: string) (observedRun: string) (evidence: string) =
        let output =
            match evidence with
            | "RealOutput" -> StrengthProviderOutputEvidence.RealOutput
            | "TransportOnly" -> StrengthProviderOutputEvidence.TransportOnly
            | _ -> StrengthProviderOutputEvidence.NoOutput

        match StrengthPromotion.decide (ProviderRunIdentity.create targetRun) (ProviderRunIdentity.create observedRun) output with
        | StrengthPromotionDecision.Promote -> "Promote"
        | StrengthPromotionDecision.IgnoreWrongRun -> "IgnoreWrongRun"
        | StrengthPromotionDecision.AwaitOrAbandon -> "AwaitOrAbandon"

    let eventPrepared
        (owner: string)
        (decision: string)
        (target: string)
        (replica: string)
        (budget: string)
        (anchor: string)
        (digest: string)
        (byteLength: int)
        (refs: string array)
        : obj =
        EventHandle(
            StrengthEvents.prepared
                (SessionId.create owner)
                (StrengthDecisionId.create decision)
                (ProviderRunIdentity.create target)
                (SessionId.create replica)
                (budgetOf (box budget))
                anchor
                digest
                byteLength
                (refs |> Array.toList |> List.map PayloadRef.create)
        )
        :> obj

    let eventPromoted (owner: string) (decision: string) (target: string) (digest: string) (refs: string array) : obj =
        EventHandle(
            StrengthEvents.promoted
                (SessionId.create owner)
                (StrengthDecisionId.create decision)
                (ProviderRunIdentity.create target)
                digest
                (refs |> Array.toList |> List.map PayloadRef.create)
        )
        :> obj

    let eventTraced (decision: string) (startInclusive: int64) (endExclusive: int64) : obj =
        EventHandle(StrengthEvents.traced (StrengthDecisionId.create decision) startInclusive endExclusive) :> obj

    let eventAbandoned (decision: string) (target: string) : obj =
        EventHandle(StrengthEvents.abandoned (StrengthDecisionId.create decision) (ProviderRunIdentity.create target)) :> obj

    let private eventOf (value: obj) = unbox<EventHandle> value |> fun handle -> handle.Value

    let eventType (value: obj) =
        match eventOf value with
        | StrengthEvent.Prepared _ -> "StrengthCandidatePrepared"
        | StrengthEvent.Promoted _ -> "StrengthCandidatePromoted"
        | StrengthEvent.Traced _ -> "StrengthFramesTraced"
        | StrengthEvent.Abandoned _ -> "StrengthCandidateAbandoned"

    let eventView (value: obj) : obj =
        match eventOf value with
        | StrengthEvent.Prepared event ->
            box
                {| kind = "Prepared"
                   ownerSessionId = SessionId.value event.OwnerSessionId
                   decisionId = StrengthDecisionId.value event.DecisionId
                   targetProviderRun = ProviderRunIdentity.value event.TargetProviderRun
                   replicaSessionId = SessionId.value event.ReplicaSessionId
                   budget = StrengthBudget.wire event.Budget
                   anchorDigest = event.AnchorDigest
                   frameDigest = event.FrameDigest
                   byteLength = event.ByteLength
                   materialPayloads = event.MaterialPayloads |> List.map PayloadRef.value |> List.toArray |}
        | StrengthEvent.Promoted event ->
            box
                {| kind = "Promoted"
                   ownerSessionId = SessionId.value event.OwnerSessionId
                   decisionId = StrengthDecisionId.value event.DecisionId
                   targetProviderRun = ProviderRunIdentity.value event.TargetProviderRun
                   frameDigest = event.FrameDigest
                   materialPayloads = event.MaterialPayloads |> List.map PayloadRef.value |> List.toArray |}
        | StrengthEvent.Traced event ->
            box {| kind = "Traced"; decisionId = StrengthDecisionId.value event.DecisionId; startInclusive = event.StartInclusive; endExclusive = event.EndExclusive |}
        | StrengthEvent.Abandoned event ->
            box {| kind = "Abandoned"; decisionId = StrengthDecisionId.value event.DecisionId; targetProviderRun = ProviderRunIdentity.value event.TargetProviderRun |}

    let private projectionOf value = unbox<ProjectionHandle> value |> fun handle -> handle.Value

    let projectionEmpty () : obj = ProjectionHandle StrengthProjection.empty :> obj

    let private projectionErrorName error =
        match error with
        | StrengthProjectionError.PreparedConflict _ -> "PreparedConflict"
        | StrengthProjectionError.TargetAlreadyBound _ -> "TargetAlreadyBound"
        | StrengthProjectionError.PromotionWithoutPrepared _ -> "PromotionWithoutPrepared"
        | StrengthProjectionError.PromotionMismatch _ -> "PromotionMismatch"
        | StrengthProjectionError.PromotionAfterAbandon _ -> "PromotionAfterAbandon"
        | StrengthProjectionError.TraceWithoutPrepared _ -> "TraceWithoutPrepared"
        | StrengthProjectionError.TraceWithoutPromotion _ -> "TraceWithoutPromotion"
        | StrengthProjectionError.InvalidTraceRange _ -> "InvalidTraceRange"
        | StrengthProjectionError.TraceConflict _ -> "TraceConflict"
        | StrengthProjectionError.AbandonWithoutPrepared _ -> "AbandonWithoutPrepared"
        | StrengthProjectionError.AbandonMismatch _ -> "AbandonMismatch"
        | StrengthProjectionError.AbandonAfterPromotion _ -> "AbandonAfterPromotion"

    let private preparedToJs (event: StrengthCandidatePrepared) : obj =
        box
            {| ownerSessionId = SessionId.value event.OwnerSessionId
               decisionId = StrengthDecisionId.value event.DecisionId
               targetProviderRun = ProviderRunIdentity.value event.TargetProviderRun
               replicaSessionId = SessionId.value event.ReplicaSessionId
               budget = StrengthBudget.wire event.Budget
               anchorDigest = event.AnchorDigest
               frameDigest = event.FrameDigest
               byteLength = event.ByteLength
               materialPayloads = event.MaterialPayloads |> List.map PayloadRef.value |> List.toArray |}

    let private candidateViewToJs (view: StrengthCandidateView) : obj =
        box
            {| prepared = preparedToJs view.Prepared
               promoted = view.Promoted
               abandoned = view.Abandoned
               traceRange =
                view.TraceRange
                |> Option.map (fun range -> box {| startInclusive = range.StartInclusive; endExclusive = range.EndExclusive |})
                |> Option.toObj |}

    let projectionApply (projection: obj) (event: obj) : obj =
        match StrengthProjection.apply (projectionOf projection) (eventOf event) with
        | Ok next -> box {| ok = true; value = (ProjectionHandle next :> obj) |}
        | Error error -> box {| ok = false; error = projectionErrorName error |}

    let projectionHasPrepared (decision: string) (projection: obj) =
        StrengthProjection.hasPrepared (StrengthDecisionId.create decision) (projectionOf projection)

    let projectionIsPromoted (decision: string) (projection: obj) =
        StrengthProjection.isPromoted (StrengthDecisionId.create decision) (projectionOf projection)

    let projectionDecisionForTarget (target: string) (projection: obj) =
        match StrengthProjection.tryDecisionForTarget (ProviderRunIdentity.create target) (projectionOf projection) with
        | Some value -> StrengthDecisionId.value value
        | None -> null

    let projectionCandidate (decision: string) (projection: obj) =
        match StrengthProjection.tryCandidate (StrengthDecisionId.create decision) (projectionOf projection) with
        | Some view -> candidateViewToJs view
        | None -> null

    let projectionTraceRange (decision: string) (projection: obj) =
        match StrengthProjection.tryTraceRange (StrengthDecisionId.create decision) (projectionOf projection) with
        | Some range -> box {| startInclusive = range.StartInclusive; endExclusive = range.EndExclusive |}
        | None -> null

    let storeToEnvelope (sha256: string -> string) (event: obj) : obj =
        EnvelopeHandle(StrengthStore.toEnvelope sha256 (eventOf event)) :> obj

    let private envelopeOf value = unbox<EnvelopeHandle> value |> fun handle -> handle.Value

    let envelopeView (value: obj) : obj =
        let envelope = envelopeOf value
        box
            {| id = EventId.value envelope.EventId
               stream = EventStreamId.value envelope.StreamId
               eventType = envelope.EventType
               parents = envelope.Parents |> List.map EventId.value |> List.toArray
               payloadRefs = envelope.PayloadRefs |> List.map PayloadRef.value |> List.toArray |}

    let storeTryDecodeEnvelope (value: obj) : obj =
        match StrengthStore.tryDecodeEnvelope (envelopeOf value) with
        | Ok event -> box {| ok = true; value = eventView (EventHandle event :> obj) |}
        | Error error -> box {| ok = false; error = error |}

    let private appendErrorName error =
        match error with
        | AppendError.StorageInvalid invalid ->
            match invalid with
            | StorageInvalid.IdentityCollision _ -> "IdentityCollision"
            | StorageInvalid.NonCanonical _ -> "NonCanonical"
            | StorageInvalid.MalformedEnvelope _ -> "MalformedEnvelope"
            | StorageInvalid.MissingParent _ -> "MissingParent"
            | StorageInvalid.CyclicParents -> "CyclicParents"
            | StorageInvalid.MissingPayload _ -> "MissingPayload"
            | StorageInvalid.UnknownEventType _ -> "UnknownEventType"
        | AppendError.SemanticCut _ -> "SemanticCut"
        | AppendError.AppendFailed _ -> "AppendFailed"

    let storeAppend (store: obj) (sha256: string -> string) (event: obj) : Task<obj> =
        task {
            let! result = StrengthStore.append (EventStoreStrengthSurface.storeOf store) sha256 (eventOf event)
            return
                match result with
                | Ok() -> box {| ok = true |}
                | Error error -> box {| ok = false; error = appendErrorName error |}
        }

    let storeWritePayload (store: obj) (bytes: byte array) : Task<obj> =
        task {
            let! result = EventStoreStrengthSurface.writePayload store bytes
            return
                match result with
                | Ok value -> box {| ok = true; value = PayloadRef.value value |}
                | Error error -> box {| ok = false; error = error |}
        }

    let storeReadPayload (store: obj) (reference: string) : Task<obj> =
        task {
            let! result = EventStoreStrengthSurface.readPayload store (PayloadRef.create reference)
            return
                match result with
                | Ok(Some bytes) -> box {| ok = true; value = bytes |}
                | Ok None -> box {| ok = true; value = null |}
                | Error error -> box {| ok = false; error = error |}
        }

    let storeCurrent (store: obj) : obj =
        match EventStoreStrengthSurface.current store "Strength" with
        | Some value -> ProjectionHandle(unbox<StrengthProjection> value) :> obj
        | None -> ProjectionHandle StrengthProjection.empty :> obj

    let durabilityCreate (store: obj) : obj =
        DurabilityHandle(EventStoreStrengthSurface.durability store) :> obj

    let private durabilityOf value = unbox<DurabilityHandle> value |> fun handle -> handle.Value

    let durabilityPublishPrepared (durability: obj) (request: obj) : Task<obj> =
        let value = durabilityOf durability
        let preparedRequest =
            { OwnerSessionId = SessionId.create (textOf request?ownerSessionId)
              DecisionId = StrengthDecisionId.create (textOf request?decisionId)
              TargetProviderRun = ProviderRunIdentity.create (textOf request?targetProviderRun)
              ReplicaSessionId = SessionId.create (textOf request?replicaSessionId)
              Budget = budgetOf request?budget
              AnchorDigest = textOf request?anchorDigest
              Bundle = bundleOf request?bundle }

        task {
            let! result = value.PublishPrepared preparedRequest
            return
                match result with
                | StrengthPreparedPublish.Published -> box {| kind = "Published" |}
                | StrengthPreparedPublish.Rejected reason -> box {| kind = "Rejected"; reason = reason |}
                | StrengthPreparedPublish.StorageInvalid reason -> box {| kind = "StorageInvalid"; reason = reason |}
        }

    let durabilityLoadProjection (durability: obj) : Task<obj> =
        task {
            let! result = (durabilityOf durability).LoadProjection()
            return
                match result with
                | Ok projection -> box {| ok = true; value = (ProjectionHandle projection :> obj) |}
                | Error error -> box {| ok = false; error = error |}
        }

    let durabilityLoadBundleForDecision (durability: obj) (projection: obj) (decision: string) : Task<obj> =
        task {
            match StrengthProjection.tryCandidate (StrengthDecisionId.create decision) (projectionOf projection) with
            | None -> return box {| ok = false; error = "missing candidate" |}
            | Some view ->
                let! result = (durabilityOf durability).LoadFrameBundle view.Prepared
                return
                    match result with
                    | Ok bundle -> box {| ok = true; value = bundleToJs bundle |}
                    | Error error -> box {| ok = false; error = error |}
        }

    let durabilityAppend (durability: obj) (event: obj) : Task<obj> =
        task {
            let! result = (durabilityOf durability).Append(eventOf event)
            return
                match result with
                | StrengthDurableAppend.Applied -> box {| ok = true |}
                | StrengthDurableAppend.SemanticRejected reason -> box {| ok = false; error = reason |}
                | StrengthDurableAppend.StorageFailed reason -> box {| ok = false; error = reason |}
        }

    let traceExpectedParts (bundle: obj) : obj array =
        StrengthTraceRecovery.expectedParts (bundleOf bundle)
        |> List.map (fun (kind, toolName, body) ->
            box {| kind = kind; toolName = toolName |> Option.toObj; body = body |})
        |> List.toArray

    let traceRecoverRange (bundle: obj) (observed: obj array) : obj =
        let parts: StrengthTraceObservedPart list =
            observed
            |> Array.toList
            |> List.map (fun value ->
                ({ CursorSequence = int64 (int (textOf value?cursorSequence))
                   Kind = textOf value?kind
                   ToolName = optionalText value?toolName
                   Body = textOf value?body }: StrengthTraceObservedPart))

        match StrengthTraceRecovery.recoverRange (bundleOf bundle) parts with
        | Ok None -> box {| ok = true; value = (null: obj) |}
        | Ok(Some range) ->
            box {| ok = true; value = box {| startInclusive = range.StartInclusive; endExclusive = range.EndExclusive |} |}
        | Error error -> box {| ok = false; error = error |}

    let turnEvidenceClassify (parts: obj array) =
        let evidence = StrengthTurnEvidence.classifyParts (parts |> Array.map partOf)
        match evidence with
        | StrengthProviderOutputEvidence.RealOutput -> "RealOutput"
        | StrengthProviderOutputEvidence.TransportOnly -> "TransportOnly"
        | StrengthProviderOutputEvidence.NoOutput -> "NoOutput"

    let private turnOutcomeOf (value: obj) =
        match textOf value with
        | "completed" -> ReconcileProgram.TurnCompleted
        | "needs-continuation" -> ReconcileProgram.TurnNeedsContinuation "needs-continuation"
        | "aborted" -> ReconcileProgram.TurnAborted "aborted"
        | "failed" -> ReconcileProgram.TurnFailed "failed"
        | _ -> ReconcileProgram.TurnInProgress

    let private reconciledTurnOf (value: obj) : ReconciledTurn =
        { SessionId = SessionId.create (textOf value?sessionId)
          PhysicalUserMessageId = PhysicalUserMessageId.create (textOf value?physicalUserMessageId)
          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create (textOf value?authorityRootUserMessageId)
          ProviderRun = ProviderRunIdentity.create (textOf value?providerRun)
          Role = None
          Directory = None
          Parts = arrayOf value?parts |> Array.map partOf
          Finish = None
          ErrorName = None
          Model = None
          Outcome = turnOutcomeOf value?outcome
          Observation = None }

    let lifecycleReconcileEvent (projection: obj) (turn: obj) : obj =
        match StrengthLifecycle.reconcileEvent (projectionOf projection) (reconciledTurnOf turn) with
        | Some event -> eventView (EventHandle event :> obj)
        | None -> null

    let lifecycleReconcileHandle (projection: obj) (turn: obj) : obj =
        match StrengthLifecycle.reconcileEvent (projectionOf projection) (reconciledTurnOf turn) with
        | Some event ->
            box
                {| event = (EventHandle event :> obj)
                   view = eventView (EventHandle event :> obj) |}
        | None -> box {| event = null; view = null |}

    let private planToJs (plan: StrengthReplayPlan) : obj =
        box
            {| prepared = preparedToJs plan.Prepared
               bundle = bundleToJs plan.Bundle
               beforeMessageIndex = plan.BeforeMessageIndex
               existingTraceRange =
                plan.ExistingTraceRange
                |> Option.map (fun range -> box {| startInclusive = range.StartInclusive; endExclusive = range.EndExclusive |})
                |> Option.toObj |}

    let private planOf (value: obj) : StrengthReplayPlan =
        let prepared =
            { OwnerSessionId = SessionId.create (textOf value?prepared?ownerSessionId)
              DecisionId = StrengthDecisionId.create (textOf value?prepared?decisionId)
              TargetProviderRun = ProviderRunIdentity.create (textOf value?prepared?targetProviderRun)
              ReplicaSessionId = SessionId.create (textOf value?prepared?replicaSessionId)
              Budget = budgetOf value?prepared?budget
              AnchorDigest = textOf value?prepared?anchorDigest
              FrameDigest = textOf value?prepared?frameDigest
              ByteLength = int value?prepared?byteLength
              MaterialPayloads = arrayOf value?prepared?materialPayloads |> Array.toList |> List.map (string >> PayloadRef.create) }

        let traceRange =
            if isNullish value?existingTraceRange then None
            else
                Some
                    { StartInclusive = int64 (int (textOf value?existingTraceRange?startInclusive))
                      EndExclusive = int64 (int (textOf value?existingTraceRange?endExclusive)) }

        { Prepared = prepared
          Bundle = bundleOf value?bundle
          BeforeMessageIndex = int value?beforeMessageIndex
          ExistingTraceRange = traceRange }

    let lifecycleReplayPlans (owner: string) (messages: obj array) (bundle: obj) (projection: obj) : Task<obj> =
        let messageIds = messages |> Array.toList |> List.map (fun value -> box (textOf value?id))
        let load _ = Task.FromResult(Ok(bundleOf bundle))
        task {
            let! result =
                StrengthLifecycle.replayPlans
                    (SessionId.create owner)
                    (fun message -> Some(string message))
                    messageIds
                    load
                    (projectionOf projection)

            return
                match result with
                | Ok plans -> box {| ok = true; value = plans |> List.map planToJs |> List.toArray |}
                | Error error -> box {| ok = false; error = error |}
        }

    let lifecycleNeedsRawReplay (coveredThrough: obj) (plan: obj) : bool =
        let covered = if isNullish coveredThrough then None else Some(int64 (int (textOf coveredThrough)))
        StrengthLifecycle.needsRawReplay covered (planOf plan)

    let lifecycleReplayIntents (plans: obj array) : obj array =
        plans
        |> Array.map (fun value ->
            let plan = planOf value
            box
                {| kind = "strength-promoted"
                   ownerSessionId = SessionId.value plan.Prepared.OwnerSessionId
                   decisionId = StrengthDecisionId.value plan.Prepared.DecisionId
                   targetProviderRun = ProviderRunIdentity.value plan.Prepared.TargetProviderRun
                   beforeIndex = plan.BeforeMessageIndex
                   isReplicaRequest = false
                   bundle = bundleToJs plan.Bundle |})

    let predictorCreate () : obj = PredictorHandle StrengthPredictor.empty :> obj

    let private predictorOf value = unbox<PredictorHandle> value

    let predictorFeature (role: string) (recent: string array) (visibleBytes: int) : obj =
        let symbols =
            recent
            |> Array.toList
            |> List.map (function
                | "ReadonlyBatch" -> StrengthPrimarySymbol.ReadonlyBatch
                | "MutatingOrExecuting" -> StrengthPrimarySymbol.MutatingOrExecuting
                | "TextOnly" -> StrengthPrimarySymbol.TextOnly
                | _ -> StrengthPrimarySymbol.Other)

        let feature = StrengthPredictor.feature (roleOf (box role)) symbols visibleBytes
        box
            {| canonicalRole = roleLabel feature.CanonicalRole
               recentPrimary =
                feature.RecentPrimary
                |> List.map (function
                    | StrengthPrimarySymbol.ReadonlyBatch -> "ReadonlyBatch"
                    | StrengthPrimarySymbol.MutatingOrExecuting -> "MutatingOrExecuting"
                    | StrengthPrimarySymbol.TextOnly -> "TextOnly"
                    | StrengthPrimarySymbol.Other -> "Other")
                |> List.toArray
               visibleByteBucket = feature.VisibleByteBucket |}

    let private featureOf (value: obj) : StrengthFeatureKey =
        { CanonicalRole = roleOf value?canonicalRole
          RecentPrimary =
            arrayOf value?recentPrimary
            |> Array.toList
            |> List.map (function
                | value when textOf value = "ReadonlyBatch" -> StrengthPrimarySymbol.ReadonlyBatch
                | value when textOf value = "MutatingOrExecuting" -> StrengthPrimarySymbol.MutatingOrExecuting
                | value when textOf value = "TextOnly" -> StrengthPrimarySymbol.TextOnly
                | _ -> StrengthPrimarySymbol.Other)
          VisibleByteBucket = int value?visibleByteBucket }

    let private symbolOf value =
        match textOf value with
        | "ReadonlyBatch" -> StrengthPrimarySymbol.ReadonlyBatch
        | "MutatingOrExecuting" -> StrengthPrimarySymbol.MutatingOrExecuting
        | "TextOnly" -> StrengthPrimarySymbol.TextOnly
        | _ -> StrengthPrimarySymbol.Other

    let predictorObserveFirst (state: obj) (feature: obj) (symbol: string) : bool =
        let handle = predictorOf state
        let next, readonly = StrengthPredictor.observeFirst (featureOf feature) (symbolOf (box symbol)) handle.State
        handle.State <- next
        readonly

    let predictorObserveSecond (state: obj) (feature: obj) (symbol: string) : unit =
        let handle = predictorOf state
        handle.State <- StrengthPredictor.observeSecond (featureOf feature) (symbolOf (box symbol)) handle.State

    let predictorBucket (state: obj) (feature: obj) : obj =
        let bucket = StrengthPredictor.bucket (featureOf feature) (predictorOf state).State
        box
            {| opportunities = bucket.Opportunities
               readonlyFirst = bucket.ReadonlyFirst
               secondObservations = bucket.SecondObservations
               readonlySecond = bucket.ReadonlySecond |}

    let predictorPredict (state: obj) (feature: obj) : obj =
        let prediction = StrengthPredictor.predict (featureOf feature) (predictorOf state).State
        box {| P1 = prediction.P1; P2 = prediction.P2; evidenceCount = prediction.EvidenceCount |}

    let rolloutEstimate (prediction: obj) (costs: obj) : obj =
        let value =
            StrengthRollout.estimate
                (predictionOf prediction)
                { SavedDeep1 = float costs?SavedDeep1
                  SavedDeep2 = float costs?SavedDeep2
                  Fast1 = float costs?Fast1
                  Fast2 = float costs?Fast2
                  Byte1 = float costs?Byte1
                  Byte2 = float costs?Byte2
                  Delay1 = float costs?Delay1
                  Delay2 = float costs?Delay2
                  Risk1 = float costs?Risk1
                  Risk2 = float costs?Risk2 }
        box {| V0 = value.V0; V1 = value.V1; V2 = value.V2 |}

    let rolloutIsShadow (mode: string) =
        match mode with
        | "Shadow" -> true
        | _ -> false

    let settingsLoad () : obj =
        let settings = StrengthSettings.load ()
        box
            {| mode =
                match settings.Mode with
                | StrengthRolloutMode.Off -> "Off"
                | StrengthRolloutMode.Shadow -> "Shadow"
                | StrengthRolloutMode.DryRun -> "DryRun"
                | StrengthRolloutMode.Treatment -> "Treatment"
               policy =
                {| K1Margin = settings.Policy.K1Margin
                   K2Margin = settings.Policy.K2Margin
                   K2MinimumEvidence = settings.Policy.K2MinimumEvidence |}
               costs =
                settings.Costs
                |> Option.map (fun costs ->
                    box
                        {| SavedDeep1 = costs.SavedDeep1
                           SavedDeep2 = costs.SavedDeep2
                           Fast1 = costs.Fast1
                           Fast2 = costs.Fast2
                           Byte1 = costs.Byte1
                           Byte2 = costs.Byte2
                           Delay1 = costs.Delay1
                           Delay2 = costs.Delay2
                           Risk1 = costs.Risk1
                           Risk2 = costs.Risk2 |})
                |> Option.toObj
               controlRateBasisPoints = settings.ControlRateBasisPoints |}

    let settingsDryRunBudget () = StrengthBudget.wire (StrengthSettings.dryRunBudget ())
    let settingsHostCanaryHealthy () = StrengthSettings.hostCanaryHealthy ()
    let settingsHostCanaryFingerprint = StrengthSettings.HostCanaryFingerprint

    type private ScopeHandle(scope: PluginStrengthScope) =
        member _.Value = scope

    let scopeCreate () : obj = ScopeHandle(PluginStrengthScope()) :> obj
    let private scopeOf value = unbox<ScopeHandle> value |> fun handle -> handle.Value

    let scopeFuseReason (scope: obj) =
        match (scopeOf scope).StrengthFuseReason with
        | Some reason -> reason
        | None -> null

    let scopeTripFuse (scope: obj) (reason: string) = (scopeOf scope).TripStrengthFuse reason
    let scopeClearSession (scope: obj) (session: string) = (scopeOf scope).ClearSession session
    let scopeDispose (scope: obj) = (scopeOf scope).Dispose()

    let private bindingOf (value: obj) : StrengthReplicaBinding =
        let role = roleOf value?canonicalRole
        let requestKind = ProviderRequestKind.StrengthReplica
        { OwnerSessionId = SessionId.create (textOf value?ownerSessionId)
          ReplicaSessionId = SessionId.create (textOf value?replicaSessionId)
          DecisionId = StrengthDecisionId.create (textOf value?decisionId)
          TargetProviderRun = ProviderRunIdentity.create (textOf value?targetProviderRun)
          CanonicalRole = role
          Budget = budgetOf value?budget
          MaxFrameBytes = int value?maxFrameBytes
          SemanticDigest = textOf value?semanticDigest
          LocalizedMirrorMessages = messagesOf value?localizedMirrorMessages
          ToolCapabilitySet = PromptAuthority.toolCapabilitiesFor role requestKind }

    let runtimeCreate () : obj = RuntimeHandle(StrengthRuntime()) :> obj
    let private runtimeOf value = unbox<RuntimeHandle> value |> fun handle -> handle.Value

    let runtimeBinding
        (owner: string)
        (replica: string)
        (decision: string)
        (target: string)
        (role: string)
        (budget: string)
        (maxFrameBytes: int)
        (semanticDigest: string)
        (localizedMirrorMessages: obj array)
        : obj =
        box
            {| ownerSessionId = owner
               replicaSessionId = replica
               decisionId = decision
               targetProviderRun = target
               canonicalRole = role
               budget = budget
               maxFrameBytes = maxFrameBytes
               semanticDigest = semanticDigest
               localizedMirrorMessages = localizedMirrorMessages |}

    let runtimeRegister (runtime: obj) (binding: obj) : obj =
        match (runtimeOf runtime).Register(bindingOf binding) with
        | Ok() -> box {| ok = true |}
        | Error error ->
            let name =
                match error with
                | StrengthRuntimeRegisterError.OwnerAlreadyHasReplica _ -> "OwnerAlreadyHasReplica"
                | StrengthRuntimeRegisterError.ReplicaAlreadyBound _ -> "ReplicaAlreadyBound"
                | StrengthRuntimeRegisterError.RoleIneligible _ -> "RoleIneligible"
                | StrengthRuntimeRegisterError.EmptyBudget -> "EmptyBudget"
            box {| ok = false; error = name |}

    let private bindingToJs (binding: StrengthReplicaBinding) =
        box
            {| ownerSessionId = SessionId.value binding.OwnerSessionId
               replicaSessionId = SessionId.value binding.ReplicaSessionId
               decisionId = StrengthDecisionId.value binding.DecisionId
               targetProviderRun = ProviderRunIdentity.value binding.TargetProviderRun
               canonicalRole = roleLabel binding.CanonicalRole
               budget = StrengthBudget.wire binding.Budget
               maxFrameBytes = binding.MaxFrameBytes
               semanticDigest = binding.SemanticDigest
               localizedMirrorMessages = binding.LocalizedMirrorMessages |> List.map wireMessageToJs |> List.toArray |}

    let runtimeFindByReplica (runtime: obj) (replica: string) =
        match (runtimeOf runtime).TryFindByReplica(SessionId.create replica) with
        | Some binding -> bindingToJs binding
        | None -> null

    let runtimeRetire (runtime: obj) (replica: string) =
        match (runtimeOf runtime).Retire(SessionId.create replica) with
        | Some binding -> bindingToJs binding
        | None -> null

    let private emptySessionPort (aborted: ResizeArray<string>) : ISessionHostPort =
        { new ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with member _.Dispose() = () }
            member _.SendPrompt(_, _, _) = Task.FromResult(Outcome.Retryable "unused")
            member _.AbortSession(sessionId) =
                aborted.Add(SessionId.value sessionId)
                Task.FromResult(Ok())
            member _.InterruptSessionOnly(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.CompletedTask
            member _.CreateSiblingSession(_, _, _) = Task.FromResult(Error "unused")
            member _.TryGetParentSession(_) = Task.FromResult(Ok None)
            member _.CreateChildSession(_, _) = Task.FromResult(Error "unused")
            member _.ListChildren(_) = Task.FromResult(Ok [])
            member _.FamilyRootOf(sessionId) = sessionId }

    let private transformOutcomeToJs outcome output aborted =
        let abortedIds = aborted |> Seq.toArray
        match outcome with
        | StrengthReplicaTransformOutcome.NotReplica ->
            box {| kind = "NotReplica"; batches = [||]; output = output; aborted = abortedIds |}
        | StrengthReplicaTransformOutcome.Ready values ->
            box
                {| kind = "Ready"
                   batches = values |> List.map batchToJs |> List.toArray
                   output = output
                   aborted = abortedIds |}
        | StrengthReplicaTransformOutcome.Retired(reason, values) ->
            box
                {| kind = "Retired"
                   reason = reason
                   batches = values |> List.map batchToJs |> List.toArray
                   output = output
                   aborted = abortedIds |}

    let transformApply (sha256: string -> string) (runtime: obj) (output: obj) : Task<obj> =
        let aborted = ResizeArray<string>()
        task {
            let! outcome = StrengthReplicaTransform.apply sha256 (runtimeOf runtime) (emptySessionPort aborted) output
            let messages = if isNullish output?messages then [||] else unbox<obj array> output?messages
            return transformOutcomeToJs outcome (box messages) aborted
        }
