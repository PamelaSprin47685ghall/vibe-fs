namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

module ProviderFailureFact =
    val inline FailureRecorded:
        payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity
               ConsecutiveFailureCount: int
               Reason: string |} ->
            AgentFact

    val inline RetryExhausted:
        payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               FinalConsecutiveFailureCount: int |} ->
            AgentFact

    val inline SuccessRecorded:
        payload:
            {| SessionId: SessionId
               LogicalRunId: LogicalRunId
               AuthorityRootUserMessageId: AuthorityRootUserMessageId
               ProviderRun: ProviderRunIdentity |} ->
            AgentFact
