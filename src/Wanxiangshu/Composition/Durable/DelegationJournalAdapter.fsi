namespace Wanxiangshu.Composition.Durable

open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module DelegationJournalAdapter =
    /// delegation-029: durable composition wraps delegation fact cases into the outer routing union.
    val fromAgentJournal: journal: AgentJournal -> AgentJournalPort

module TaskDeliveryJournal =
    /// 精确围栏读取：按完整围栏取见证，围栏其余字段不一致即拒绝。
    val tryFindByFence:
        journal: AgentJournal ->
        fence: Wanxiangshu.Host.TaskDeliveryFence ->
            Wanxiangshu.Host.TaskDeliveryWitness option

    /// 围栏子集读取：只掌握部分围栏字段时使用；材料不一致时拒绝。
    val tryFindByFenceFields:
        journal: AgentJournal ->
        sessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
        toolCallId: Wanxiangshu.Foundation.Identity.ToolCallId option ->
        authorityRoot: Wanxiangshu.Foundation.Identity.AuthorityRootUserMessageId option ->
        physicalUserMessageId: Wanxiangshu.Foundation.Identity.PhysicalUserMessageId option ->
        logicalRunId: Wanxiangshu.Foundation.Identity.LogicalRunId option ->
        task: Wanxiangshu.Host.TaskDeliveryTask option ->
            Wanxiangshu.Host.TaskDeliveryWitness option

    val tryFind:
        journal: AgentJournal ->
        sessionId: Wanxiangshu.Foundation.Identity.SessionId ->
        providerRun: Wanxiangshu.Foundation.Identity.ProviderRunIdentity ->
            Wanxiangshu.Host.TaskDeliveryWitness option

    val commit:
        journal: AgentJournal ->
        witness: Wanxiangshu.Host.TaskDeliveryWitness ->
            System.Threading.Tasks.Task<Result<unit, string>>
