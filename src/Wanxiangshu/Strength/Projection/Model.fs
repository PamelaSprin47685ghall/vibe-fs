namespace Wanxiangshu.Strength.Projection

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength

type StrengthTraceRange =
    { StartInclusive: int64
      EndExclusive: int64 }

/// DELEGATE-6.2: one delegation lifecycle expressed as a closed union, never as
/// boolean combinations. Legal edges are enforced by the fold below.
[<RequireQualifiedAccess>]
type StrengthCandidateState =
    | Requested
    | Bound
    | Prepared
    | Promoted
    | Traced
    | Closed of DelegationClosed
    | Abandoned

/// Folded view of one DecisionId: the immutable request plus the facts legally
/// attached so far. Prepared is the candidate that a Bound request produced.
type StrengthDelegationView =
    { Request: DelegationRequest
      Binding: DelegationBinding option
      Prepared: StrengthCandidatePrepared option
      State: StrengthCandidateState
      TraceRange: StrengthTraceRange option }

type StrengthProjection =
    { ByDecision: Map<string, StrengthDelegationView>
      ByTargetRun: Map<string, StrengthDecisionId>
      ImportedHistory: Map<string, DelegationHistoryImported> }

/// Typed refusal taxonomy for Strength-owned projection decisions.
[<RequireQualifiedAccess>]
type StrengthProjectionIntentError =
    | CandidateWrongTarget of decisionId: StrengthDecisionId
    | PromotedReplicaReflection of decisionId: StrengthDecisionId
    | FrameDigestMismatch of decisionId: StrengthDecisionId
    | InvalidAnchor of decisionId: StrengthDecisionId

/// Strength policy and frame expansion. The provider projection receives only
/// generic message-base and message-row intents produced here.
[<RequireQualifiedAccess>]
module StrengthProjectionIntent =

    let private key (decisionId: StrengthDecisionId) = StrengthDecisionId.value decisionId

    let projectionMirror
        (decisionId: StrengthDecisionId)
        (localizedRows: ProjectionMessageRow list)
        : Result<ProjectionIntent, StrengthProjectionIntentError> =
        Ok(ProjectionIntent.replaceMessageBase (key decisionId) localizedRows)

    let private digestMatches (sha256: string -> string) (bundle: StrengthFrameBundle) =
        sha256 (StrengthFrame.canonicalText bundle.Batches) = bundle.Digest

    let private frameRows
        (sha256: string -> string)
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        : ProjectionMessageRow list =
        bundle.Batches
        |> List.collect (fun batch ->
            let exchanges =
                batch.Exchanges
                |> List.mapi (fun index exchange ->
                    let callId =
                        StrengthFrame.wireToolCallId
                            sha256
                            ownerSessionId
                            decisionId
                            batch.RequestOrdinal
                            (index + 1)
                            bundle.Digest
                        |> ToolCallId.create

                    callId, exchange)

            let calls =
                exchanges
                |> List.map (fun (callId, exchange) ->
                    ProviderProjection.WireToolCall(callId, exchange.ToolName, exchange.CanonicalArguments))

            let results =
                exchanges
                |> List.map (fun (callId, exchange) ->
                    ProviderProjection.WireToolResult(callId, exchange.CanonicalResult))

            [ { Message = { Role = "assistant"; Parts = calls }
                HostMessageId =
                  Some(
                      StrengthFrame.hostMessageId
                          sha256
                          ownerSessionId
                          decisionId
                          batch.RequestOrdinal
                          "call"
                          bundle.Digest
                  )
                HostIsPhysical = false }
              { Message = { Role = "tool"; Parts = results }
                HostMessageId =
                  Some(
                      StrengthFrame.hostMessageId
                          sha256
                          ownerSessionId
                          decisionId
                          batch.RequestOrdinal
                          "result"
                          bundle.Digest
                  )
                HostIsPhysical = false } ])

    let private insertion
        (sha256: string -> string)
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (anchor: ProjectionMessageAnchor)
        (bundle: StrengthFrameBundle)
        : Result<ProjectionIntent, StrengthProjectionIntentError> =
        if not (digestMatches sha256 bundle) then
            Error(StrengthProjectionIntentError.FrameDigestMismatch decisionId)
        else
            frameRows sha256 ownerSessionId decisionId bundle
            |> ProjectionIntent.insertMessageRows (key decisionId) anchor
            |> Ok

    let candidate
        (sha256: string -> string)
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (targetProviderRun: ProviderRunIdentity)
        (currentProviderRun: ProviderRunIdentity)
        (bundle: StrengthFrameBundle)
        : Result<ProjectionIntent, StrengthProjectionIntentError> =
        if targetProviderRun <> currentProviderRun then
            Error(StrengthProjectionIntentError.CandidateWrongTarget decisionId)
        else
            insertion sha256 ownerSessionId decisionId ProjectionMessageAnchor.Append bundle

    let promoted
        (sha256: string -> string)
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (beforeMessageIndex: int)
        (isReplicaRequest: bool)
        (bundle: StrengthFrameBundle)
        : Result<ProjectionIntent, StrengthProjectionIntentError> =
        if isReplicaRequest then
            Error(StrengthProjectionIntentError.PromotedReplicaReflection decisionId)
        elif beforeMessageIndex < 0 then
            Error(StrengthProjectionIntentError.InvalidAnchor decisionId)
        else
            insertion
                sha256
                ownerSessionId
                decisionId
                (ProjectionMessageAnchor.BeforeMessageIndex beforeMessageIndex)
                bundle

    let replicaLocal
        (sha256: string -> string)
        (ownerSessionId: SessionId)
        (decisionId: StrengthDecisionId)
        (bundle: StrengthFrameBundle)
        : Result<ProjectionIntent, StrengthProjectionIntentError> =
        insertion sha256 ownerSessionId decisionId ProjectionMessageAnchor.Append bundle

