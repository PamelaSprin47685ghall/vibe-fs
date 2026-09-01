namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

module FallbackFact =
    val inline FallbackCursorAdvanced:
        payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity
               PreviousOffset: byte
               NextOffset: byte
               ConsecutiveFailureCount: int
               Reason: string |} ->
            AgentFact

    val inline FallbackExhausted:
        payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               FinalConsecutiveFailureCount: int
               FinalOffset: byte |} ->
            AgentFact

    val inline FallbackSucceeded:
        payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity |} ->
            AgentFact
