namespace Wanxiangshu.Strength.Projection

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength

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

[<RequireQualifiedAccess>]
type StrengthProjectionIntentError =
    | CandidateWrongTarget of decisionId: StrengthDecisionId
    | PromotedReplicaReflection of decisionId: StrengthDecisionId
    | FrameDigestMismatch of decisionId: StrengthDecisionId
    | InvalidAnchor of decisionId: StrengthDecisionId

[<RequireQualifiedAccess>]
module StrengthProjectionIntent =
    val projectionMirror:
        decisionId: StrengthDecisionId ->
        localizedRows: ProjectionMessageRow list ->
            Result<ProjectionIntent, StrengthProjectionIntentError>

    val candidate:
        sha256: (string -> string) ->
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        targetProviderRun: ProviderRunIdentity ->
        currentProviderRun: ProviderRunIdentity ->
        bundle: StrengthFrameBundle ->
            Result<ProjectionIntent, StrengthProjectionIntentError>

    val promoted:
        sha256: (string -> string) ->
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        beforeMessageIndex: int ->
        isReplicaRequest: bool ->
        bundle: StrengthFrameBundle ->
            Result<ProjectionIntent, StrengthProjectionIntentError>

    val replicaLocal:
        sha256: (string -> string) ->
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        bundle: StrengthFrameBundle ->
            Result<ProjectionIntent, StrengthProjectionIntentError>

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
    val empty: StrengthProjection
    val tryCandidate: decisionId: StrengthDecisionId -> projection: StrengthProjection -> StrengthCandidateView option
    val hasPrepared: decisionId: StrengthDecisionId -> projection: StrengthProjection -> bool
    val isPromoted: decisionId: StrengthDecisionId -> projection: StrengthProjection -> bool

    val tryDecisionForTarget:
        targetProviderRun: ProviderRunIdentity -> projection: StrengthProjection -> StrengthDecisionId option

    val tryTraceRange: decisionId: StrengthDecisionId -> projection: StrengthProjection -> StrengthTraceRange option

    val apply:
        projection: StrengthProjection -> event: StrengthEvent -> Result<StrengthProjection, StrengthProjectionError>
