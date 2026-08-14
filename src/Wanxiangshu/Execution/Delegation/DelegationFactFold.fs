namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Composition.Durable

module DelegationFactFold =

    let fold projection fact =
        match fact with
        | DelegationFactCases.DelegatedToolEstimateReplaced payload ->
            if payload.ExpectedToolCalls < 0 then
                FoldRejection.reject
                    "DelegatedToolEstimateReplaced"
                    "expected tool calls must be a non-negative integer"
            else
                Ok(
                    AgentProjection.update
                        payload.SessionId
                        (fun session ->
                            { session with
                                DelegatedToolEstimate =
                                    Some(DelegatedToolEstimateProjection.replace payload.ExpectedToolCalls) })
                        projection
                )
        | DelegationFactCases.DelegatedToolCallObserved payload ->
            Ok(
                AgentProjection.update
                    payload.SessionId
                    (fun session ->
                        match session.DelegatedToolEstimate with
                        | Some estimate ->
                            { session with
                                DelegatedToolEstimate =
                                    Some(DelegatedToolEstimateProjection.observe payload.ToolCallId estimate) }
                        | None -> session)
                    projection
            )
