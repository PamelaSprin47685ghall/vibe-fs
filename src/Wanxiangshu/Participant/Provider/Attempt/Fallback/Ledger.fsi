namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type FailureAdmissionOutcome =
    | RetryAuthorized
    | RetryExhausted
    | EpisodeSuperseded
    | NoActiveRun

module ProviderFailureLedger =
    val recordAuthorizedFailure:
        port: ProviderFailureJournalPort ->
        sessionId: SessionId ->
        authorization: ProviderRecoveryAuthorization ->
        reason: string ->
            Task<Result<FailureAdmissionOutcome, string>>

    val recordConfirmedSuccess:
        port: ProviderFailureJournalPort ->
        sessionId: SessionId ->
        providerRun: ProviderRunIdentity ->
            Task<Result<unit, string>>
