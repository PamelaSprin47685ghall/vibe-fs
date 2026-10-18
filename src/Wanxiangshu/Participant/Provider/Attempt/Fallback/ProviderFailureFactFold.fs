namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type ProviderFailureProjectionChange = ProviderFailuresSet of SessionId * ProviderFailureProjection

[<RequireQualifiedAccess>]
type ProviderFailureFoldRejection =
    | FailureRecordedWithoutBudget
    | RetryExhaustedWithoutBudget
    | SuccessRecordedWithoutBudget
    | InvalidTransition
    | InvalidExhaustion
    | ConflictingSuccess

[<RequireQualifiedAccess>]
module ProviderFailureFoldRejection =
    let fact (rejection: ProviderFailureFoldRejection) : string =
        match rejection with
        | ProviderFailureFoldRejection.FailureRecordedWithoutBudget
        | ProviderFailureFoldRejection.InvalidTransition -> "FailureRecorded"
        | ProviderFailureFoldRejection.RetryExhaustedWithoutBudget
        | ProviderFailureFoldRejection.InvalidExhaustion -> "RetryExhausted"
        | ProviderFailureFoldRejection.SuccessRecordedWithoutBudget
        | ProviderFailureFoldRejection.ConflictingSuccess -> "SuccessRecorded"

    let message (rejection: ProviderFailureFoldRejection) : string =
        match rejection with
        | ProviderFailureFoldRejection.FailureRecordedWithoutBudget ->
            "provider failure has no active budget: requires an accepted Authority Root"
        | ProviderFailureFoldRejection.RetryExhaustedWithoutBudget ->
            "retry exhausted has no active budget: requires an accepted Authority Root"
        | ProviderFailureFoldRejection.SuccessRecordedWithoutBudget ->
            "success has no budget to clear: requires an accepted Authority Root"
        | ProviderFailureFoldRejection.InvalidTransition ->
            "provider failure violates validation (budget, count or observed identity conflicts)"
        | ProviderFailureFoldRejection.InvalidExhaustion ->
            "retry exhausted does not confirm the persisted failure limit"
        | ProviderFailureFoldRejection.ConflictingSuccess ->
            "provider success conflicts with an observed failure identity"

module ProviderFailureFactFold =

    let private isSuperseded
        (current: ProviderFailureProjection)
        (logicalRunId: LogicalRunId)
        (authorityRoot: AuthorityRootUserMessageId)
        : bool =
        current.LogicalRunId <> logicalRunId
        || current.AuthorityRootUserMessageId <> authorityRoot

    let private applyFailureRecord
        (payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity
               BudgetSpec: ProviderFailureBudgetSpec
               ConsecutiveFailureCount: int
               Reason: string |})
        (current: ProviderFailureProjection)
        : Result<ProviderFailureProjectionChange list, ProviderFailureFoldRejection> =
        let identity: FailedProviderAttemptIdentity =
            { SessionId = payload.SessionId
              LogicalRunId = payload.LogicalRunId
              AuthorityRootUserMessageId = payload.AuthorityRootUserMessageId
              ProviderRun = payload.ProviderRun }

        match
            ProviderFailureProjection.applyFailure
                identity
                payload.BudgetSpec
                payload.ConsecutiveFailureCount
                payload.Reason
                current
        with
        | Ok updated -> Ok [ ProviderFailureProjectionChange.ProviderFailuresSet(payload.SessionId, updated) ]
        | Error ProviderFailureAdvanceRejection.AlreadyObserved
        | Error ProviderFailureAdvanceRejection.DifferentRun -> Ok []
        | Error ProviderFailureAdvanceRejection.NoActiveBudget ->
            Error ProviderFailureFoldRejection.FailureRecordedWithoutBudget
        | Error ProviderFailureAdvanceRejection.AlreadyExhausted
        | Error ProviderFailureAdvanceRejection.InvalidBudget
        | Error ProviderFailureAdvanceRejection.ConflictingObservation
        | Error ProviderFailureAdvanceRejection.InvalidTransition ->
            Error ProviderFailureFoldRejection.InvalidTransition

    let private foldFailureRecorded
        (providerFailuresOf: SessionId -> ProviderFailureProjection option)
        (payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity
               BudgetSpec: ProviderFailureBudgetSpec
               ConsecutiveFailureCount: int
               Reason: string |})
        : Result<ProviderFailureProjectionChange list, ProviderFailureFoldRejection> =
        match providerFailuresOf payload.SessionId with
        | None -> Error ProviderFailureFoldRejection.FailureRecordedWithoutBudget
        | Some current -> applyFailureRecord payload current

    let private foldRetryExhausted
        (providerFailuresOf: SessionId -> ProviderFailureProjection option)
        (payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               FinalConsecutiveFailureCount: int |})
        : Result<ProviderFailureProjectionChange list, ProviderFailureFoldRejection> =
        match providerFailuresOf payload.SessionId with
        | None -> Error ProviderFailureFoldRejection.RetryExhaustedWithoutBudget
        | Some current when isSuperseded current payload.LogicalRunId payload.AuthorityRootUserMessageId -> Ok []
        | Some current ->
            ProviderFailureProjection.applyExhausted payload.FinalConsecutiveFailureCount current
            |> Result.map (fun updated ->
                [ ProviderFailureProjectionChange.ProviderFailuresSet(payload.SessionId, updated) ])
            |> Result.mapError (fun _ -> ProviderFailureFoldRejection.InvalidExhaustion)

    let private applySuccess identity sessionId current =
        match ProviderFailureProjection.recordSuccess identity current with
        | Ok updated -> Ok [ ProviderFailureProjectionChange.ProviderFailuresSet(sessionId, updated) ]
        | Error ProviderFailureAdvanceRejection.AlreadyObserved -> Ok []
        | Error _ -> Error ProviderFailureFoldRejection.ConflictingSuccess

    let private foldSuccessRecorded
        (providerFailuresOf: SessionId -> ProviderFailureProjection option)
        (payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity |})
        : Result<ProviderFailureProjectionChange list, ProviderFailureFoldRejection> =
        match providerFailuresOf payload.SessionId with
        | None -> Error ProviderFailureFoldRejection.SuccessRecordedWithoutBudget
        | Some current when isSuperseded current payload.LogicalRunId payload.AuthorityRootUserMessageId -> Ok []
        | Some current ->
            let identity =
                ProviderFailureBudget.attemptIdentity
                    payload.SessionId
                    payload.LogicalRunId
                    payload.AuthorityRootUserMessageId
                    payload.ProviderRun

            applySuccess identity payload.SessionId current

    let fold
        (providerFailuresOf: SessionId -> ProviderFailureProjection option)
        (fact: ProviderFailureFactCases)
        : Result<ProviderFailureProjectionChange list, ProviderFailureFoldRejection> =
        match fact with
        | ProviderFailureFactCases.FailureRecorded payload -> foldFailureRecorded providerFailuresOf payload
        | ProviderFailureFactCases.RetryExhausted payload -> foldRetryExhausted providerFailuresOf payload
        | ProviderFailureFactCases.SuccessRecorded payload -> foldSuccessRecorded providerFailuresOf payload
