namespace Wanxiangshu.Interaction.Repair

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

module InteractionRepairWorkflow =
    val repairBloggerProtocol:
        host: IBloggerRuntimeHost ->
        quiescence: ISessionQuiescenceGate ->
        context: ReconciledTurnContext ->
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
            Task

    val repairMissingFinalReport:
        quiescence: ISessionQuiescenceGate ->
        context: ReconciledTurnContext ->
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
            Task

    val repairIncompleteInteraction:
        quiescence: ISessionQuiescenceGate ->
        context: ReconciledTurnContext ->
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
            Task
