namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Persistence.Journal

/// Turn observation policy for one reconciled turn (STRENGTH / RECOVERY-FAMILY / TurnWorkflow).
module HostTurnObserver =

    val observe:
        observeTurnWorkflow: (AbortCause -> ReconciledTurnContext -> Task) ->
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        handlePreTurn: (ReconciledTurn -> Task<bool>) option ->
        observePrimaryTurn: (ReconciledTurn -> Task<unit>) option ->
        scope: PluginRuntimeScope ->
        context: ReconciledTurnContext ->
            Task
