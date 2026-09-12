namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

module DelegationFactFold =

    let private completeHandoff
        (handoffFrontier: string -> int64 option)
        (payload:
            {| ParentSessionId: SessionId
               Route: DelegationHandoffRoute
               ParentEndExclusive: int64 |})
        : Result<DelegationProjectionChange list, DelegationFoldRejection> =
        let key = DelegationHandoff.key payload.ParentSessionId payload.Route

        let previous = handoffFrontier key |> Option.defaultValue 0L

        if payload.ParentEndExclusive < previous then
            Error(HandoffFrontierCannotRetreat(previous, payload.ParentEndExclusive))
        elif payload.ParentEndExclusive < 0L then
            Error(HandoffFrontierNegative payload.ParentEndExclusive)
        else
            Ok [ MoveHandoffFrontier(key, payload.ParentEndExclusive) ]

    let private replaceEstimate
        (sessionState: SessionId -> DelegationSessionState option)
        (payload:
            {| SessionId: SessionId
               ExpectedToolCalls: int |})
        : Result<DelegationProjectionChange list, DelegationFoldRejection> =
        if payload.ExpectedToolCalls < 0 then
            Error(ToolEstimateNegative payload.ExpectedToolCalls)
        else
            let state =
                sessionState payload.SessionId
                |> Option.defaultValue DelegationSessionState.empty

            let updatedState =
                { state with
                    ToolEstimate = Some(DelegatedToolEstimateProjection.replace payload.ExpectedToolCalls) }

            Ok [ ReplaceSessionState(payload.SessionId, updatedState) ]

    let private observeEstimate
        (sessionState: SessionId -> DelegationSessionState option)
        (payload:
            {| SessionId: SessionId
               ToolCallId: ToolCallId |})
        : Result<DelegationProjectionChange list, DelegationFoldRejection> =
        let state =
            sessionState payload.SessionId
            |> Option.defaultValue DelegationSessionState.empty

        let updatedState =
            match state.ToolEstimate with
            | Some estimate ->
                { state with
                    ToolEstimate = Some(DelegatedToolEstimateProjection.observe payload.ToolCallId estimate) }
            | None -> state

        Ok [ ReplaceSessionState(payload.SessionId, updatedState) ]

    let fold
        (sessionState: SessionId -> DelegationSessionState option)
        (handoffFrontier: string -> int64 option)
        (fact: DelegationFactCases)
        : Result<DelegationProjectionChange list, DelegationFoldRejection> =
        match fact with
        | DelegationFactCases.DelegatedToolEstimateReplaced payload -> replaceEstimate sessionState payload
        | DelegationFactCases.DelegatedToolCallObserved payload -> observeEstimate sessionState payload
        | DelegationFactCases.DelegationHandoffCompleted payload -> completeHandoff handoffFrontier payload
