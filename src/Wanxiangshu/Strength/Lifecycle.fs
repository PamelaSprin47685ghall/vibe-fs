namespace Wanxiangshu.Strength

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
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

    let private abandonOrWait (view: StrengthDelegationView) (turn: ReconciledTurn) =
        match view.Binding, turn.Outcome with
        | Some binding, ReconcileProgram.TurnCompleted
        | Some binding, ReconcileProgram.TurnAborted _
        | Some binding, ReconcileProgram.TurnFailed _ ->
            Some(StrengthEvents.abandoned view.Request.DecisionId binding.TargetProviderRun)
        | _ -> None

    let private promotionFromDecision
        (view: StrengthDelegationView)
        (prepared: StrengthCandidatePrepared)
        turn
        decision
        =
        match decision with
        | StrengthPromotionDecision.Promote ->
            Some(
                StrengthEvents.promoted
                    prepared.OwnerSessionId
                    prepared.DecisionId
                    prepared.TargetProviderRun
                    prepared.FrameDigest
                    prepared.MaterialPayloads
            )
        | StrengthPromotionDecision.IgnoreWrongRun -> None
        | StrengthPromotionDecision.AwaitOrAbandon -> abandonOrWait view turn

    let private promotionEvent (view: StrengthDelegationView) (turn: ReconciledTurn) =
        match view.Binding, view.Prepared with
        | Some binding, Some prepared ->
            let decision = StrengthTurnEvidence.promotionDecision binding.TargetProviderRun turn
            promotionFromDecision view prepared turn decision
        | _ -> None

    let reconcileEvent (projection: StrengthProjection) (turn: ReconciledTurn) : StrengthEvent option =
        StrengthProjection.tryDecisionForTarget turn.ProviderRun projection
        |> Option.bind (fun decisionId -> StrengthProjection.tryCandidate decisionId projection)
        |> Option.bind (fun view ->
            match view.State with
            | StrengthCandidateState.Prepared -> promotionEvent view turn
            | StrengthCandidateState.Requested
            | StrengthCandidateState.Bound
            | StrengthCandidateState.Promoted
            | StrengthCandidateState.Traced
            | StrengthCandidateState.Closed _
            | StrengthCandidateState.Abandoned -> None)

    let private completedRequestEvent
        owner
        (view: StrengthDelegationView)
        (prepared: StrengthCandidatePrepared)
        (assistant: SessionMessage)
        =
        match
            StrengthTurnEvidence.completedRequestDecision
                prepared.TargetProviderRun
                view.Request.SourcePhysicalUserMessageId
                assistant
        with
        | StrengthPromotionDecision.Promote ->
            Some(
                StrengthEvents.promoted
                    owner
                    prepared.DecisionId
                    prepared.TargetProviderRun
                    prepared.FrameDigest
                    prepared.MaterialPayloads
            )
        | _ -> None

    let reconcileCompletedRequest
        (owner: SessionId)
        (projection: StrengthProjection)
        (assistant: SessionMessage)
        : StrengthEvent option =
        StrengthProjection.tryDecisionForTarget (ProviderRunIdentity.create assistant.Id) projection
        |> Option.bind (fun decision -> StrengthProjection.tryCandidate decision projection)
        |> Option.bind (fun view ->
            match view.State, view.Prepared with
            | StrengthCandidateState.Prepared, Some prepared when prepared.OwnerSessionId = owner ->
                completedRequestEvent owner view prepared assistant
            | _ -> None)

    let private anchorMissingError (prepared: StrengthCandidatePrepared) (target: string) =
        Error(
            sprintf
                "Promoted Strength target anchor is absent: decision=%s target=%s"
                (StrengthDecisionId.value prepared.DecisionId)
                target
        )

    let private digestMismatchError (prepared: StrengthCandidatePrepared) =
        Error(
            sprintf
                "Promoted Strength payload digest mismatch: decision=%s"
                (StrengthDecisionId.value prepared.DecisionId)
        )

    let private requireDigestMatch (prepared: StrengthCandidatePrepared) (bundle: StrengthFrameBundle) =
        if bundle.Digest <> prepared.FrameDigest then
            digestMismatchError prepared
        else
            Ok()

    let private chooseReplayCandidate (ownerSessionId: SessionId) (view: StrengthDelegationView) =
        match view.Prepared, view.State with
        | Some prepared, (StrengthCandidateState.Promoted | StrengthCandidateState.Traced) when
            prepared.OwnerSessionId = ownerSessionId
            ->
            Some(prepared, view)
        | _ -> None

    let private resolveBeforeIndex prepared target authorityRoot messages messageIdOf =
        let targetIndex =
            messages |> List.tryFindIndex (fun message -> messageIdOf message = Some target)

        let rootIndex =
            messages
            |> List.tryFindIndex (fun message -> messageIdOf message = Some authorityRoot)

        match targetIndex, rootIndex with
        | Some targetAt, Some rootAt when rootAt < targetAt -> Ok targetAt
        | _ -> anchorMissingError prepared target

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
            |> List.choose (chooseReplayCandidate ownerSessionId)
            |> List.sortBy (fun (prepared, _) -> StrengthDecisionId.value prepared.DecisionId)

        let rec loop
            (remaining: (StrengthCandidatePrepared * StrengthDelegationView) list)
            (acc: StrengthReplayPlan list)
            =
            taskResult {
                match remaining with
                | [] -> return List.rev acc
                | (prepared, view) :: tail ->
                    let target = ProviderRunIdentity.value prepared.TargetProviderRun

                    let authorityRoot =
                        view.Request.OwnerLogicalRun.AuthorityRootUserMessageId
                        |> AuthorityRootUserMessageId.value

                    let! beforeIndex = resolveBeforeIndex prepared target authorityRoot messages messageIdOf

                    let! bundle = loadBundle prepared
                    do! requireDigestMatch prepared bundle

                    return!
                        loop
                            tail
                            ({ Prepared = prepared
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
        (displayName: string -> string)
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
                displayName
                plan.Bundle)

    let framePartCount (bundle: StrengthFrameBundle) =
        bundle.Batches |> List.sumBy (fun batch -> batch.Exchanges.Length * 2)
