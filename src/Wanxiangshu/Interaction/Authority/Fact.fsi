namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

module PromptFact =
    val inline PluginPromptClaimed:
        payload:
            {| PromptKey: PromptKey
               SessionId: SessionId
               ContinuationKind: string
               LogicalRunId: LogicalRunId option
               AuthorityRootUserMessageId: AuthorityRootUserMessageId option
               EffectiveAgent: string option
               IdentitySeed: PromptIdentitySeed
               PayloadDigest: string |} ->
            AgentFact

    val inline PluginPromptSubmitted:
        payload:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Receipt: TransportReceipt |} ->
            AgentFact

    val inline PluginPromptPhysicalAccepted:
        payload:
            {| PromptKey: PromptKey
               SessionId: SessionId
               PhysicalUserMessageId: PhysicalUserMessageId |} ->
            AgentFact

    val inline PluginPromptAbandoned:
        payload:
            {| PromptKey: PromptKey
               SessionId: SessionId
               Reason: PromptAbandonReason |} ->
            AgentFact

    val AuthorityRootAccepted: payload: AuthorityRootAcceptedPayload -> AgentFact
