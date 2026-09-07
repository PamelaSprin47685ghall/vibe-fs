namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

type ProviderFailureProjection =
    { LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      Budget: ProviderFailureBudget.FailureBudget
      RecentFailureKeys: string list
      Exhausted: bool
      LastTransitionWasSuccess: bool }

type ProviderFailureAdvanceRejection =
    | AlreadyObserved
    | AlreadyExhausted
    | DifferentRun
    | NoActiveBudget
    | InvalidTransition

module ProviderFailureProjection =
    val forAuthority:
        logicalRunId: LogicalRunId -> authorityRoot: AuthorityRootUserMessageId -> ProviderFailureProjection

    val applyFailure:
        identity: FailedProviderAttemptIdentity ->
        consecutiveFailureCount: int ->
        current: ProviderFailureProjection ->
            Result<ProviderFailureProjection, ProviderFailureAdvanceRejection>

    val applyExhausted: current: ProviderFailureProjection -> ProviderFailureProjection
    val recordSuccess: current: ProviderFailureProjection -> ProviderFailureProjection
    val mayRetry: budgetLimit: int -> current: ProviderFailureProjection -> bool
