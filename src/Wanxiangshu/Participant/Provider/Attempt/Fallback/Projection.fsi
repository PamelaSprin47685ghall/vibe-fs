namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

type ObservedProviderFailure =
    { Key: string
      Count: int
      Reason: string }

type ProviderFailureProjection =
    { LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      Budget: ProviderFailureBudget.FailureBudget
      BudgetSpec: ProviderFailureBudgetSpec option
      ObservedFailures: ObservedProviderFailure list
      SuccessKeys: string list
      Exhausted: bool
      LastTransitionWasSuccess: bool }

type ProviderFailureAdvanceRejection =
    | AlreadyObserved
    | AlreadyExhausted
    | DifferentRun
    | NoActiveBudget
    | InvalidTransition
    | InvalidBudget
    | ConflictingObservation

module ProviderFailureProjection =
    val forAuthority:
        logicalRunId: LogicalRunId -> authorityRoot: AuthorityRootUserMessageId -> ProviderFailureProjection

    val applyFailure:
        identity: FailedProviderAttemptIdentity ->
        spec: ProviderFailureBudgetSpec ->
        consecutiveFailureCount: int ->
        reason: string ->
        current: ProviderFailureProjection ->
            Result<ProviderFailureProjection, ProviderFailureAdvanceRejection>

    val applyExhausted:
        finalCount: int ->
        current: ProviderFailureProjection ->
            Result<ProviderFailureProjection, ProviderFailureAdvanceRejection>

    val recordSuccess:
        identity: FailedProviderAttemptIdentity ->
        current: ProviderFailureProjection ->
            Result<ProviderFailureProjection, ProviderFailureAdvanceRejection>

    val mayRetry: current: ProviderFailureProjection -> bool
