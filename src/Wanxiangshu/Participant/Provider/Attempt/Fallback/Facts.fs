namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity

type ProviderFailureBudgetSpec = { Version: int; Limit: int }

module ProviderFailureBudgetSpec =
    let isValid (spec: ProviderFailureBudgetSpec) = spec.Version = 1 && spec.Limit > 0

/// Durable provider failure facts owned by the provider-attempt failure boundary.
type ProviderFailureFactCases =
    /// One confirmed failed attempt recorded in the provider failure budget.
    | FailureRecorded of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           ProviderRun: ProviderRunIdentity
           BudgetSpec: ProviderFailureBudgetSpec
           ConsecutiveFailureCount: int
           Reason: string |}
    /// The automatic recovery budget is spent.
    | RetryExhausted of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           FinalConsecutiveFailureCount: int |}
    /// A confirmed successful business-main attempt clears the failure budget.
    | SuccessRecorded of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           ProviderRun: ProviderRunIdentity |}
