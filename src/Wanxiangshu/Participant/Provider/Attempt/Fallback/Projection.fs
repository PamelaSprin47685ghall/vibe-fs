namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Foundation.Identity

/// Durable provider failure projection for one Logical Run.
type ProviderFailureProjection =
    { LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      Budget: ProviderFailureBudget.FailureBudget
      RecentFailureKeys: string list
      Exhausted: bool
      LastTransitionWasSuccess: bool }

/// Why a failure record was not applied.
type ProviderFailureAdvanceRejection =
    | AlreadyObserved
    | AlreadyExhausted
    | DifferentRun
    | NoActiveBudget
    | InvalidTransition

module ProviderFailureProjection =

    [<Literal>]
    let private DedupeWindow = 32

    let forAuthority (logicalRunId: LogicalRunId) (authorityRoot: AuthorityRootUserMessageId) =
        { LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRoot
          Budget = ProviderFailureBudget.forNewAuthorityRoot
          RecentFailureKeys = []
          Exhausted = false
          LastTransitionWasSuccess = false }

    let private remember key keys =
        key :: (keys |> List.filter ((<>) key)) |> List.truncate DedupeWindow

    /// Apply one failure record.
    let applyFailure
        (identity: FailedProviderAttemptIdentity)
        (consecutiveFailureCount: int)
        (current: ProviderFailureProjection)
        : Result<ProviderFailureProjection, ProviderFailureAdvanceRejection> =
        let key = FailedProviderAttemptIdentity.dedupeKey identity

        if current.Exhausted then
            Error AlreadyExhausted
        elif
            identity.LogicalRunId <> current.LogicalRunId
            || identity.AuthorityRootUserMessageId <> current.AuthorityRootUserMessageId
        then
            Error DifferentRun
        elif List.contains key current.RecentFailureKeys then
            Error AlreadyObserved
        elif
            not (ProviderFailureBudget.isValidRecord current.Budget.ConsecutiveFailureCount consecutiveFailureCount)
        then
            Error InvalidTransition
        else
            Ok
                { current with
                    Budget = { ConsecutiveFailureCount = consecutiveFailureCount }
                    RecentFailureKeys = remember key current.RecentFailureKeys
                    LastTransitionWasSuccess = false }

    /// Terminal exhausted state.
    let applyExhausted (current: ProviderFailureProjection) = { current with Exhausted = true }

    /// Success clears the budget streak.
    let recordSuccess (current: ProviderFailureProjection) =
        { current with
            Budget = ProviderFailureBudget.recordSuccess current.Budget
            RecentFailureKeys = []
            LastTransitionWasSuccess = true }

    let private budgetMayRetry (budgetLimit: int) (budget: ProviderFailureBudget.FailureBudget) =
        match ProviderFailureBudget.verdict budgetLimit budget with
        | ProviderFailureBudget.MayRetry _ -> true
        | ProviderFailureBudget.Exhausted _ -> false

    let mayRetry (budgetLimit: int) (current: ProviderFailureProjection) =
        if current.Exhausted then
            false
        else
            budgetMayRetry budgetLimit current.Budget
