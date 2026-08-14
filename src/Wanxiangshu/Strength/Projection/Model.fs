namespace Wanxiangshu.Strength.Projection
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

    let apply
        (projection: StrengthProjection)
        (event: StrengthEvent)
        : Result<StrengthProjection, StrengthProjectionError> =
        match event with
        | StrengthEvent.Prepared prepared ->
            let dkey = decisionKey prepared.DecisionId
            let tkey = targetKey prepared.TargetProviderRun

            match Map.tryFind dkey projection.ByDecision with
            | Some existing when existing.Prepared = prepared -> Ok projection
            | Some _ -> Error(StrengthProjectionError.PreparedConflict prepared.DecisionId)
            | None ->
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
                        { ByDecision = Map.add dkey view projection.ByDecision
                          ByTargetRun = Map.add tkey prepared.DecisionId projection.ByTargetRun }

        | StrengthEvent.Promoted promoted ->
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

        | StrengthEvent.Traced traced ->
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

                match view.TraceRange with
                | Some existing when existing = range -> Ok projection
                | Some _ -> Error(StrengthProjectionError.TraceConflict traced.DecisionId)
                | None ->
                    Ok
                        { projection with
                            ByDecision = Map.add dkey { view with TraceRange = Some range } projection.ByDecision }

        | StrengthEvent.Abandoned abandoned ->
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

    // No history-fold API by design. CanonicalIntegrator is the sole history
    // enumerator and registers `apply` as this module's one-event oracle.
