namespace Wanxiangshu.Composition.Durable

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
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.OpenCode.Host.PairProgramming
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Concern
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
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.ProjectionUpdate
open Wanxiangshu.Execution.Session
open Wanxiangshu.Enforcer.Guidance

module HostFactFold =

    let private reject = FoldRejection.reject

    let fold (projection: AgentProjectionSet) (fact: HostFactCases) : Result<AgentProjectionSet, FoldRejection> =
        match fact with
        | HostFactCases.PairProgrammingGuidelineAnchored payload ->
            AgentProjection.tryUpdate
                payload.SessionId
                (fun session ->
                    session.Guidelines
                    |> Option.defaultValue GuidelineProjection.empty
                    |> GuidelineProjection.apply
                        payload.Ordinal
                        payload.CallId
                        payload.MarkerText
                        payload.CallGap
                        payload.ResultGap
                    |> Result.map (fun updated ->
                        { session with
                            Guidelines = Some updated }))
                projection
            |> function
                | Ok updated ->
                    match payload.ConcernPlacement with
                    | None -> Ok updated
                    | Some batch ->
                        ConcernProjection.applyPlacement payload.SessionId batch updated.Concern
                        |> Result.map (fun concern -> { updated with Concern = concern })
                        |> Result.mapError (fun reason ->
                            { Fact = "PairProgrammingGuidelineAnchored"
                              Reason = reason })
                | Error(GuidelineFoldRejection.NonSequentialOrdinal(expected, actual)) ->
                    reject
                        "PairProgrammingGuidelineAnchored"
                        (sprintf "ordinal %d is not the successor of %d (HOST-013)" actual expected)
                | Error(GuidelineFoldRejection.DuplicateCallId callId) ->
                    reject
                        "PairProgrammingGuidelineAnchored"
                        (sprintf "call id %s already exists in this transcript (HOST-013)" callId)
                | Error(GuidelineFoldRejection.DuplicatePlacement(callGap, resultGap)) ->
                    reject
                        "PairProgrammingGuidelineAnchored"
                        (sprintf "placement (%A, %A) already exists in this transcript (HOST-013 §8)" callGap resultGap)

        | HostFactCases.RequirementGroundingRequested payload ->
            Ok(
                AgentProjection.update
                    payload.SessionId
                    (fun session ->
                        let prior =
                            session.RequirementGrounding
                            |> Option.defaultValue RequirementGroundingProjection.empty

                        { session with
                            RequirementGrounding =
                                Some(RequirementGroundingProjection.applyRequested payload.Snapshot prior) })
                    projection
            )

        | HostFactCases.RequirementGroundingMaterialObserved payload ->
            Ok(
                AgentProjection.update
                    payload.SessionId
                    (fun session ->
                        let prior =
                            session.RequirementGrounding
                            |> Option.defaultValue RequirementGroundingProjection.empty

                        { session with
                            RequirementGrounding =
                                Some(
                                    RequirementGroundingProjection.applyMaterialObserved
                                        payload.Observation
                                        prior
                                ) })
                    projection
            )

        | HostFactCases.RequirementGroundingAnchored payload ->
            AgentProjection.tryUpdate
                payload.SessionId
                (fun session ->
                    session.RequirementGrounding
                    |> Option.defaultValue RequirementGroundingProjection.empty
                    |> RequirementGroundingProjection.applyAnchored payload.Occurrence
                    |> Result.map (fun updated ->
                        { session with
                            RequirementGrounding = Some updated }))
                projection
            |> function
                | Ok updated -> Ok updated
                | Error(RequirementGroundingFoldRejection.NonSequentialOrdinal(expected, actual)) ->
                    reject
                        "RequirementGroundingAnchored"
                        (sprintf "ordinal %d is not the successor of %d" actual expected)
                | Error(RequirementGroundingFoldRejection.DuplicateIdentity identity) ->
                    reject "RequirementGroundingAnchored" ("identity already grounded: " + identity)
                | Error(RequirementGroundingFoldRejection.MissingRequest identity) ->
                    reject "RequirementGroundingAnchored" ("missing grounding request: " + identity)

        | HostFactCases.TipGuidanceDelivered payload ->
            // Idempotent: Full tips accumulate; IdentityOnly is a no-op on the set.
            Ok(
                AgentProjection.update
                    payload.SessionId
                    (fun session ->
                        let prior = session.TipDelivery |> Option.defaultValue TipDeliveryProjection.empty

                        { session with
                            TipDelivery = Some(TipDeliveryProjection.apply payload.TipName payload.Presentation prior) })
                    projection
            )

        | HostFactCases.SessionStartedAtBound payload ->
            Ok(
                AgentProjection.update
                    payload.SessionId
                    (fun session ->
                        { session with
                            SessionStartedAt =
                                Some(SessionStartedAtProjection.bind payload.StartedAt session.SessionStartedAt) })
                    projection
            )
