namespace Wanxiangshu.Mission.Manager

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Manager terminal business story: handoff → background → idle labor.
module ManagerWorkflow =

    val observeIdle:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        nudgeSent: HashSet<string> ->
        hasLivePty: (string -> bool) ->
        quiescence: ISessionQuiescenceGate ->
        context: ReconciledTurnContext ->
            Task

    /// Observe one Manager-role turn. Manager-specific business branches stay here;
    /// non-Manager terminal semantics are delegated through the injected ordinary
    /// workflow rather than returned as a handled-bool program counter.
    val observe:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        nudgeSent: HashSet<string> ->
        joinGuardNudges: HashSet<string> ->
        hasLivePty: (string -> bool) ->
        quiescence: ISessionQuiescenceGate ->
        observeOrdinary: (ReconciledTurnContext -> Task) ->
        context: ReconciledTurnContext ->
            Task
