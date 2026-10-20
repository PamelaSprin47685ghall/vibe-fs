namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module DelegationJournalAdapter =
    /// delegation-029: durable composition wraps delegation fact cases into the outer routing union.
    val fromAgentJournal: journal: AgentJournal -> AgentJournalPort

module TaskDeliveryJournal =
    val tryFind:
        journal: AgentJournal ->
        sessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
            Wanxiangshu.Host.TaskDeliveryWitness option

    val commit:
        journal: AgentJournal ->
        witness: Wanxiangshu.Host.TaskDeliveryWitness ->
            System.Threading.Tasks.Task<Result<unit, string>>

    val commitChild:
        journal: AgentJournal ->
        sessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        physicalUserMessageId: Wanxiangshu.Foundation.Identity.PhysicalUserMessageId ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId ->
        formalText: string ->
        declaration: string ->
        view: string ->
            System.Threading.Tasks.Task<
                Result<Wanxiangshu.Host.TaskDeliveryWitness * Wanxiangshu.Foundation.Role, string>
             >
