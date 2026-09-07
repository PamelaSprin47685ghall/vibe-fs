namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type FailureAdmissionOutcome =
    | RetryAuthorized
    | RetryExhausted
    | EpisodeSuperseded
    | NoActiveRun

module ProviderFailureLedger =
    val recordAuthorizedFailure:
        journal: AgentJournal ->
        sessionId: SessionId ->
        authorization: ProviderRecoveryAuthorization ->
        reason: string ->
            Task<Result<FailureAdmissionOutcome, string>>

    val recordConfirmedSuccess:
        journal: AgentJournal -> sessionId: SessionId -> providerRun: ProviderRunIdentity -> Task<Result<unit, string>>
