namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

module DelegationFactFold =

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
        (fact: DelegationFactCases)
        : Result<DelegationProjectionChange list, DelegationFoldRejection> =
        match fact with
        | DelegationFactCases.DelegatedToolEstimateReplaced payload -> replaceEstimate sessionState payload
        | DelegationFactCases.DelegatedToolCallObserved payload -> observeEstimate sessionState payload
