namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Strength.Persistence

/// Turn observation policy for one reconciled turn (STRENGTH / RECOVERY-FAMILY / TurnWorkflow).
module HostTurnObserver =

    val observe:
        sessionPort: ISessionHostPort ->
        rootWorkspace: IRootWorkspaceReader ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        strengthDurability: StrengthDurabilityPort option ->
        scope: PluginRuntimeScope ->
        reviewerContinuationPort: ReviewerContinuationPort ->
        context: ReconciledTurnContext ->
            Task
