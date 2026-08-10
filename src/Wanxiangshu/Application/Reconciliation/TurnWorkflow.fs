namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Sole Application entry for a reconciled turn observation (rabbit §6.5).
///
/// Host hands a stable observation here; this module owns bounded-context fan-out:
/// SyncDelegate-owned → Reviewer → Manager → Ordinary. Host must not retain three
/// sequential `handled` bools for SyncDelegate / Reviewer / Manager.
module TurnWorkflow =

    /// Route one stable observation to SyncDelegate-owned / Reviewer / Manager /
    /// Ordinary. Ordinary falls through when Manager does not claim the turn.
    let observe
        (timerPort: ITimerPort)
        (abortParent: string -> unit)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (syncDelegate: SyncDelegateRuntime option)
        (nudgeSent: HashSet<string>)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortedSessions: HashSet<string>)
        (loopSensor: LoopSensor option)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        : Task =
        task {
            let turn = context.Turn

            // SyncDelegate path stays first and exclusive when it claims the turn
            // (Inspector/Coder dedicated sessions). Do not break this ownership.
            let! syncDelegateHandled =
                match syncDelegate with
                | Some runtime -> runtime.HandleTurn(turn, context.Quiescence)
                | None -> Task.FromResult false

            if syncDelegateHandled then
                return ()

            let observeOrdinary current =
                OrdinaryTurnWorkflow.observe
                    timerPort
                    abortParent
                    sessionPort
                    eventPort
                    journal
                    joinGuardNudges
                    hasLivePty
                    abortedSessions
                    loopSensor
                    quiescence
                    current

            match turn.Role with
            | Some Role.Reviewer ->
                do!
                    ReviewerWorkflow.observe
                        sessionPort
                        eventPort
                        journal
                        nudgeSent
                        turn
                        (SessionId.value turn.SessionId)
            | Some Role.Manager ->
                do!
                    ManagerWorkflow.observe
                        sessionPort
                        eventPort
                        journal
                        nudgeSent
                        joinGuardNudges
                        hasLivePty
                        abortedSessions
                        quiescence
                        observeOrdinary
                        context
            | _ -> do! observeOrdinary context
        }
        :> Task
