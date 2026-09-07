namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity

type ProviderFailureFactCases =
    | FailureRecorded of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           ProviderRun: ProviderRunIdentity
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
