namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity

type ProviderFailureBudgetSpec = { Version: int; Limit: int }

module ProviderFailureBudgetSpec =
    val isValid: spec: ProviderFailureBudgetSpec -> bool

type ProviderFailureFactCases =
    | FailureRecorded of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           ProviderRun: ProviderRunIdentity
           BudgetSpec: ProviderFailureBudgetSpec
           ConsecutiveFailureCount: int
           Reason: string |}
    | RetryExhausted of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           FinalConsecutiveFailureCount: int |}
    | SuccessRecorded of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           ProviderRun: ProviderRunIdentity |}
