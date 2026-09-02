namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode.Host.PairProgramming
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Interaction.Concern
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Session

module HostFactFold =

    let private reject = FoldRejection.reject

    let private guidelineRejection =
        function
        | GuidelineFoldRejection.NonSequentialOrdinal(expected, actual) ->
            { Fact = "PairProgrammingGuidelineAnchored"
              Reason = sprintf "ordinal %d is not the successor of %d (HOST-013)" actual expected }
        | GuidelineFoldRejection.DuplicateCallId callId ->
            { Fact = "PairProgrammingGuidelineAnchored"
              Reason = sprintf "call id %s already exists in this transcript (HOST-013)" callId }
        | GuidelineFoldRejection.DuplicatePlacement(callGap, resultGap) ->
            { Fact = "PairProgrammingGuidelineAnchored"
              Reason = sprintf "placement (%A, %A) already exists in this transcript (HOST-013 §8)" callGap resultGap }

    let private applyConcernPlacement sessionId placement (projection: AgentProjectionSet) =
        match placement with
        | None -> Ok projection
        | Some batch ->
            ConcernProjection.applyPlacement sessionId batch projection.Concern
            |> Result.map (fun concern -> { projection with Concern = concern })
            |> Result.mapError (fun reason ->
                { Fact = "PairProgrammingGuidelineAnchored"
                  Reason = reason })

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
            |> Result.mapError guidelineRejection
            |> Result.bind (applyConcernPlacement payload.SessionId payload.ConcernPlacement)

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
                                Some(RequirementGroundingProjection.applyMaterialObserved payload.Observation prior) })
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
