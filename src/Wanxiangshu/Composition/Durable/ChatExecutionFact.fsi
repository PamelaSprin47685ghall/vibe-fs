namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Session.ChatExecution

module ChatExecutionFact =
    val inline Accepted:
        payload:
            {| SchemaVersion: int
               Key: ChatExecutionKey
               Evidence: AcceptedChatExecutionEvidence |} ->
            AgentFact

    val inline ProviderStarted:
        payload:
            {| SchemaVersion: int
               Key: ChatExecutionKey
               Evidence: ProviderStartedEvidence |} ->
            AgentFact

    val inline Terminal:
        payload:
            {| SchemaVersion: int
               Key: ChatExecutionKey
               Evidence: ChatExecutionTerminalEvidence
               Disposition: ChatExecutionTerminalDisposition |} ->
            AgentFact