/// DSL-class: Decision — Strength delegation fold refusals.
[<RequireQualifiedAccess>]
type StrengthProjectionError =
    | RequestedConflict of decisionId: StrengthDecisionId
    | BoundWithoutRequested of decisionId: StrengthDecisionId
    | BoundConflict of decisionId: StrengthDecisionId
    | TargetAlreadyBound of targetProviderRun: ProviderRunIdentity
    | ClosedWithoutRequested of decisionId: StrengthDecisionId
    | ClosedConflict of decisionId: StrengthDecisionId
    | PreparedWithoutBound of decisionId: StrengthDecisionId
    | PreparedConflict of decisionId: StrengthDecisionId
    | PreparedBindingMismatch of decisionId: StrengthDecisionId
    | PromotionWithoutPrepared of decisionId: StrengthDecisionId
    | PromotionMismatch of decisionId: StrengthDecisionId
    | PromotionAfterAbandon of decisionId: StrengthDecisionId
    | TraceWithoutPrepared of decisionId: StrengthDecisionId
    | TraceWithoutPromotion of decisionId: StrengthDecisionId
    | InvalidTraceRange of decisionId: StrengthDecisionId
    | TraceConflict of decisionId: StrengthDecisionId
    | AbandonWithoutPrepared of decisionId: StrengthDecisionId
    | AbandonMismatch of decisionId: StrengthDecisionId
    | AbandonAfterPromotion of decisionId: StrengthDecisionId
    | ImportConflict of importId: string

