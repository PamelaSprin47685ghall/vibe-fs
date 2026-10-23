namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

open Wanxiangshu.Foundation.HostSession

open Wanxiangshu.Foundation.TerminalSignal

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Persistence.Journal

/// Turn observation policy for one reconciled turn (RECOVERY-FAMILY / TurnWorkflow).
module HostTurnObserver =

    val observe:
        observeTurnWorkflow: (ReconciledTurnContext -> Task) ->
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        scope: PluginRuntimeScope ->

        context: ReconciledTurnContext ->
            Task
