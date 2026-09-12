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

[<RequireQualifiedAccess>]
module ProviderFailureFoldRejection =
    let fact (rejection: ProviderFailureFoldRejection) : string =
        match rejection with
        | ProviderFailureFoldRejection.FailureRecordedWithoutBudget
        | ProviderFailureFoldRejection.InvalidTransition -> "FailureRecorded"
        | ProviderFailureFoldRejection.RetryExhaustedWithoutBudget -> "RetryExhausted"
        | ProviderFailureFoldRejection.SuccessRecordedWithoutBudget -> "SuccessRecorded"

    let message (rejection: ProviderFailureFoldRejection) : string =
        match rejection with
        | ProviderFailureFoldRejection.FailureRecordedWithoutBudget ->
            "provider failure has no active budget: requires an accepted Authority Root"
        | ProviderFailureFoldRejection.RetryExhaustedWithoutBudget ->
            "retry exhausted has no active budget: requires an accepted Authority Root"
        | ProviderFailureFoldRejection.SuccessRecordedWithoutBudget ->
            "success has no budget to clear: requires an accepted Authority Root"
        | ProviderFailureFoldRejection.InvalidTransition ->
            "provider failure violates validation (consecutive failure count is not valid successor)"

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
               ConsecutiveFailureCount: int
               Reason: string |})
        (current: ProviderFailureProjection)
        : Result<ProviderFailureProjectionChange list, ProviderFailureFoldRejection> =
        let identity: FailedProviderAttemptIdentity =
            { SessionId = payload.SessionId
              LogicalRunId = payload.LogicalRunId
              AuthorityRootUserMessageId = payload.AuthorityRootUserMessageId
              ProviderRun = payload.ProviderRun }

        match ProviderFailureProjection.applyFailure identity payload.ConsecutiveFailureCount current with
        | Ok updated -> Ok [ ProviderFailureProjectionChange.ProviderFailuresSet(payload.SessionId, updated) ]
        | Error ProviderFailureAdvanceRejection.AlreadyObserved
        | Error ProviderFailureAdvanceRejection.AlreadyExhausted
        | Error ProviderFailureAdvanceRejection.DifferentRun -> Ok []
        | Error ProviderFailureAdvanceRejection.NoActiveBudget ->
            Error ProviderFailureFoldRejection.FailureRecordedWithoutBudget
        | Error ProviderFailureAdvanceRejection.InvalidTransition ->
            Error ProviderFailureFoldRejection.InvalidTransition

    let private foldFailureRecorded
        (providerFailuresOf: SessionId -> ProviderFailureProjection option)
        (payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity
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
            let updated = ProviderFailureProjection.applyExhausted current
            Ok [ ProviderFailureProjectionChange.ProviderFailuresSet(payload.SessionId, updated) ]

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
            let updated = ProviderFailureProjection.recordSuccess current
            Ok [ ProviderFailureProjectionChange.ProviderFailuresSet(payload.SessionId, updated) ]

    let fold
        (providerFailuresOf: SessionId -> ProviderFailureProjection option)
        (fact: ProviderFailureFactCases)
        : Result<ProviderFailureProjectionChange list, ProviderFailureFoldRejection> =
        match fact with
        | ProviderFailureFactCases.FailureRecorded payload -> foldFailureRecorded providerFailuresOf payload
        | ProviderFailureFactCases.RetryExhausted payload -> foldRetryExhausted providerFailuresOf payload
        | ProviderFailureFactCases.SuccessRecorded payload -> foldSuccessRecorded providerFailuresOf payload
