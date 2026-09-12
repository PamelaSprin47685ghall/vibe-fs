namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Session.ChatExecution

module ChatExecutionFact =
    let inline Accepted payload =
        AgentFact.ChatExecution(ChatExecutionFactCases.Accepted payload)

    let inline ProviderStarted payload =
        AgentFact.ChatExecution(ChatExecutionFactCases.ProviderStarted payload)

    let inline Terminal payload =
        AgentFact.ChatExecution(ChatExecutionFactCases.Terminal payload)
