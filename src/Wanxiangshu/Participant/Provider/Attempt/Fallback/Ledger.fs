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

    let private replayLatestFailure (identity: FailedProviderAttemptIdentity) (current: ProviderFailureProjection) =
        let exactLatest =
            (current.ObservedFailures
             |> List.tryHead
             |> Option.exists (fun observed -> observed.Key = FailedProviderAttemptIdentity.dedupeKey identity))
            && not current.LastTransitionWasSuccess

        match exactLatest, ProviderFailureProjection.mayRetry current with
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
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (current: ProviderFailureProjection)
        : Task<Result<FailureAdmissionOutcome, string>> =
        task {
            if ProviderFailureProjection.mayRetry current then
                return Ok FailureAdmissionOutcome.RetryAuthorized
            else
                return! appendExhausted port sessionId providerRun current current.Budget.ConsecutiveFailureCount
        }

    let private appendFailureRecorded
        (port: ProviderFailureJournalPort)
        (spec: ProviderFailureBudgetSpec)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        (reason: string)
        (current: ProviderFailureProjection)
        (nextCount: int)
        (updated: ProviderFailureProjection)
        : Task<Result<FailureAdmissionOutcome, string>> =
        task {
            let recorded =
                ProviderFailureFactCases.FailureRecorded
                    {| SessionId = sessionId
                       LogicalRunId = current.LogicalRunId
                       AuthorityRootUserMessageId = current.AuthorityRootUserMessageId
                       ProviderRun = providerRun
                       BudgetSpec = spec
                       ConsecutiveFailureCount = nextCount
                       Reason = reason |}

            let! appended = port.Append sessionId providerRun recorded

            match appended with
            | Error failure -> return Error failure
            | Ok _ -> return! completeAdvance port sessionId providerRun updated
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
        | ProviderFailureAdvanceRejection.AlreadyObserved -> Ok(replayLatestFailure identity current)
        | ProviderFailureAdvanceRejection.AlreadyExhausted -> Ok FailureAdmissionOutcome.RetryExhausted
        | ProviderFailureAdvanceRejection.DifferentRun
        | ProviderFailureAdvanceRejection.NoActiveBudget -> Ok FailureAdmissionOutcome.NoActiveRun
        | ProviderFailureAdvanceRejection.InvalidBudget
        | ProviderFailureAdvanceRejection.ConflictingObservation
        | ProviderFailureAdvanceRejection.InvalidTransition ->
            Error "Provider failure advance violates validation (budget, count or observed identity conflicts)"

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

            let key = FailedProviderAttemptIdentity.dedupeKey identity

            let appendNewFailure () =
                let spec =
                    current.BudgetSpec
                    |> Option.defaultWith (fun () ->
                        { Version = 1
                          Limit = ProviderFailureBudget.DefaultBudget })

                let nextCount = current.Budget.ConsecutiveFailureCount + 1

                match ProviderFailureProjection.applyFailure identity spec nextCount reason current with
                | Error rejection -> Task.FromResult(outcomeForAdvanceRejection identity current rejection)
                | Ok updated -> appendFailureRecorded port spec sessionId providerRun reason current nextCount updated

            match current.ObservedFailures |> List.tryFind (fun observed -> observed.Key = key) with
            | Some observed when observed.Reason = reason -> return Ok(replayLatestFailure identity current)
            | Some _ -> return Error "Provider failure identity conflicts with its recorded reason"
            | None -> return! appendNewFailure ()
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
        let identity =
            ProviderFailureBudget.attemptIdentity
                sessionId
                current.LogicalRunId
                current.AuthorityRootUserMessageId
                providerRun

        match ProviderFailureProjection.recordSuccess identity current with
        | Ok _ -> appendSuccessFact port sessionId providerRun current
        | Error ProviderFailureAdvanceRejection.AlreadyObserved -> Task.FromResult(Ok())
        | Error _ -> Task.FromResult(Error "Provider success conflicts with its recorded failure identity")

    let recordConfirmedSuccess
        (port: ProviderFailureJournalPort)
        (sessionId: SessionId)
        (authorityRoot: AuthorityRootUserMessageId)
        (providerRun: ProviderRunIdentity)
        : Task<Result<unit, string>> =
        task {
            match port.CurrentState sessionId with
            | None -> return Error "NoActiveRun: no provider failure state for session"
            | Some current when current.AuthorityRootUserMessageId <> authorityRoot ->
                return Error "Provider success belongs to a different authority root"
            | Some current -> return! recordSuccessForCurrent port sessionId providerRun current
        }
