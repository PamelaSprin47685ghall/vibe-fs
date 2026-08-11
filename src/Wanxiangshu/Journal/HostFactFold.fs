namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal.ProjectionUpdate

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
                | Ok updated -> Ok updated
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
