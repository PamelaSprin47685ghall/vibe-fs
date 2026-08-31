namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Durable.Fact

/// Prompt fact constructors — bridge from Authority-owned PromptFactCases
/// into the Composition-owned AgentFact outer routing union.
module PromptFact =
    let inline PluginPromptClaimed payload =
        AgentFact.Prompt(PromptFactCases.PluginPromptClaimed payload)

    let inline PluginPromptSubmitted payload =
        AgentFact.Prompt(PromptFactCases.PluginPromptSubmitted payload)

    let inline PluginPromptPhysicalAccepted payload =
        AgentFact.Prompt(PromptFactCases.PluginPromptPhysicalAccepted payload)

    let inline PluginPromptAbandoned payload =
        AgentFact.Prompt(PromptFactCases.PluginPromptAbandoned payload)

    let AuthorityRootAccepted (payload: AuthorityRootAcceptedPayload) =
        AgentFact.Prompt(PromptFactCases.AuthorityRootAccepted payload)
