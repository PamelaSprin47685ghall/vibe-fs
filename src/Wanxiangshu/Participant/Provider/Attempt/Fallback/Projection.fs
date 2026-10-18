namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Foundation.Identity

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

/// Why a failure record was not applied.
type ProviderFailureAdvanceRejection =
    | AlreadyObserved
    | AlreadyExhausted
    | DifferentRun
    | NoActiveBudget
    | InvalidTransition
    | InvalidBudget
    | ConflictingObservation

module ProviderFailureProjection =

    let forAuthority (logicalRunId: LogicalRunId) (authorityRoot: AuthorityRootUserMessageId) =
        { LogicalRunId = logicalRunId
          AuthorityRootUserMessageId = authorityRoot
          Budget = ProviderFailureBudget.forNewAuthorityRoot
          BudgetSpec = None
          ObservedFailures = []
          SuccessKeys = []
          Exhausted = false
          LastTransitionWasSuccess = false }

    let private isDifferentRun (identity: FailedProviderAttemptIdentity) (current: ProviderFailureProjection) =
        identity.LogicalRunId <> current.LogicalRunId
        || identity.AuthorityRootUserMessageId <> current.AuthorityRootUserMessageId

    /// Apply one failure record.
    let applyFailure
        (identity: FailedProviderAttemptIdentity)
        (spec: ProviderFailureBudgetSpec)
        (consecutiveFailureCount: int)
        (reason: string)
        (current: ProviderFailureProjection)
        : Result<ProviderFailureProjection, ProviderFailureAdvanceRejection> =
        let key = FailedProviderAttemptIdentity.dedupeKey identity

        match current.ObservedFailures |> List.tryFind (fun observed -> observed.Key = key) with
        | _ when isDifferentRun identity current -> Error DifferentRun
        | _ when
            not (ProviderFailureBudgetSpec.isValid spec)
            || (current.BudgetSpec |> Option.exists ((<>) spec))
            ->
            Error InvalidBudget
        | _ when List.contains key current.SuccessKeys -> Error ConflictingObservation
        | Some observed when observed.Count = consecutiveFailureCount && observed.Reason = reason ->
            Error AlreadyObserved
        | Some _ -> Error ConflictingObservation
        | None when current.Exhausted -> Error AlreadyExhausted
        | None when
            consecutiveFailureCount > spec.Limit
            || not (ProviderFailureBudget.isValidRecord current.Budget.ConsecutiveFailureCount consecutiveFailureCount)
            ->
            Error InvalidTransition
        | None ->
            Ok
                { current with
                    Budget = { ConsecutiveFailureCount = consecutiveFailureCount }
                    BudgetSpec = Some spec
                    ObservedFailures =
                        { Key = key
                          Count = consecutiveFailureCount
                          Reason = reason }
                        :: current.ObservedFailures
                    Exhausted = consecutiveFailureCount = spec.Limit
                    LastTransitionWasSuccess = false }

    /// Terminal exhausted state.
    let applyExhausted (finalCount: int) (current: ProviderFailureProjection) =
        match current.BudgetSpec with
        | Some spec when
            current.Exhausted
            && finalCount = spec.Limit
            && finalCount = current.Budget.ConsecutiveFailureCount
            ->
            Ok current
        | _ -> Error InvalidTransition

    /// Success clears the budget streak.
    let recordSuccess (identity: FailedProviderAttemptIdentity) (current: ProviderFailureProjection) =
        let key = FailedProviderAttemptIdentity.dedupeKey identity

        let budget =
            if current.Exhausted then
                current.Budget
            else
                ProviderFailureBudget.recordSuccess current.Budget

        if isDifferentRun identity current then
            Error DifferentRun
        elif List.contains key current.SuccessKeys then
            Error AlreadyObserved
        elif current.ObservedFailures |> List.exists (fun observed -> observed.Key = key) then
            Error ConflictingObservation
        else
            Ok
                { current with
                    Budget = budget
                    SuccessKeys = key :: current.SuccessKeys
                    LastTransitionWasSuccess = true }

    let mayRetry (current: ProviderFailureProjection) =
        match current.BudgetSpec with
        | _ when current.Exhausted -> false
        | None ->
            current.Budget.ConsecutiveFailureCount = 0
            && List.isEmpty current.ObservedFailures
        | Some spec ->
            ProviderFailureBudgetSpec.isValid spec
            && spec.Limit > current.Budget.ConsecutiveFailureCount
