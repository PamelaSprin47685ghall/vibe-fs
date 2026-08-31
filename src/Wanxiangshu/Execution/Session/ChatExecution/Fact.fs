namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Composition.Durable.Fact

module ChatExecutionFact =
    let inline Accepted payload =
        AgentFact.ChatExecution(ChatExecutionFactCases.Accepted payload)

    let inline ProviderStarted payload =
        AgentFact.ChatExecution(ChatExecutionFactCases.ProviderStarted payload)

    let inline Terminal payload =
        AgentFact.ChatExecution(ChatExecutionFactCases.Terminal payload)
