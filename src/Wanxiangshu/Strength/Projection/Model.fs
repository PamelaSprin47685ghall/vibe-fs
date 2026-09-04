namespace Wanxiangshu.Strength.Projection

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation.Identity

type StrengthTraceRange =
    { StartInclusive: int64
      EndExclusive: int64 }

type StrengthCandidateView =
    { Prepared: StrengthCandidatePrepared
      Promoted: bool
      TraceRange: StrengthTraceRange option
      Abandoned: bool }

type StrengthProjection =
    { ByDecision: Map<string, StrengthCandidateView>
      ByTargetRun: Map<string, StrengthDecisionId> }

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

/// DSL-class: Decision — Strength candidate fold refusals (Prepared/Promoted/Trace/Abandon).
[<RequireQualifiedAccess>]
type StrengthProjectionError =
    | PreparedConflict of decisionId: StrengthDecisionId
    | TargetAlreadyBound of targetProviderRun: ProviderRunIdentity
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

module StrengthProjection =

    let empty =
        { ByDecision = Map.empty
          ByTargetRun = Map.empty }

    let private decisionKey decisionId = StrengthDecisionId.value decisionId
    let private targetKey providerRun = ProviderRunIdentity.value providerRun

    let tryCandidate (decisionId: StrengthDecisionId) (projection: StrengthProjection) =
        Map.tryFind (decisionKey decisionId) projection.ByDecision

    let hasPrepared decisionId projection =
        Option.isSome (tryCandidate decisionId projection)

    let isPromoted decisionId projection =
        tryCandidate decisionId projection |> Option.exists (fun view -> view.Promoted)

    let tryDecisionForTarget (targetProviderRun: ProviderRunIdentity) (projection: StrengthProjection) =
        Map.tryFind (targetKey targetProviderRun) projection.ByTargetRun

    let tryTraceRange decisionId projection =
        tryCandidate decisionId projection |> Option.bind (fun view -> view.TraceRange)

    let private samePromotion (prepared: StrengthCandidatePrepared) (promoted: StrengthCandidatePromoted) =
        prepared.OwnerSessionId = promoted.OwnerSessionId
        && prepared.DecisionId = promoted.DecisionId
        && prepared.TargetProviderRun = promoted.TargetProviderRun
        && prepared.FrameDigest = promoted.FrameDigest
        && prepared.MaterialPayloads = promoted.MaterialPayloads

    let private registerPrepared (projection: StrengthProjection) (prepared: StrengthCandidatePrepared) =
        let tkey = targetKey prepared.TargetProviderRun

        match Map.tryFind tkey projection.ByTargetRun with
        | Some existingDecision when existingDecision <> prepared.DecisionId ->
            Error(StrengthProjectionError.TargetAlreadyBound prepared.TargetProviderRun)
        | _ ->
            let view =
                { Prepared = prepared
                  Promoted = false
                  TraceRange = None
                  Abandoned = false }

            Ok
                { ByDecision = Map.add (decisionKey prepared.DecisionId) view projection.ByDecision
                  ByTargetRun = Map.add tkey prepared.DecisionId projection.ByTargetRun }

    let private applyPrepared (projection: StrengthProjection) (prepared: StrengthCandidatePrepared) =
        let dkey = decisionKey prepared.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | Some existing when existing.Prepared = prepared -> Ok projection
        | Some _ -> Error(StrengthProjectionError.PreparedConflict prepared.DecisionId)
        | None -> registerPrepared projection prepared

    let private applyPromoted (projection: StrengthProjection) (promoted: StrengthCandidatePromoted) =
        let dkey = decisionKey promoted.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.PromotionWithoutPrepared promoted.DecisionId)
        | Some view when view.Abandoned -> Error(StrengthProjectionError.PromotionAfterAbandon promoted.DecisionId)
        | Some view when not (samePromotion view.Prepared promoted) ->
            Error(StrengthProjectionError.PromotionMismatch promoted.DecisionId)
        | Some view when view.Promoted -> Ok projection
        | Some view ->
            Ok
                { projection with
                    ByDecision = Map.add dkey { view with Promoted = true } projection.ByDecision }

    let private attachTraceRange
        (projection: StrengthProjection)
        (decisionId: StrengthDecisionId)
        (range: StrengthTraceRange)
        (view: StrengthCandidateView)
        : Result<StrengthProjection, StrengthProjectionError> =
        match view.TraceRange with
        | Some existing when existing = range -> Ok projection
        | Some _ -> Error(StrengthProjectionError.TraceConflict decisionId)
        | None ->
            Ok
                { projection with
                    ByDecision =
                        Map.add (decisionKey decisionId) { view with TraceRange = Some range } projection.ByDecision }

    let private applyTraced (projection: StrengthProjection) (traced: StrengthFramesTraced) =
        let dkey = decisionKey traced.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.TraceWithoutPrepared traced.DecisionId)
        | Some view when not view.Promoted -> Error(StrengthProjectionError.TraceWithoutPromotion traced.DecisionId)
        | Some _ when traced.StartInclusive < 0L || traced.EndExclusive <= traced.StartInclusive ->
            Error(StrengthProjectionError.InvalidTraceRange traced.DecisionId)
        | Some view ->
            let range =
                { StartInclusive = traced.StartInclusive
                  EndExclusive = traced.EndExclusive }

            attachTraceRange projection traced.DecisionId range view

    let private applyAbandoned (projection: StrengthProjection) (abandoned: StrengthCandidateAbandoned) =
        let dkey = decisionKey abandoned.DecisionId

        match Map.tryFind dkey projection.ByDecision with
        | None -> Error(StrengthProjectionError.AbandonWithoutPrepared abandoned.DecisionId)
        | Some view when view.Promoted -> Error(StrengthProjectionError.AbandonAfterPromotion abandoned.DecisionId)
        | Some view when view.Prepared.TargetProviderRun <> abandoned.TargetProviderRun ->
            Error(StrengthProjectionError.AbandonMismatch abandoned.DecisionId)
        | Some view when view.Abandoned -> Ok projection
        | Some view ->
            Ok
                { ByDecision = Map.add dkey { view with Abandoned = true } projection.ByDecision
                  ByTargetRun = Map.remove (targetKey abandoned.TargetProviderRun) projection.ByTargetRun }

    let apply
        (projection: StrengthProjection)
        (event: StrengthEvent)
        : Result<StrengthProjection, StrengthProjectionError> =
        match event with
        | StrengthEvent.Prepared prepared -> applyPrepared projection prepared
        | StrengthEvent.Promoted promoted -> applyPromoted projection promoted
        | StrengthEvent.Traced traced -> applyTraced projection traced
        | StrengthEvent.Abandoned abandoned -> applyAbandoned projection abandoned

// No history-fold API by design. CanonicalIntegrator is the sole history
// enumerator and registers `apply` as this module's one-event oracle.
