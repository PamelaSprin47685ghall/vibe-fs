namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Persistence.Journal
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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

    let private sendJoinGuard
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (turn: ReconciledTurn)
        =
        task {
            match! HostJoinGuard.nudge sessionPort journal joinGuardNudges turn.SessionId turn.Directory with
            | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed reason)
                |> ignore
            | _ -> ()
        }
        :> Task

    /// If Manager still has outstanding background work, request JoinGuard and
    /// defer; otherwise report Settled for this observation.
    let ensureSettled
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (turn: ReconciledTurn)
        : Task<BackgroundSettlement> =
        task {
            if TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId then
                do! sendJoinGuard sessionPort eventPort journal joinGuardNudges turn
                return BackgroundSettlement.Deferred
            else
                return BackgroundSettlement.Settled
        }
