namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity

/// Durable provider failure facts owned by the provider-attempt failure boundary.
type ProviderFailureFactCases =
    /// One confirmed failed attempt recorded in the provider failure budget.
    | FailureRecorded of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           ProviderRun: ProviderRunIdentity
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
