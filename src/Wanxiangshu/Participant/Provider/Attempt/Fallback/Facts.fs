namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity

/// Durable fallback facts owned by the provider-attempt fallback boundary.
type FallbackFactCases =
    /// One confirmed failed attempt advanced the provider cursor.
    | FallbackCursorAdvanced of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           ProviderRun: ProviderRunIdentity
           PreviousOffset: byte
           NextOffset: byte
           ConsecutiveFailureCount: int
           Reason: string |}
    /// The automatic recovery budget is spent.
    | FallbackExhausted of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           FinalConsecutiveFailureCount: int
           FinalOffset: byte |}
    /// A confirmed successful business-main attempt clears the failure budget and
    /// closes A′/B′ to the ordinary slot on the same side.
    | FallbackSucceeded of
        {| SessionId: SessionId
           LogicalRunId: LogicalRunId
           AuthorityRootUserMessageId: AuthorityRootUserMessageId
           ProviderRun: ProviderRunIdentity |}
