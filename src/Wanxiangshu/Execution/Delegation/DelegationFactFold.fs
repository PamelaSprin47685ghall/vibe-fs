namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Durable

module DelegationFactFold =

    let private advanceHandoff
        (projection: AgentProjectionSet)
        (payload:
            {| ParentSessionId: Identity.SessionId
               DelegateSessionId: Identity.SessionId
               ParentEndExclusive: int64 |})
        : Result<AgentProjectionSet, FoldRejection> =
        let key = DelegationHandoff.key payload.ParentSessionId payload.DelegateSessionId

        let previous =
            Map.tryFind key projection.DelegationHandoffs |> Option.defaultValue 0L

        if payload.ParentEndExclusive < previous then
            FoldRejection.reject "DelegationHandoffAdvanced" "parent handoff cursor cannot retreat"
        elif payload.ParentEndExclusive < 0L then
            FoldRejection.reject "DelegationHandoffAdvanced" "parent handoff cursor must be non-negative"
        else
            Ok
                { projection with
                    DelegationHandoffs = Map.add key payload.ParentEndExclusive projection.DelegationHandoffs }

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
        | DelegationFactCases.DelegationHandoffAdvanced payload -> advanceHandoff projection payload