module StrengthProjection =

    let empty =
        { ByDecision = Map.empty
          ByTargetRun = Map.empty
          ImportedHistory = Map.empty }

    let private decisionKey decisionId = StrengthDecisionId.value decisionId
    let private targetKey providerRun = ProviderRunIdentity.value providerRun

    let tryCandidate (decisionId: StrengthDecisionId) (projection: StrengthProjection) =
        Map.tryFind (decisionKey decisionId) projection.ByDecision

    let hasPrepared decisionId projection =
        tryCandidate decisionId projection
        |> Option.exists (fun view -> Option.isSome view.Prepared)

    let isPromoted decisionId projection =
        tryCandidate decisionId projection
        |> Option.exists (fun view ->
            view.State = StrengthCandidateState.Promoted
            || view.State = StrengthCandidateState.Traced)

    let tryDecisionForTarget (targetProviderRun: ProviderRunIdentity) (projection: StrengthProjection) =
        Map.tryFind (targetKey targetProviderRun) projection.ByTargetRun

    let tryTraceRange decisionId projection =
        tryCandidate decisionId projection |> Option.bind (fun view -> view.TraceRange)

    /// DELEGATE-6.3: the binding reads the requested rounds from the immutable
    /// projection; no layer keeps its own mutable copy of the budget.
    let requestedRounds decisionId projection =
        tryCandidate decisionId projection
        |> Option.filter (fun view ->
            match view.State with
            // A superseded authorization was replaced by a successor, so it no
            // longer offers a budget. Every other close keeps the requested
            // fact readable — the Closed state itself already blocks any spend
            // at the fold — and an abandoned candidate offers nothing.
            | StrengthCandidateState.Closed closed -> closed.Reason <> DelegationClosedReason.Superseded
            | StrengthCandidateState.Abandoned -> false
            | StrengthCandidateState.Requested
            | StrengthCandidateState.Bound
            | StrengthCandidateState.Prepared
            | StrengthCandidateState.Promoted
            | StrengthCandidateState.Traced -> true)
        |> Option.map (fun view -> view.Request.RequestedRounds)

    let tryImported (importId: string) (projection: StrengthProjection) =
        Map.tryFind importId projection.ImportedHistory

    let private samePromotion (prepared: StrengthCandidatePrepared) (promoted: StrengthCandidatePromoted) =
        prepared.OwnerSessionId = promoted.OwnerSessionId
        && prepared.DecisionId = promoted.DecisionId
        && prepared.TargetProviderRun = promoted.TargetProviderRun
        && prepared.FrameDigest = promoted.FrameDigest
        && prepared.MaterialPayloads = promoted.MaterialPayloads

    let private resolveRequestedConflict projection requested (existing: StrengthDelegationView) =
        if Delegation.sameRequest existing.Request requested then
            Ok projection
        else
            Error(StrengthProjectionError.RequestedConflict requested.DecisionId)

    let private applyRequested projection (requested: DelegationRequest) =
        let dkey = decisionKey requested.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | Some existing -> resolveRequestedConflict projection requested existing
        | None ->
            let view =
                { Request = requested
                  Binding = None
                  Prepared = None
                  State = StrengthCandidateState.Requested
                  TraceRange = None }

            Ok
                { projection with
                    ByDecision = Map.add dkey view projection.ByDecision }

    let private bindExistingView projection dkey tkey (bound: DelegationBinding) (existing: StrengthDelegationView) =
        match existing.State with
        | StrengthCandidateState.Bound when
            existing.Binding
            |> Option.exists (fun current -> Delegation.sameBinding current bound)
            ->
            Ok projection
        | StrengthCandidateState.Bound -> Error(StrengthProjectionError.BoundConflict bound.DecisionId)
        | StrengthCandidateState.Requested ->
            Ok
                { projection with
                    ByDecision =
                        Map.add
                            dkey
                            { existing with
                                Binding = Some bound
                                State = StrengthCandidateState.Bound }
                            projection.ByDecision
                    ByTargetRun = Map.add tkey bound.DecisionId projection.ByTargetRun }
        | _ -> Error(StrengthProjectionError.BoundConflict bound.DecisionId)

    let private resolveBoundView projection dkey tkey (bound: DelegationBinding) =
        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.BoundWithoutRequested bound.DecisionId)
        | Some existing -> bindExistingView projection dkey tkey bound existing

    let private applyBound projection (bound: DelegationBinding) =
        let dkey = decisionKey bound.DecisionId
        let tkey = targetKey bound.TargetProviderRun

        match Map.tryFind tkey projection.ByTargetRun with
        | Some other when other <> bound.DecisionId ->
            Error(StrengthProjectionError.TargetAlreadyBound bound.TargetProviderRun)
        | _ -> resolveBoundView projection dkey tkey bound

    let private closeView
        projection
        dkey
        (existing: StrengthDelegationView)
        (closed: DelegationClosed)
        (releasedTarget: string option)
        =
        let byTargetRun =
            match releasedTarget with
            | Some tkey -> Map.remove tkey projection.ByTargetRun
            | None -> projection.ByTargetRun

        Ok
            { projection with
                ByDecision =
                    Map.add
                        dkey
                        { existing with
                            State = StrengthCandidateState.Closed closed }
                        projection.ByDecision
                ByTargetRun = byTargetRun }

    let private resolveClosedState projection dkey (existing: StrengthDelegationView) (closed: DelegationClosed) =
        match existing.State with
        | StrengthCandidateState.Closed prior when prior = closed -> Ok projection
        | StrengthCandidateState.Closed _ -> Error(StrengthProjectionError.ClosedConflict closed.DecisionId)
        | StrengthCandidateState.Requested when closed.From = DelegationClosedFrom.Requested ->
            closeView projection dkey existing closed None
        | StrengthCandidateState.Bound when closed.From = DelegationClosedFrom.Bound ->
            let released =
                existing.Binding
                |> Option.map (fun binding -> targetKey binding.TargetProviderRun)

            closeView projection dkey existing closed released
        | _ -> Error(StrengthProjectionError.ClosedConflict closed.DecisionId)

    let private applyClosed projection (closed: DelegationClosed) =
        let dkey = decisionKey closed.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.ClosedWithoutRequested closed.DecisionId)
        | Some existing -> resolveClosedState projection dkey existing closed

    let private bindPreparedView
        projection
        dkey
        (prepared: StrengthCandidatePrepared)
        (existing: StrengthDelegationView)
        (binding: DelegationBinding)
        =
        if
            prepared.TargetProviderRun <> binding.TargetProviderRun
            || prepared.ReplicaSessionId <> binding.ReplicaSessionId
            || prepared.OwnerSessionId <> existing.Request.OwnerSessionId
        then
            Error(StrengthProjectionError.PreparedBindingMismatch prepared.DecisionId)
        else
            Ok
                { projection with
                    ByDecision =
                        Map.add
                            dkey
                            { existing with
                                Prepared = Some prepared
                                State = StrengthCandidateState.Prepared }
                            projection.ByDecision }

    let private prepareBoundView
        projection
        dkey
        (prepared: StrengthCandidatePrepared)
        (existing: StrengthDelegationView)
        =
        match existing.State, existing.Binding with
        | StrengthCandidateState.Bound, Some binding ->
            bindPreparedView projection dkey prepared existing binding
        | _ -> Error(StrengthProjectionError.PreparedWithoutBound prepared.DecisionId)

    let private resolvePreparedState
        projection
        dkey
        (prepared: StrengthCandidatePrepared)
        (existing: StrengthDelegationView)
        =
        match existing.Prepared with
        | Some prior when prior = prepared -> Ok projection
        | Some _ -> Error(StrengthProjectionError.PreparedConflict prepared.DecisionId)
        | None -> prepareBoundView projection dkey prepared existing

    let private applyPrepared projection (prepared: StrengthCandidatePrepared) =
        let dkey = decisionKey prepared.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.PreparedWithoutBound prepared.DecisionId)
        | Some existing -> resolvePreparedState projection dkey prepared existing

    let private resolvePromotedState
        projection
        dkey
        (promoted: StrengthCandidatePromoted)
        (existing: StrengthDelegationView)
        =
        match existing.State, existing.Prepared with
        | (StrengthCandidateState.Abandoned | StrengthCandidateState.Closed _), _ ->
            Error(StrengthProjectionError.PromotionAfterAbandon promoted.DecisionId)
        | (StrengthCandidateState.Promoted | StrengthCandidateState.Traced), Some prior when samePromotion prior promoted ->
            Ok projection
        | (StrengthCandidateState.Promoted | StrengthCandidateState.Traced), _ ->
            Error(StrengthProjectionError.PromotionMismatch promoted.DecisionId)
        | StrengthCandidateState.Prepared, Some prior when samePromotion prior promoted ->
            Ok
                { projection with
                    ByDecision =
                        Map.add
                            dkey
                            { existing with
                                State = StrengthCandidateState.Promoted }
                            projection.ByDecision }
        | StrengthCandidateState.Prepared, _ ->
            Error(StrengthProjectionError.PromotionMismatch promoted.DecisionId)
        | _ -> Error(StrengthProjectionError.PromotionWithoutPrepared promoted.DecisionId)

    let private applyPromoted projection (promoted: StrengthCandidatePromoted) =
        let dkey = decisionKey promoted.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.PromotionWithoutPrepared promoted.DecisionId)
        | Some existing -> resolvePromotedState projection dkey promoted existing

    let private resolvePromotedTrace
        projection
        dkey
        (traced: StrengthFramesTraced)
        (existing: StrengthDelegationView)
        range
        =
        if traced.StartInclusive < 0L || traced.EndExclusive <= traced.StartInclusive then
            Error(StrengthProjectionError.InvalidTraceRange traced.DecisionId)
        else
            Ok
                { projection with
                    ByDecision =
                        Map.add
                            dkey
                            { existing with
                                State = StrengthCandidateState.Traced
                                TraceRange = Some range }
                            projection.ByDecision }

    let private resolveTracedState
        projection
        dkey
        (traced: StrengthFramesTraced)
        (existing: StrengthDelegationView)
        range
        =
        match existing.State with
        | StrengthCandidateState.Traced when existing.TraceRange = Some range -> Ok projection
        | StrengthCandidateState.Traced -> Error(StrengthProjectionError.TraceConflict traced.DecisionId)
        | StrengthCandidateState.Promoted -> resolvePromotedTrace projection dkey traced existing range
        | _ -> Error(StrengthProjectionError.TraceWithoutPromotion traced.DecisionId)

    let private applyTraced projection (traced: StrengthFramesTraced) =
        let dkey = decisionKey traced.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.TraceWithoutPrepared traced.DecisionId)
        | Some existing ->
            let range =
                { StartInclusive = traced.StartInclusive
                  EndExclusive = traced.EndExclusive }

            resolveTracedState projection dkey traced existing range

    let private resolveAbandonedState
        projection
        dkey
        (abandoned: StrengthCandidateAbandoned)
        (existing: StrengthDelegationView)
        =
        match existing.State, existing.Binding with
        | StrengthCandidateState.Abandoned, Some binding when binding.TargetProviderRun = abandoned.TargetProviderRun ->
            Ok projection
        | StrengthCandidateState.Abandoned, _ ->
            Error(StrengthProjectionError.AbandonMismatch abandoned.DecisionId)
        | (StrengthCandidateState.Promoted | StrengthCandidateState.Traced), _ ->
            Error(StrengthProjectionError.AbandonAfterPromotion abandoned.DecisionId)
        | StrengthCandidateState.Prepared, Some binding when binding.TargetProviderRun = abandoned.TargetProviderRun ->
            Ok
                { projection with
                    ByDecision =
                        Map.add
                            dkey
                            { existing with
                                State = StrengthCandidateState.Abandoned }
                            projection.ByDecision
                    ByTargetRun = Map.remove (targetKey abandoned.TargetProviderRun) projection.ByTargetRun }
        | StrengthCandidateState.Prepared, _ ->
            Error(StrengthProjectionError.AbandonMismatch abandoned.DecisionId)
        | _ -> Error(StrengthProjectionError.AbandonWithoutPrepared abandoned.DecisionId)

    let private applyAbandoned projection (abandoned: StrengthCandidateAbandoned) =
        let dkey = decisionKey abandoned.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.AbandonWithoutPrepared abandoned.DecisionId)
        | Some existing -> resolveAbandonedState projection dkey abandoned existing

    /// DELEGATE: imported history is evidence only. It never enters ByDecision
    /// or ByTargetRun, so it neither advances a lifecycle nor mints an
    /// admission; re-appending the identical import is idempotent.
    let private applyImported projection (imported: DelegationHistoryImported) =
        let key = imported.ImportId

        match Map.tryFind key projection.ImportedHistory with
        | Some existing when existing = imported -> Ok projection
        | Some _ -> Error(StrengthProjectionError.ImportConflict imported.ImportId)
        | None ->
            Ok
                { projection with
                    ImportedHistory = Map.add key imported projection.ImportedHistory }

    let apply
        (projection: StrengthProjection)
        (event: StrengthEvent)
        : Result<StrengthProjection, StrengthProjectionError> =
        match event with
        | StrengthEvent.DelegationRequested requested -> applyRequested projection requested
        | StrengthEvent.DelegationBound bound -> applyBound projection bound
        | StrengthEvent.DelegationClosed closed -> applyClosed projection closed
        | StrengthEvent.Prepared prepared -> applyPrepared projection prepared
        | StrengthEvent.Promoted promoted -> applyPromoted projection promoted
        | StrengthEvent.Traced traced -> applyTraced projection traced
        | StrengthEvent.DelegationHistoryImported imported -> applyImported projection imported
        | StrengthEvent.Abandoned abandoned -> applyAbandoned projection abandoned

// No history-fold API by design. CanonicalIntegrator is the sole history
// enumerator and registers `apply` as this module's one-event oracle.
