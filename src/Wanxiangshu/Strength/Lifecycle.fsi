namespace Wanxiangshu.Strength

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection

/// STRENGTH-007/008: pure lifecycle decisions around durable Strength facts.
/// Persistence and Host message codecs are ports supplied by the composition root.
type StrengthReplayPlan =
    { Prepared: StrengthCandidatePrepared
      Bundle: StrengthFrameBundle
      BeforeMessageIndex: int
      ExistingTraceRange: StrengthTraceRange option }

[<RequireQualifiedAccess>]
module StrengthLifecycle =
    val reconcileEvent: projection: StrengthProjection -> turn: ReconciledTurn -> StrengthEvent option

    val replayPlans:
        ownerSessionId: SessionId ->
        messageIdOf: ('message -> string option) ->
        messages: 'message list ->
        loadBundle: (StrengthCandidatePrepared -> Task<Result<StrengthFrameBundle, string>>) ->
        projection: StrengthProjection ->
            Task<Result<StrengthReplayPlan list, string>>

    val needsRawReplay: coveredThroughSequence: int64 option -> plan: StrengthReplayPlan -> bool

    val replayIntents:
        sha256: (string -> string) ->
        plans: StrengthReplayPlan list ->
            Result<ProjectionIntent list, StrengthProjectionIntentError>

    val framePartCount: bundle: StrengthFrameBundle -> int
