namespace Wanxiangshu.Interaction.Repair

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module InteractionRepairWorkflow =
    val repairBloggerProtocol:
        host: IBloggerRuntimeHost ->
        quiescence: SessionQuiescenceGate ->
        context: ReconciledTurnContext ->
        sessionPort: ISessionHostPort ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
            Task

    val repairMissingFinalReport:
        quiescence: SessionQuiescenceGate ->
        context: ReconciledTurnContext ->
        sessionPort: ISessionHostPort ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
            Task

    val repairIncompleteInteraction:
        quiescence: SessionQuiescenceGate ->
        context: ReconciledTurnContext ->
        sessionPort: ISessionHostPort ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
            Task
