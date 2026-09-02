namespace Wanxiangshu.Strength

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

/// STRENGTH-007/008: pure lifecycle decisions around durable Strength facts.
/// Persistence and Host message codecs are ports supplied by the composition root.
type StrengthReplayPlan =
    { Prepared: StrengthCandidatePrepared
      Bundle: StrengthFrameBundle
      BeforeMessageIndex: int
      ExistingTraceRange: StrengthTraceRange option }

[<RequireQualifiedAccess>]
module StrengthLifecycle =

    let private abandonOrWait (view: StrengthCandidateView) (turn: ReconciledTurn) =
        match turn.Outcome with
        | ReconcileProgram.TurnCompleted
        | ReconcileProgram.TurnAborted _
        | ReconcileProgram.TurnFailed _ ->
            Some(StrengthEvents.abandoned view.Prepared.DecisionId view.Prepared.TargetProviderRun)
        | ReconcileProgram.TurnNeedsContinuation _
        | ReconcileProgram.TurnInProgress -> None

    let private promotionEvent (view: StrengthCandidateView) (turn: ReconciledTurn) =
        match StrengthTurnEvidence.promotionDecision view.Prepared.TargetProviderRun turn with
        | StrengthPromotionDecision.Promote ->
            Some(
                StrengthEvents.promoted
                    view.Prepared.OwnerSessionId
                    view.Prepared.DecisionId
                    view.Prepared.TargetProviderRun
                    view.Prepared.FrameDigest
                    view.Prepared.MaterialPayloads
            )
        | StrengthPromotionDecision.IgnoreWrongRun -> None
        | StrengthPromotionDecision.AwaitOrAbandon -> abandonOrWait view turn

    let reconcileEvent (projection: StrengthProjection) (turn: ReconciledTurn) : StrengthEvent option =
        StrengthProjection.tryDecisionForTarget turn.ProviderRun projection
        |> Option.bind (fun decisionId -> StrengthProjection.tryCandidate decisionId projection)
        |> Option.bind (fun view ->
            if view.Promoted || view.Abandoned then
                None
            else
                promotionEvent view turn)

    let private anchorMissingError (view: StrengthCandidateView) (target: string) =
        Error(
            sprintf
                "Promoted Strength target anchor is absent: decision=%s target=%s"
                (StrengthDecisionId.value view.Prepared.DecisionId)
                target
        )

    let private digestMismatchError (view: StrengthCandidateView) =
        Error(
            sprintf
                "Promoted Strength payload digest mismatch: decision=%s"
                (StrengthDecisionId.value view.Prepared.DecisionId)
        )

    let private requireDigestMatch (view: StrengthCandidateView) (bundle: StrengthFrameBundle) =
        if bundle.Digest <> view.Prepared.FrameDigest then
            digestMismatchError view
        else
            Ok()

    /// Build deterministic replay plans for every unretired Promoted decision owned
    /// by this Session. The caller supplies Host message ids and payload loading;
    /// this module never guesses an anchor or reconstructs missing payload bytes.
    let replayPlans
        (ownerSessionId: SessionId)
        (messageIdOf: 'message -> string option)
        (messages: 'message list)
        (loadBundle: StrengthCandidatePrepared -> Task<Result<StrengthFrameBundle, string>>)
        (projection: StrengthProjection)
        : Task<Result<StrengthReplayPlan list, string>> =
        let candidates =
            projection.ByDecision
            |> Map.toList
            |> List.map snd
            |> List.filter (fun view ->
                view.Prepared.OwnerSessionId = ownerSessionId
                && view.Promoted
                && not view.Abandoned)
            |> List.sortBy (fun view -> StrengthDecisionId.value view.Prepared.DecisionId)

        let rec loop (remaining: StrengthCandidateView list) (acc: StrengthReplayPlan list) =
            taskResult {
                match remaining with
                | [] -> return List.rev acc
                | view :: tail ->
                    let target = ProviderRunIdentity.value view.Prepared.TargetProviderRun

                    let! beforeIndex =
                        messages
                        |> List.tryFindIndex (fun message -> messageIdOf message = Some target)
                        |> Option.map Ok
                        |> Option.defaultValue (anchorMissingError view target)

                    let! bundle = loadBundle view.Prepared
                    do! requireDigestMatch view bundle

                    return!
                        loop
                            tail
                            ({ Prepared = view.Prepared
                               Bundle = bundle
                               BeforeMessageIndex = beforeIndex
                               ExistingTraceRange = view.TraceRange }
                             :: acc)
            }

        loop candidates []

    let needsRawReplay (coveredThroughSequence: int64 option) (plan: StrengthReplayPlan) =
        match plan.ExistingTraceRange, coveredThroughSequence with
        | Some range, Some covered -> covered < range.EndExclusive - 1L
        | _ -> true

    let replayIntents
        (sha256: string -> string)
        (plans: StrengthReplayPlan list)
        : Result<ProjectionIntent list, StrengthProjectionIntentError> =
        plans
        |> List.traverseResultM (fun plan ->
            StrengthProjectionIntent.promoted
                sha256
                plan.Prepared.OwnerSessionId
                plan.Prepared.DecisionId
                plan.BeforeMessageIndex
                false
                plan.Bundle)

    let framePartCount (bundle: StrengthFrameBundle) =
        bundle.Batches |> List.sumBy (fun batch -> batch.Exchanges.Length * 2)
