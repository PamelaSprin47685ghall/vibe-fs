namespace Wanxiangshu.Strength.Projection

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength

type StrengthTraceRange =
    { StartInclusive: int64
      EndExclusive: int64 }

[<RequireQualifiedAccess>]
type StrengthCandidateState =
    | Requested
    | Bound
    | Prepared
    | Promoted
    | Traced
    | Closed of DelegationClosed
    | Abandoned

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

[<RequireQualifiedAccess>]
type StrengthProjectionIntentError =
    | CandidateWrongTarget of decisionId: StrengthDecisionId
    | PromotedReplicaReflection of decisionId: StrengthDecisionId
    | FrameDigestMismatch of decisionId: StrengthDecisionId
    | InvalidAnchor of decisionId: StrengthDecisionId

[<RequireQualifiedAccess>]
module StrengthProjectionIntent =
    /// The owner's final request for this decision, used as the replica's message base.
    val projectionMirror:
        localizedRows: ProjectionMessageRow list -> Result<ProjectionIntent, StrengthProjectionIntentError>


    val candidate:
        sha256: (string -> string) ->
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        targetProviderRun: ProviderRunIdentity ->
        currentProviderRun: ProviderRunIdentity ->
        displayName: (string -> string) ->
        bundle: StrengthFrameBundle ->
            Result<ProjectionIntent, StrengthProjectionIntentError>

    val promoted:
        sha256: (string -> string) ->
        ownerSessionId: SessionId ->
        decisionId: StrengthDecisionId ->
        beforeMessageIndex: int ->
        isReplicaRequest: bool ->
        displayName: (string -> string) ->
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
    val empty: StrengthProjection
    val tryCandidate: decisionId: StrengthDecisionId -> projection: StrengthProjection -> StrengthDelegationView option

    val tryCandidateBySource:
        ownerSessionId: SessionId ->
        ownerLogicalRun: OwnerLogicalRunIdentity ->
        sourcePhysicalUserMessageId: PhysicalUserMessageId ->
        sourceProviderRun: ProviderRunIdentity ->
        projection: StrengthProjection ->
            StrengthDelegationView option

    val hasPrepared: decisionId: StrengthDecisionId -> projection: StrengthProjection -> bool
    val isPromoted: decisionId: StrengthDecisionId -> projection: StrengthProjection -> bool

    val tryDecisionForTarget:
        targetProviderRun: ProviderRunIdentity -> projection: StrengthProjection -> StrengthDecisionId option

    val tryTraceRange: decisionId: StrengthDecisionId -> projection: StrengthProjection -> StrengthTraceRange option

    val requestedRounds: decisionId: StrengthDecisionId -> projection: StrengthProjection -> ReadonlyRoundBudget option

    /// Evidence-only imported history. It never yields a runnable delegation.
    val tryImported: importId: string -> projection: StrengthProjection -> DelegationHistoryImported option

    val apply:
        projection: StrengthProjection -> event: StrengthEvent -> Result<StrengthProjection, StrengthProjectionError>
