namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt.Fallback

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
