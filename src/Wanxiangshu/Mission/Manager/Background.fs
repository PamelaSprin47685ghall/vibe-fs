namespace Wanxiangshu.Mission.Manager

open Wanxiangshu.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

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
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        =
        task {
            let turn = context.Turn

            match context.Quiescence with
            | None -> ()
            | Some permit ->
                match!
                    HostJoinGuard.nudge
                        sessionPort
                        journal
                        joinGuardNudges
                        (fun () -> quiescence.TryConsume permit)
                        (fun () -> quiescence.TryRelease permit)
                        turn.SessionId
                        turn.ProviderRun
                        turn.Directory
                with
                | HostJoinGuard.JoinGuardNudgeOutcome.Failed reason ->
                    eventPort.NotifyTerminal
                        turn.SessionId
                        (Wanxiangshu.OpenCode.TerminalOutcome.Failed(
                            Wanxiangshu.OpenCode.TerminalStop.forAuthority turn.AuthorityRootUserMessageId reason
                        ))
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
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        : Task<BackgroundSettlement> =
        task {
            let turn = context.Turn

            if TerminalPolicy.outstandingBackground journal hasLivePty turn.Role turn.SessionId then
                do! sendJoinGuard sessionPort eventPort journal joinGuardNudges quiescence context
                return BackgroundSettlement.Deferred
            else
                return BackgroundSettlement.Settled
        }
