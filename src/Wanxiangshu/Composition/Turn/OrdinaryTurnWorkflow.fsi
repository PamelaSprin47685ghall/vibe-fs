namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Ordinary turn observation policy (INTERACTION-REPAIR / FALLBACK / TERMINAL-REPORT).
module OrdinaryTurnWorkflow =

    /// Revisit a previously delivered turn only for work whose authority comes
    /// from a fresh idle observation. Terminal plumbing remains first-delivery only.
    val observeIdle:
        quiescence: ISessionQuiescenceGate ->
        sessionPort: ISessionHostPort ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        context: ReconciledTurnContext ->
            Task

    val observe:
        sessionPort: ISessionHostPort ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        recoveryScope: IBloggerRuntimeHost ->
        joinGuardNudges: HashSet<string> ->
        hasLivePty: (string -> bool) ->
        abortCause: AbortCause ->
        quiescence: ISessionQuiescenceGate ->
        context: ReconciledTurnContext ->
            Task
