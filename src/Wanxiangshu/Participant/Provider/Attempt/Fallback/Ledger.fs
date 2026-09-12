namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt

[<RequireQualifiedAccess>]
type FailureAdmissionOutcome =
    | RetryAuthorized
    | RetryExhausted
    | EpisodeSuperseded
    | NoActiveRun

/// Single writer: policy-authorized provider failure → durable dedupe → budget advance/exhaust.
module ProviderFailureLedger =

    let private replayLatestFailure
        (budgetLimit: int)
        (identity: FailedProviderAttemptIdentity)
        (current: ProviderFailureProjection)
        =
        let exactLatest =
            current.RecentFailureKeys
            |> List.tryHead
            |> Option.contains (FailedProviderAttemptIdentity.dedupeKey identity)

        match exactLatest, ProviderFailureProjection.mayRetry budgetLimit current with
        | true, true -> FailureAdmissionOutcome.RetryAuthorized
        | true, false -> FailureAdmissionOutcome.RetryExhausted
        | false, _ -> FailureAdmissionOutcome.EpisodeSuperseded

    let private appendExhausted
        (port: ProviderFailureJournalPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: ProviderFailureProjection)
        (finalCount: int)
        : Task<Result<FailureAdmissionOutcome, string>> =
        task {
            let exhausted =
                ProviderFailureFactCases.RetryExhausted
                    {| SessionId = sessionId
                       LogicalRunId = current.LogicalRunId
                       AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                       FinalConsecutiveFailureCount = finalCount |}

            let! appended = port.Append sessionId providerRun exhausted

            return appended |> Result.map (fun _ -> FailureAdmissionOutcome.RetryExhausted)
        }

    let private completeAdvance
        (port: ProviderFailureJournalPort)
        (budgetLimit: int)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: ProviderFailureProjection)
        (nextBudget: ProviderFailureBudget.FailureBudget)
        : Task<Result<FailureAdmissionOutcome, string>> =
        task {
            match ProviderFailureBudget.verdict budgetLimit nextBudget with
            | ProviderFailureBudget.MayRetry _ -> return Ok FailureAdmissionOutcome.RetryAuthorized
            | ProviderFailureBudget.Exhausted _ ->
                return! appendExhausted port sessionId providerRun current nextBudget.ConsecutiveFailureCount
        }

    let private appendFailureRecorded
        (port: ProviderFailureJournalPort)
        (budgetLimit: int)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reason: string)
        (current: ProviderFailureProjection)
        (nextCount: int)
        : Task<Result<FailureAdmissionOutcome, string>> =
        task {
            let recorded =
                ProviderFailureFactCases.FailureRecorded
                    {| SessionId = sessionId
                       LogicalRunId = current.LogicalRunId
                       AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                       ProviderRun = providerRun
                       ConsecutiveFailureCount = nextCount
                       Reason = reason |}

            let! appended = port.Append sessionId providerRun recorded

            match appended with
            | Error failure -> return Error failure
            | Ok _ ->
                return!
                    completeAdvance
                        port
                        budgetLimit
                        sessionId
                        providerRun
                        current
                        { ConsecutiveFailureCount = nextCount }
        }

    let private checkRecoveryLicence
        (authorization: ProviderRecoveryAuthorization)
        (current: ProviderFailureProjection)
        : Result<ProviderFailureProjection, string> =
        if current.LogicalRunId <> authorization.LogicalRun then
            Error "Provider recovery licence belongs to a different logical run"
        else
            Ok current

    let private outcomeForAdvanceRejection
        (identity: FailedProviderAttemptIdentity)
        (current: ProviderFailureProjection)
        (rejection: ProviderFailureAdvanceRejection)
        : Result<FailureAdmissionOutcome, string> =
        match rejection with
        | ProviderFailureAdvanceRejection.AlreadyObserved ->
            Ok(replayLatestFailure ProviderFailureBudget.DefaultBudget identity current)
        | ProviderFailureAdvanceRejection.AlreadyExhausted -> Ok FailureAdmissionOutcome.RetryExhausted
        | ProviderFailureAdvanceRejection.DifferentRun
        | ProviderFailureAdvanceRejection.NoActiveBudget -> Ok FailureAdmissionOutcome.NoActiveRun
        | ProviderFailureAdvanceRejection.InvalidTransition ->
            Error "Provider failure advance violates validation (consecutive failure count is not the successor)"

    let private recordReadyFailure
        (port: ProviderFailureJournalPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reason: string)
        (current: ProviderFailureProjection)
        : Task<Result<FailureAdmissionOutcome, string>> =
        task {
            let identity =
                ProviderFailureBudget.attemptIdentity
                    sessionId
                    current.LogicalRunId
                    current.AuthorityRootUserMessageId
                    providerRun

            let nextCount = current.Budget.ConsecutiveFailureCount + 1

            match ProviderFailureProjection.applyFailure identity nextCount current with
            | Error rejection -> return outcomeForAdvanceRejection identity current rejection
            | Ok _ ->
                return!
                    appendFailureRecorded
                        port
                        ProviderFailureBudget.DefaultBudget
                        sessionId
                        providerRun
                        reason
                        current
                        nextCount
        }

    let private recordAfterLicenceCheck
        (port: ProviderFailureJournalPort)
        (sessionId: SessionId)
        (authorization: ProviderRecoveryAuthorization)
        (reason: string)
        (current: ProviderFailureProjection)
        : Task<Result<FailureAdmissionOutcome, string>> =
        task {
            match checkRecoveryLicence authorization current with
            | Error message -> return Error message
            | Ok ready -> return! recordReadyFailure port sessionId authorization.ProviderRun reason ready
        }

    let recordAuthorizedFailure
        (port: ProviderFailureJournalPort)
        (sessionId: SessionId)
        (authorization: ProviderRecoveryAuthorization)
        (reason: string)
        : Task<Result<FailureAdmissionOutcome, string>> =
        task {
            match port.CurrentState sessionId with
            | None -> return Ok FailureAdmissionOutcome.NoActiveRun
            | Some current -> return! recordAfterLicenceCheck port sessionId authorization reason current
        }

    let private appendSuccessFact
        (port: ProviderFailureJournalPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: ProviderFailureProjection)
        : Task<Result<unit, string>> =
        task {
            let succeeded =
                ProviderFailureFactCases.SuccessRecorded
                    {| SessionId = sessionId
                       LogicalRunId = current.LogicalRunId
                       AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                       ProviderRun = providerRun |}

            let! appended = port.Append sessionId providerRun succeeded

            return appended |> Result.map (fun _ -> ())
        }

    let private recordSuccessForCurrent
        (port: ProviderFailureJournalPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: ProviderFailureProjection)
        : Task<Result<unit, string>> =
        if current.Budget.ConsecutiveFailureCount = 0 then
            Task.FromResult(Ok())
        else
            appendSuccessFact port sessionId providerRun current

    let recordConfirmedSuccess
        (port: ProviderFailureJournalPort)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        : Task<Result<unit, string>> =
        task {
            match port.CurrentState sessionId with
            | None -> return Error "NoActiveRun: no provider failure state for session"
            | Some current -> return! recordSuccessForCurrent port sessionId providerRun current
        }
