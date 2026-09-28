namespace Wanxiangshu.Strength.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// DELEGATE-7: Host boundary wiring for explicit read-only delegation.
///
/// Two phases share one owner transform call and pass evidence through local
/// values only — never a cross-callback cache. Capture freezes the authorization
/// from the real completed owner batch and persists DelegationRequested before
/// any compaction or message replacement can lose batch metadata. Start reads
/// the pending request back from canonical Current, freezes target and mirror,
/// and runs the replica through its prepared stages with DelegationBound
/// persisted in between. All policy math stays in Domain (Policy/Delegation);
/// this adapter only freezes Host evidence, invokes the decision-local Replica,
/// publishes Prepared, and applies the insertion intent after publication.
[<RequireQualifiedAccess>]
module StrengthDelegate =

    /// WHAT-014: stable code-level contract version of the delegation field set.
    /// It never changes with environment, configuration or rollout state.
    let private contractRevision = DelegationContractRevisions.create 1

    let private failClosed (strengthScope: PluginStrengthScope) (reason: string) : 'a =
        strengthScope.TripStrengthFuse reason
        raise (InvalidOperationException reason)

    let private isRootWorkEntry (entry: SessionAssociation) : bool =
        entry.ParentSessionId.IsNone
        && match SessionOwnershipClassification.classifyLegacy entry with
           | SessionExecutionClass.Work, Some SessionOwnership.Root -> true
           | _ -> false

    let private rootWork (sessionId: SessionId) (associations: Map<SessionId, SessionAssociation>) : bool =
        SessionAssociationProjection.tryFind sessionId associations
        |> Option.exists isRootWorkEntry

    let private renderCandidate
        (owner: SessionId)
        (target: ProviderRunIdentity)
        (decision: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        (output: obj)
        : Result<unit, string> =
        result {
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output
            let wire = ProviderWireCapture.decodeMessageView rawMessages

            let! intent =
                StrengthProjectionIntent.candidate HostDigest.sha256Hex owner decision target target bundle
                |> Result.mapError (fun error -> sprintf "Strength Candidate intent refused: %A" error)

            let snapshot = { CurrentProjection = ProviderProjection.toSemantic wire }

            let rendered =
                ProjectionRenderer.renderMessagesWithHostIds snapshot wire.Messages [ intent ]

            let! projected =
                ProjectionMessageEdit.tryApplyRenderedInsertionsPreservingBase
                    (SessionId.value owner)
                    HostDigest.sha256Hex
                    rawMessages
                    rendered

            HostMessageProjection.replaceMessagesInPlace output projected
            return ()
        }

    let private wireAnchorDigest (rawMessages: obj list) =
        ProviderWireCapture.decodeMessageView rawMessages
        |> ProviderProjection.toSemantic
        |> ProviderProjection.renderSemantic
        |> HostDigest.sha256Hex

    type private BoundPorts =
        { Snapshots: ISessionSnapshotPort
          Durable: AgentJournal
          Runtime: StrengthReplicaRuntime
          Durability: StrengthDurabilityPort }

    type private OwnerSurface =
        { Owner: SessionId
          Target: ProviderRunIdentity
          Authority: PromptAuthority.AuthorityExecutionProfile
          Projections: ProjectionSet
          RawMessages: obj list
          Output: obj
          Ports: BoundPorts
          Wire: ProviderProjection.ProviderWireProjection
          AnchorDigest: string
          SourcePhysicalUserMessageId: PhysicalUserMessageId
          RequestKind: ProviderRequestKind
          HasPrefixProbe: bool
          IsRootWork: bool
          DurableProjection: StrengthProjection }

    let private planEvidence
        (tryAttemptPlan: SessionId -> ProviderRunIdentity -> AttemptPlan option)
        (owner: SessionId)
        (target: ProviderRunIdentity)
        =
        match tryAttemptPlan owner target with
        | Some plan ->
            let hasPrefixProbe =
                match plan.Profile.ProjectionChoice with
                | Wanxiangshu.Context.Prefix.XProjectionChoice.UsePrefixProbe _ -> true
                | Wanxiangshu.Context.Prefix.XProjectionChoice.UseCommittedEpoch -> false

            plan.Profile.RequestKind, hasPrefixProbe
        | None -> ProviderRequestKind.WorkMain, false

    let private tryBind
        (journal: AgentJournal option)
        (snapshotPort: ISessionSnapshotPort option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthScope: PluginStrengthScope)
        (output: obj)
        : Result<BoundPorts * SessionId, string> =
        let candidates =
            journal,
            snapshotPort,
            strengthScope.StrengthReplicaRuntime,
            strengthDurability,
            ProviderWireDecode.projectionSessionIdFromMessages output

        match candidates with
        | Some durable, Some snapshots, Some runtime, Some durability, Some sessionIdText ->
            Ok(
                { Snapshots = snapshots
                  Durable = durable
                  Runtime = runtime
                  Durability = durability },
                SessionId.create sessionIdText
            )
        | _ -> Error "Strength ports are not fully bound"

    let private resolveSurface
        (bound: BoundPorts * SessionId)
        (strengthScope: PluginStrengthScope)
        (tryAttemptPlan: SessionId -> ProviderRunIdentity -> AttemptPlan option)
        (syncDelegateRuntime: SyncDelegateRuntime option)
        (durableStrength: StrengthProjection)
        (output: obj)
        : Task<Result<OwnerSurface, string>> =
        taskResult {
            let ports, owner = bound
            let rawMessages = ProviderWireDecode.messagesFromTransformOutput output

            match ProviderWireCapture.lastUserMessageId rawMessages with
            | None -> return! Error "owner transform has no physical user message"
            | Some physical ->
                let! messages = ports.Snapshots.GetMessages owner

                match ProviderRunBinding.bindableRun (PhysicalUserMessageId.value physical) messages with
                | Error _ -> return! Error "owner provider run is not uniquely bound"
                | Ok assistant ->
                    let target = ProviderRunIdentity.create assistant.Id
                    let projections = AgentJournal.snapshot ports.Durable

                    match PromptAuthorityProjectionQueries.activeProfile owner projections.AgentProjections with
                    | None -> return! Error "owner has no active authority profile"
                    | Some authority ->
                        let requestKind, hasPrefixProbe = planEvidence tryAttemptPlan owner target

                        let attached =
                            syncDelegateRuntime
                            |> Option.bind (fun sd -> sd.TryFindDelegateOwner owner)
                            |> Option.isSome

                        let wire = ProviderWireCapture.decodeMessageView rawMessages

                        return
                            { Owner = owner
                              Target = target
                              Authority = authority
                              Projections = projections
                              RawMessages = rawMessages
                              Output = output
                              Ports = ports
                              Wire = wire
                              AnchorDigest = wireAnchorDigest rawMessages
                              SourcePhysicalUserMessageId = physical
                              RequestKind = requestKind
                              HasPrefixProbe = hasPrefixProbe
                              IsRootWork =
                                authority.AuthorityKind = PromptAuthority.RootAuthorityKind.HumanRoot
                                && rootWork owner projections.AgentProjections.Associations
                                && not attached
                              DurableProjection = durableStrength }
        }

    // ---- source batch evidence -------------------------------------------------

    /// Resolves the completed source batch of the tail assistant message.
    /// Supports both:
    /// 1. Host session-shaped tool parts (single assistant message where all tool parts are completed with output)
    /// 2. Wire-level multi-message parts (assistant WireToolCall messages followed by WireToolResult messages)
    let private resolveCompletedSourceBatch
        (rawMessages: obj list)
        (wire: ProviderProjection.ProviderWireProjection)
        : (ToolCallId * string) list option =
        let fromRaw =
            rawMessages
            |> List.choose (fun raw ->
                let info = ProviderWireDecode.infoObject raw

                let role =
                    ProviderWireDecode.firstString info [ "role" ]
                    |> Option.orElse (ProviderWireDecode.firstString raw [ "role" ])
                    |> Option.defaultValue ""

                if String.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) then
                    Some raw
                else
                    None)
            |> List.tryLast
            |> Option.bind (fun assistant ->
                let parts = ProviderWireDecode.rawPartsOf assistant

                let toolParts =
                    parts
                    |> List.filter (fun part ->
                        let kind =
                            ProviderWireDecode.firstString part [ "type" ]
                            |> Option.defaultValue ""
                            |> fun s -> s.ToLowerInvariant()

                        kind = "tool" || kind = "tool-call" || kind = "tool_call")

                if List.isEmpty toolParts then
                    None
                else
                    let isCompleted (part: obj) =
                        let state = ProviderWireDecode.readField part "state"

                        if isNull state then
                            false
                        else
                            let status = if isNull state?status then "" else string state?status

                            if status = "completed" then
                                let hasInput =
                                    not (isNull state?input)
                                    || not (isNull part?args)
                                    || not (isNull part?arguments)

                                let hasOutput =
                                    not (isNull state?output)
                                    || not (isNull state?result)
                                    || not (isNull state?content)

                                hasInput && hasOutput
                            else
                                false

                    if List.forall isCompleted toolParts then
                        let calls =
                            toolParts
                            |> List.choose (fun part ->
                                let state = ProviderWireDecode.readField part "state"

                                let callId =
                                    ProviderWireDecode.firstString part [ "callID"; "callId"; "id" ]
                                    |> Option.map ToolCallId.create

                                let args =
                                    if not (isNull state) && not (isNull state?input) then
                                        Some(emitJsExpr state?input "JSON.stringify($0)")
                                    elif not (isNull part?args) then
                                        Some(emitJsExpr part?args "JSON.stringify($0)")
                                    elif not (isNull part?arguments) then
                                        Some(emitJsExpr part?arguments "JSON.stringify($0)")
                                    else
                                        None

                                match callId, args with
                                | Some cid, Some a -> Some(cid, a)
                                | _ -> None)

                        if calls.Length = toolParts.Length then
                            let ids = calls |> List.map fst

                            if Set.count (Set.ofList ids) = ids.Length then
                                Some calls
                            else
                                None
                        else
                            None
                    else
                        None)

        match fromRaw with
        | Some calls -> Some calls
        | None ->
            let assistantBatches =
                wire.Messages
                |> List.choose (fun message ->
                    if String.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) then
                        let calls =
                            message.Parts
                            |> List.choose (function
                                | ProviderProjection.WireToolCall(callId, _, arguments) -> Some(callId, arguments)
                                | _ -> None)

                        if List.isEmpty calls then None else Some calls
                    else
                        None)

            match assistantBatches with
            | [] -> None
            | batches ->
                let tailCalls = List.last batches

                match List.tryLast (StrengthBatchCollector.collectCompleteBatches wire.Messages) with
                | None -> None
                | Some batch ->
                    let batchArgs =
                        batch.Exchanges |> List.map (fun exchange -> exchange.CanonicalArguments)

                    let callArgs = tailCalls |> List.map snd
                    if batchArgs = callArgs then Some tailCalls else None

    /// DELEGATE-002: the batch budget is one integer per call, validated exactly
    /// as written. Missing, null, string, boolean, fractional, negative or
    /// out-of-range values are a call-argument error: the batch never starts and
    /// the value is never silently normalized to zero.
    let private budgetOfCall (callId: ToolCallId) (arguments: string) : Result<int, string> =
        let parsed =
            try
                Ok(emitJsExpr arguments "JSON.parse($0)")
            with _ ->
                Error(sprintf "delegation arguments of call %s are not valid JSON" (ToolCallId.value callId))

        match parsed with
        | Error reason -> Error reason
        | Ok value ->
            let raw = value?delegate_readonly_rounds

            if isNull raw then
                Error(sprintf "delegate_readonly_rounds is absent for call %s" (ToolCallId.value callId))
            else
                let asNumber =
                    try
                        Ok(unbox<float> raw)
                    with _ ->
                        Error(sprintf "delegate_readonly_rounds of call %s is not a number" (ToolCallId.value callId))

                match asNumber with
                | Error reason -> Error reason
                | Ok number when
                    Double.IsNaN number
                    || Double.IsInfinity number
                    || number < 0.0
                    || number <> floor number
                    ->
                    Error(
                        sprintf
                            "delegate_readonly_rounds of call %s is not a non-negative integer"
                            (ToolCallId.value callId)
                    )
                | Ok number when number > float Int32.MaxValue ->
                    Error(sprintf "delegate_readonly_rounds of call %s is out of range" (ToolCallId.value callId))
                | Ok number -> Ok(int number)

    let private batchBudgetOfCalls (calls: (ToolCallId * string) list) : Result<ReadonlyRoundBudget, string> =
        let rec loop remaining acc =
            match remaining with
            | [] -> Ok(List.rev acc)
            | Ok value :: tail -> loop tail (value :: acc)
            | Error reason :: _ -> Error reason

        match loop (calls |> List.map (fun (callId, arguments) -> budgetOfCall callId arguments)) [] with
        | Error reason -> Error reason
        | Ok values -> values |> List.max |> ReadonlyRoundBudget.tryCreate

    // ---- phase one: capture the authorization ----------------------------------

    type CaptureOutcome =
        | Captured of DelegationRequest
        | Skipped of reason: string

    let private captureOnSurface
        (strengthScope: PluginStrengthScope)
        (predictorConfigured: bool)
        (surface: OwnerSurface)
        : Task<CaptureOutcome> =
        task {
            // DELEGATE-2: authorization forms only for a legal ordinary
            // WorkMain continuation of a real fresh owner response; a
            // Replica or internal leaf run can never be a source.
            if surface.RequestKind <> ProviderRequestKind.WorkMain then
                return CaptureOutcome.Skipped "not-work-main"
            elif surface.Ports.Runtime.IsReplica surface.Owner || not surface.IsRootWork then
                return CaptureOutcome.Skipped "not-root-owner-work"
            else
                match resolveCompletedSourceBatch surface.RawMessages surface.Wire with
                | None -> return CaptureOutcome.Skipped "no-completed-source-batch"
                | Some calls ->
                    match batchBudgetOfCalls calls with
                    | Error reason -> return CaptureOutcome.Skipped reason
                    | Ok budget ->
                        let ownerLogicalRun =
                            { LogicalRunId = surface.Authority.LogicalRunId
                              AuthorityRootUserMessageId = surface.Authority.AuthorityRootUserMessageId }

                        let decisionId =
                            Delegation.deriveDecisionId
                                HostDigest.sha256Hex
                                contractRevision
                                ownerLogicalRun
                                surface.Target

                        let sourceToolCallIds = calls |> List.map fst

                        let request =
                            { DecisionId = decisionId
                              OwnerSessionId = surface.Owner
                              OwnerLogicalRun = ownerLogicalRun
                              SourcePhysicalUserMessageId = surface.SourcePhysicalUserMessageId
                              SourceProviderRun = surface.Target
                              SourceToolCallIds = sourceToolCallIds
                              RequestedRounds = budget
                              ContractRevision = contractRevision }

                        // Same-source idempotence and conflict against canonical
                        // Current: re-recording the identical request is a no-op;
                        // any change to N, the call set or the authority conflicts.
                        match StrengthProjection.tryCandidate decisionId surface.DurableProjection with
                        | Some existing when Delegation.sameRequest existing.Request request ->
                            return CaptureOutcome.Captured existing.Request
                        | Some _ -> return CaptureOutcome.Skipped "delegation-request-conflict"
                        | None ->
                            let opportunity =
                                { OwnerSessionId = surface.Owner
                                  OwnerLogicalRun = ownerLogicalRun
                                  SourcePhysicalUserMessageId = surface.SourcePhysicalUserMessageId
                                  SourceProviderRun = surface.Target
                                  SourceToolCallIds = sourceToolCallIds
                                  RequestedRounds = Some budget
                                  ContractRevision = contractRevision
                                  IsRootWork = surface.IsRootWork
                                  RequestKind = surface.RequestKind
                                  CanonicalRole = surface.Authority.CanonicalRole
                                  HasPrefixProbe = surface.HasPrefixProbe
                                  IsReplicaOrInternalLeaf = not surface.IsRootWork
                                  IsInteractionRepair = surface.RequestKind = ProviderRequestKind.InteractionRepair
                                  IsExplicitRecoveryBranch = false
                                  OwnerCancelled = false
                                  TargetProviderRunBound = true
                                  EventStoreHealthy = true
                                  HostBoundaryHealthy = true
                                  ProcessFuseHealthy = true
                                  OwnerLogicalRunSuperseded = false
                                  PendingRequested = true
                                  PredictorConfigured = predictorConfigured }

                            match StrengthPolicy.tryRequest HostDigest.sha256Hex opportunity with
                            | Error reason -> return CaptureOutcome.Skipped reason
                            | Ok admitted ->
                                match!
                                    surface.Ports.Durability.Append(
                                        StrengthEvents.requested
                                            admitted.DecisionId
                                            admitted.OwnerSessionId
                                            admitted.OwnerLogicalRun
                                            admitted.SourcePhysicalUserMessageId
                                            admitted.SourceProviderRun
                                            admitted.SourceToolCallIds
                                            admitted.RequestedRounds
                                            admitted.ContractRevision
                                    )
                                with
                                | StrengthDurableAppend.Applied -> return CaptureOutcome.Captured admitted
                                | StrengthDurableAppend.SemanticRejected reason ->
                                    return failClosed strengthScope ("Strength DelegationRequested rejected: " + reason)
                                | StrengthDurableAppend.StorageInvalid reason ->
                                    return
                                        failClosed
                                            strengthScope
                                            ("Strength DelegationRequested storage invalid: " + reason)
                                | StrengthDurableAppend.StorageFailed reason ->
                                    return
                                        failClosed
                                            strengthScope
                                            ("Strength DelegationRequested append failed: " + reason)
        }

    let tryCapture
        (snapshotPort: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthScope: PluginStrengthScope)
        (tryAttemptPlan: SessionId -> ProviderRunIdentity -> AttemptPlan option)
        (syncDelegateRuntime: SyncDelegateRuntime option)
        (predictorConfigured: bool)
        (output: obj)
        : Task<CaptureOutcome> =
        task {
            match tryBind journal snapshotPort strengthDurability strengthScope output with
            | Error reason -> return CaptureOutcome.Skipped reason
            | Ok bound ->
                if strengthScope.StrengthFuseReason |> Option.isSome then
                    return CaptureOutcome.Skipped "strength-fuse-tripped"
                else
                    let ports, _ = bound

                    match! ports.Durability.LoadProjection() with
                    | Error reason ->
                        return failClosed strengthScope ("Strength capture cannot prove EventStore health: " + reason)
                    | Ok durableStrength ->
                        match!
                            resolveSurface bound strengthScope tryAttemptPlan syncDelegateRuntime durableStrength output
                        with
                        | Error reason -> return CaptureOutcome.Skipped reason
                        | Ok resolved -> return! captureOnSurface strengthScope predictorConfigured resolved
        }

    // ---- phase two: start / consume --------------------------------------------

    let private appendClosed
        (strengthScope: PluginStrengthScope)
        (surface: OwnerSurface)
        (decisionId: StrengthDecisionId)
        (closedFrom: DelegationClosedFrom)
        (closedReason: DelegationClosedReason)
        : Task<unit> =
        task {
            match! surface.Ports.Durability.Append(StrengthEvents.closed decisionId closedFrom closedReason) with
            | StrengthDurableAppend.Applied -> return ()
            | StrengthDurableAppend.SemanticRejected reason ->
                return failClosed strengthScope ("Strength DelegationClosed rejected: " + reason)
            | StrengthDurableAppend.StorageInvalid reason ->
                return failClosed strengthScope ("Strength DelegationClosed storage invalid: " + reason)
            | StrengthDurableAppend.StorageFailed reason ->
                return failClosed strengthScope ("Strength DelegationClosed append failed: " + reason)
        }

    let private publishAndRender
        (strengthScope: PluginStrengthScope)
        (surface: OwnerSurface)
        (decisionId: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        (replicaSessionId: SessionId)
        : Task<unit> =
        task {
            let! published =
                surface.Ports.Durability.PublishPrepared
                    { OwnerSessionId = surface.Owner
                      DecisionId = decisionId
                      TargetProviderRun = surface.Target
                      ReplicaSessionId = replicaSessionId
                      AnchorDigest = surface.AnchorDigest
                      Bundle = bundle }

            match published with
            | StrengthPreparedPublish.StorageInvalid error ->
                return failClosed strengthScope ("Strength Prepared storage invalid: " + error)
            | StrengthPreparedPublish.Rejected _ -> return ()
            | StrengthPreparedPublish.Published ->
                match renderCandidate surface.Owner surface.Target decisionId bundle surface.Output with
                | Ok() -> return ()
                | Error error -> return failClosed strengthScope ("Strength Candidate render failed closed: " + error)
        }

    /// DELEGATE-7/10: the target run already owns a durable decision. A Prepared
    /// candidate re-renders the exact same material without re-running the
    /// readonly tools; a Bound-but-empty execution whose local child is gone
    /// loses this investigation opportunity and closes; settled states wait.
    let private consumeBoundDecision
        (strengthScope: PluginStrengthScope)
        (surface: OwnerSurface)
        (view: StrengthDelegationView)
        : Task<unit> =
        task {
            match view.State with
            | StrengthCandidateState.Prepared ->
                match view.Prepared with
                | Some prepared ->
                    if wireAnchorDigest surface.RawMessages <> prepared.AnchorDigest then
                        return
                            failClosed
                                strengthScope
                                "Strength Prepared recovery anchor digest changed before target consumption"
                    else
                        match! surface.Ports.Durability.LoadFrameBundle prepared with
                        | Error error ->
                            return failClosed strengthScope ("Strength Prepared frame load failed: " + error)
                        | Ok bundle ->
                            match
                                renderCandidate surface.Owner surface.Target prepared.DecisionId bundle surface.Output
                            with
                            | Ok() -> return ()
                            | Error error ->
                                return failClosed strengthScope ("Strength Candidate render failed closed: " + error)
                | None -> return ()
            | StrengthCandidateState.Bound ->
                match view.Binding with
                | Some binding ->
                    if surface.Ports.Runtime.IsReplica binding.ReplicaSessionId then
                        return ()
                    else
                        do!
                            appendClosed
                                strengthScope
                                surface
                                binding.DecisionId
                                DelegationClosedFrom.Bound
                                DelegationClosedReason.RecoveryAbandoned

                        return ()
                | None -> return ()
            | StrengthCandidateState.Promoted
            | StrengthCandidateState.Traced
            | StrengthCandidateState.Closed _
            | StrengthCandidateState.Abandoned
            | StrengthCandidateState.Requested -> return ()
        }

    let private startRequest
        (strengthScope: PluginStrengthScope)
        (predictorConfigured: bool)
        (surface: OwnerSurface)
        (request: DelegationRequest)
        : Task<unit> =
        task {
            if
                request.OwnerLogicalRun.AuthorityRootUserMessageId
                <> surface.Authority.AuthorityRootUserMessageId
            then
                do!
                    appendClosed
                        strengthScope
                        surface
                        request.DecisionId
                        DelegationClosedFrom.Requested
                        DelegationClosedReason.Superseded

                return ()
            elif strengthScope.StrengthFuseReason |> Option.isSome then
                return! failClosed strengthScope "Strength fuse is tripped; delegation is closed for this process"
            elif not predictorConfigured then
                do!
                    appendClosed
                        strengthScope
                        surface
                        request.DecisionId
                        DelegationClosedFrom.Requested
                        DelegationClosedReason.CannotContinue

                return ()
            elif not (Set.contains surface.Authority.CanonicalRole StrengthPolicy.eligibleRoles) then
                do!
                    appendClosed
                        strengthScope
                        surface
                        request.DecisionId
                        DelegationClosedFrom.Requested
                        DelegationClosedReason.CannotContinue

                return ()
            else
                let replicaAgent = Roles.roleLabel surface.Authority.CanonicalRole

                match
                    StrengthFrame.tryLocalizeMirror
                        HostDigest.sha256Hex
                        request.DecisionId
                        surface.AnchorDigest
                        surface.Wire.Messages
                with
                | Error _ -> return ()
                | Ok replicaMirror ->
                    match!
                        surface.Ports.Runtime.PrepareReplicaStart(
                            surface.Owner,
                            request.DecisionId,
                            surface.Target,
                            request.RequestedRounds,
                            replicaAgent,
                            replicaMirror,
                            surface.AnchorDigest
                        )
                    with
                    | Error _ ->
                        do!
                            appendClosed
                                strengthScope
                                surface
                                request.DecisionId
                                DelegationClosedFrom.Requested
                                DelegationClosedReason.CannotContinue

                        return ()
                    | Ok preparation ->
                        // DELEGATE-6.2: the empty child exists and carries internal
                        // identity, but no prompt was sent and no model capacity was
                        // reserved. Persist DelegationBound before any send; on a
                        // write failure clean the empty child through CancelOwner.
                        match!
                            surface.Ports.Durability.Append(
                                StrengthEvents.bound
                                    request.DecisionId
                                    surface.Target
                                    preparation.ReplicaSessionId
                                    surface.AnchorDigest
                            )
                        with
                        | StrengthDurableAppend.SemanticRejected reason ->
                            do! surface.Ports.Runtime.CancelOwner surface.Owner
                            return! failClosed strengthScope ("Strength DelegationBound rejected: " + reason)
                        | StrengthDurableAppend.StorageInvalid reason ->
                            do! surface.Ports.Runtime.CancelOwner surface.Owner
                            return! failClosed strengthScope ("Strength DelegationBound storage invalid: " + reason)
                        | StrengthDurableAppend.StorageFailed reason ->
                            do! surface.Ports.Runtime.CancelOwner surface.Owner
                            return! failClosed strengthScope ("Strength DelegationBound append failed: " + reason)
                        | StrengthDurableAppend.Applied ->
                            match! surface.Ports.Runtime.SendPreparedPrompt preparation.ReplicaSessionId with
                            | Error _ ->
                                do!
                                    appendClosed
                                        strengthScope
                                        surface
                                        request.DecisionId
                                        DelegationClosedFrom.Bound
                                        DelegationClosedReason.CannotContinue

                                return ()
                            | Ok() ->
                                let! completed = preparation.Completion

                                match completed.Terminal with
                                | StrengthReplicaTerminal.InvalidFrame reason ->
                                    return! failClosed strengthScope ("Strength Replica invalid frame: " + reason)
                                | _ when List.isEmpty completed.Batches ->
                                    do!
                                        appendClosed
                                            strengthScope
                                            surface
                                            request.DecisionId
                                            DelegationClosedFrom.Bound
                                            DelegationClosedReason.NoMaterial

                                    return ()
                                | _ ->
                                    match StrengthFrame.tryBuild HostDigest.sha256Hex completed.Batches with
                                    | Error error ->
                                        return!
                                            failClosed
                                                strengthScope
                                                (sprintf "Strength Replica bundle invalid: %A" error)
                                    | Ok bundle ->
                                        return!
                                            publishAndRender
                                                strengthScope
                                                surface
                                                request.DecisionId
                                                bundle
                                                completed.ReplicaSessionId
        }

    /// DELEGATE-10: recovery reads the pending request from persisted facts. A
    /// new user input or authority replacement closes the old request; the
    /// request never rescans arbitrary history for a positive budget.
    let private startPendingRequest
        (strengthScope: PluginStrengthScope)
        (predictorConfigured: bool)
        (surface: OwnerSurface)
        : Task<unit> =
        task {
            let pending =
                surface.DurableProjection.ByDecision
                |> Map.toList
                |> List.map snd
                |> List.filter (fun view ->
                    view.State = StrengthCandidateState.Requested
                    && view.Request.OwnerSessionId = surface.Owner
                    && view.Request.OwnerLogicalRun.LogicalRunId = surface.Authority.LogicalRunId)
                |> List.sortBy (fun view -> StrengthDecisionId.value view.Request.DecisionId)

            match pending with
            | [] -> return ()
            | view :: _ -> return! startRequest strengthScope predictorConfigured surface view.Request
        }

    let tryApply
        (snapshotPort: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthScope: PluginStrengthScope)
        (tryAttemptPlan: SessionId -> ProviderRunIdentity -> AttemptPlan option)
        (syncDelegateRuntime: SyncDelegateRuntime option)
        (predictorConfigured: bool)
        (output: obj)
        : Task<unit> =
        task {
            match tryBind journal snapshotPort strengthDurability strengthScope output with
            | Error _ -> return ()
            | Ok bound ->
                let ports, _ = bound

                match! ports.Durability.LoadProjection() with
                | Error reason ->
                    return failClosed strengthScope ("Strength start cannot prove EventStore health: " + reason)
                | Ok durableStrength ->
                    match!
                        resolveSurface bound strengthScope tryAttemptPlan syncDelegateRuntime durableStrength output
                    with
                    | Error _ -> return ()
                    | Ok surface ->
                        if surface.Ports.Runtime.IsReplica surface.Owner || not surface.IsRootWork then
                            return ()
                        else
                            match StrengthProjection.tryDecisionForTarget surface.Target surface.DurableProjection with
                            | Some decisionId ->
                                match StrengthProjection.tryCandidate decisionId surface.DurableProjection with
                                | Some view -> return! consumeBoundDecision strengthScope surface view
                                | None -> return ()
                            | None -> return! startPendingRequest strengthScope predictorConfigured surface
        }
