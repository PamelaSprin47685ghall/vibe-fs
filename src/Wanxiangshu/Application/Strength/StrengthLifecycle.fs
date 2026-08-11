namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Kernel.Identity

/// STRENGTH-007/008: pure lifecycle decisions around durable Strength facts.
/// Persistence and Host message codecs are ports supplied by the composition root.
type StrengthReplayPlan =
    { Prepared: StrengthCandidatePrepared
      Bundle: StrengthFrameBundle
      BeforeMessageIndex: int
      ExistingTraceRange: StrengthTraceRange option }

[<RequireQualifiedAccess>]
module StrengthLifecycle =

    let reconcileEvent (projection: StrengthProjection) (turn: ReconciledTurn) : StrengthEvent option =
        match StrengthProjection.tryDecisionForTarget turn.ProviderRun projection with
        | None -> None
        | Some decisionId ->
            match StrengthProjection.tryCandidate decisionId projection with
            | None -> None
            | Some view when view.Promoted || view.Abandoned -> None
            | Some view ->
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
                | StrengthPromotionDecision.AwaitOrAbandon ->
                    match turn.Outcome with
                    | ReconcileProgram.TurnCompleted
                    | ReconcileProgram.TurnAborted _
                    | ReconcileProgram.TurnFailed _ ->
                        Some(StrengthEvents.abandoned view.Prepared.DecisionId view.Prepared.TargetProviderRun)
                    | ReconcileProgram.TurnNeedsContinuation _
                    | ReconcileProgram.TurnInProgress -> None

    /// Build deterministic replay plans for every unretired Promoted decision owned
    /// by this Session. The caller supplies Host message ids and payload loading;
    /// this module never guesses an anchor or reconstructs missing payload bytes.
    let replayPlans
        (ownerSessionId: SessionId)
        (messageIdOf: 'message -> string option)
        (messages: 'message list)
        (loadBundle: StrengthCandidatePrepared -> Result<StrengthFrameBundle, string>)
        (projection: StrengthProjection)
        : Result<StrengthReplayPlan list, string> =
        let candidates =
            projection.ByDecision
            |> Map.toList
            |> List.map snd
            |> List.filter (fun view ->
                view.Prepared.OwnerSessionId = ownerSessionId && view.Promoted && not view.Abandoned)
            |> List.sortBy (fun view -> StrengthDecisionId.value view.Prepared.DecisionId)

        let rec loop (remaining: StrengthCandidateView list) (acc: StrengthReplayPlan list) =
            match remaining with
            | [] -> Ok(List.rev acc)
            | view :: tail ->
                let target = ProviderRunIdentity.value view.Prepared.TargetProviderRun

                match messages |> List.tryFindIndex (fun message -> messageIdOf message = Some target) with
                | None ->
                    Error(
                        sprintf
                            "Promoted Strength target anchor is absent: decision=%s target=%s"
                            (StrengthDecisionId.value view.Prepared.DecisionId)
                            target
                    )
                | Some beforeIndex ->
                    match loadBundle view.Prepared with
                    | Error error -> Error error
                    | Ok bundle when bundle.Digest <> view.Prepared.FrameDigest ->
                        Error(
                            sprintf
                                "Promoted Strength payload digest mismatch: decision=%s"
                                (StrengthDecisionId.value view.Prepared.DecisionId)
                        )
                    | Ok bundle ->
                        loop
                            tail
                            ({ Prepared = view.Prepared
                               Bundle = bundle
                               BeforeMessageIndex = beforeIndex
                               ExistingTraceRange = view.TraceRange }
                             :: acc)

        loop candidates []

    let needsRawReplay (coveredThroughSequence: int64 option) (plan: StrengthReplayPlan) =
        match plan.ExistingTraceRange, coveredThroughSequence with
        | Some range, Some covered -> covered < range.EndExclusive - 1L
        | _ -> true

    let replayIntents (plans: StrengthReplayPlan list) : ProjectionIntent list =
        plans
        |> List.map (fun plan ->
            ProjectionIntent.strengthPromoted
                plan.Prepared.OwnerSessionId
                plan.Prepared.DecisionId
                plan.Prepared.TargetProviderRun
                plan.BeforeMessageIndex
                false
                plan.Bundle)

    let framePartCount (bundle: StrengthFrameBundle) =
        bundle.Batches
        |> List.sumBy (fun batch -> batch.Exchanges.Length * 2)

