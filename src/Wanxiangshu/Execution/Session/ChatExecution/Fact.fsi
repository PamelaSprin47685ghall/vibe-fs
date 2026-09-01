namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Composition.Durable.Fact

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
