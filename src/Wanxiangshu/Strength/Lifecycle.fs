namespace Wanxiangshu.Strength
open Wanxiangshu.Change
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Turn

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
            task {
                match remaining with
                | [] -> return Ok(List.rev acc)
                | view :: tail ->
                    let target = ProviderRunIdentity.value view.Prepared.TargetProviderRun

                    match messages |> List.tryFindIndex (fun message -> messageIdOf message = Some target) with
                    | None ->
                        return
                            Error(
                                sprintf
                                    "Promoted Strength target anchor is absent: decision=%s target=%s"
                                    (StrengthDecisionId.value view.Prepared.DecisionId)
                                    target
                            )
                    | Some beforeIndex ->
                        match! loadBundle view.Prepared with
                        | Error error -> return Error error
                        | Ok bundle when bundle.Digest <> view.Prepared.FrameDigest ->
                            return
                                Error(
                                    sprintf
                                        "Promoted Strength payload digest mismatch: decision=%s"
                                        (StrengthDecisionId.value view.Prepared.DecisionId)
                                )
                        | Ok bundle ->
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
        bundle.Batches |> List.sumBy (fun batch -> batch.Exchanges.Length * 2)
