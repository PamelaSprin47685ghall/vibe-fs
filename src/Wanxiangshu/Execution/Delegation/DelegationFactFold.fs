namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Durable

module DelegationFactFold =

    let private replaceEstimate
        (projection: AgentProjectionSet)
        (payload:
            {| SessionId: Identity.SessionId
               ExpectedToolCalls: int |})
        : Result<AgentProjectionSet, FoldRejection> =
        if payload.ExpectedToolCalls < 0 then
            FoldRejection.reject "DelegatedToolEstimateReplaced" "expected tool calls must be a non-negative integer"
        else
            Ok(
                AgentProjection.update
                    payload.SessionId
                    (fun (session: SessionAgentProjection) ->
                        { session with
                            DelegatedToolEstimate =
                                Some(DelegatedToolEstimateProjection.replace payload.ExpectedToolCalls) })
                    projection
            )

    let private observeEstimate
        (projection: AgentProjectionSet)
        (payload:
            {| SessionId: Identity.SessionId
               ToolCallId: Identity.ToolCallId |})
        : Result<AgentProjectionSet, FoldRejection> =
        let update (session: SessionAgentProjection) =
            match session.DelegatedToolEstimate with
            | Some estimate ->
                { session with
                    DelegatedToolEstimate = Some(DelegatedToolEstimateProjection.observe payload.ToolCallId estimate) }
            | None -> session

        Ok(AgentProjection.update payload.SessionId update projection)

    let fold projection fact =
        match fact with
        | DelegationFactCases.DelegatedToolEstimateReplaced payload -> replaceEstimate projection payload
        | DelegationFactCases.DelegatedToolCallObserved payload -> observeEstimate projection payload
