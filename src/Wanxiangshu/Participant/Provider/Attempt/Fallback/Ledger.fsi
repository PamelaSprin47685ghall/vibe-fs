namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type ConfirmedFailureOutcome =
    | RecoveryAdvanced of RecoveryOpportunity
    | RecoveryExhausted
    | AlreadyRecorded
    | NoActiveRun

module FallbackLedger =
    val recordAuthorizedFailure:
        journal: AgentJournal ->
        sessionId: SessionId ->
        authorization: ProviderRecoveryAuthorization ->
        reason: string ->
            Task<Result<ConfirmedFailureOutcome, string>>

    val recordConfirmedSuccess:
        journal: AgentJournal -> sessionId: SessionId -> providerRun: ProviderRunIdentity -> Task<Result<unit, string>>
