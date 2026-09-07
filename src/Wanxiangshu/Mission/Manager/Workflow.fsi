namespace Wanxiangshu.Mission.Manager

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Manager current-action guidance and retired-idle continuation ownership.
module ManagerWorkflow =

    val observeIdle:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        journal: AgentJournal option ->
        context: ReconciledTurnContext ->
            Task

    /// Observe one Manager-role turn. Manager-specific business branches stay here;
    /// non-Manager terminal semantics are delegated through the injected ordinary
    /// workflow rather than returned as a handled-bool program counter.
    val observe:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        journal: AgentJournal option ->
        observeOrdinary: (ReconciledTurnContext -> Task) ->
        context: ReconciledTurnContext ->
            Task

    val ensureManagerRoadOpened:
        journal: AgentJournal option ->
        workspaceDirectory: string option ->
        sessionIdTextOpt: string option ->
        providerRunIdOpt: ProviderRunIdentity option ->
            Task<unit>

    val maybeDeliverLoop:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        journal: AgentJournal option ->
        workspaceDirectory: string option ->
        sessionIdTextOpt: string option ->
            Task<unit>

    val continueAfterRetiredAttempt:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        journal: AgentJournal option ->
        workspaceDirectory: string option ->
        stopRetiredAttempt: (SessionId -> Task<unit>) ->
        sessionId: SessionId ->
            Task<unit>
