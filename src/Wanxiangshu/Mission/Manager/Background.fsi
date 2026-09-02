namespace Wanxiangshu.Mission.Manager

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// Vocabulary: Manager background settlement before idle/activation (rabbit §8.2).
///
/// Owns the Manager use of `TerminalPolicy.outstandingBackground` and the
/// business choice to send `HostJoinGuard` when children are still outstanding.
module ManagerBackground =

    /// One-shot result of ensuring background work is settled for this turn.
    /// Not durable state — a function result for this observation.
    [<RequireQualifiedAccess>]
    type BackgroundSettlement =
        /// No outstanding background work; caller may proceed.
        | Settled
        /// Background work still outstanding; JoinGuard was requested / deferred.
        | Deferred

    /// If Manager still has outstanding background work, request JoinGuard and
    /// defer; otherwise report Settled for this observation.
    val ensureSettled:
        sessionPort: ISessionHostPort ->
        eventPort: IEventObservationPort ->
        journal: AgentJournal option ->
        joinGuardNudges: HashSet<string> ->
        hasLivePty: (string -> bool) ->
        quiescence: ISessionQuiescenceGate ->
        context: ReconciledTurnContext ->
            Task<BackgroundSettlement>
