namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Mission.Review
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Sole Application entry for a reconciled turn observation (rabbit §6.5).
///
/// Host hands a stable observation here; this module owns bounded-context fan-out:
/// SyncDelegate-owned → Reviewer → Manager → Ordinary. Host must not retain three
/// sequential `handled` bools for SyncDelegate / Reviewer / Manager.
module TurnWorkflow =

    /// Route one stable observation to SyncDelegate-owned / Reviewer / Manager /
    /// Ordinary. Ordinary falls through when Manager does not claim the turn.
    val observe:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        recoveryScope: IBloggerRuntimeHost ->
        syncDelegate: SyncDelegateRuntime option ->
        reviewerContinuationPort: ReviewerContinuationPort ->
        nudgeSent: HashSet<string> ->
        joinGuardNudges: HashSet<string> ->
        hasLivePty: (string -> bool) ->
        abortCause: AbortCause ->
        quiescence: ISessionQuiescenceGate ->
        context: ReconciledTurnContext ->
            Task
