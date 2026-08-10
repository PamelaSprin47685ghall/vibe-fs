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
                // TODO(TurnWorkflow): ManagerWorkflow.tryObserve still returns a
                // handled-bool for Ordinary fallthrough (ManagerStarted /
                // ConflictPending / non-completed outcomes). Collapse when Manager
                // owns every Manager-role observation without bool multiplexing.
                let! managerHandled =
                    ManagerWorkflow.tryObserve
                        sessionPort
                        eventPort
                        journal
                        nudgeSent
                        joinGuardNudges
                        hasLivePty
                        abortedSessions
                        quiescence
                        context

                if not managerHandled then
                    do!
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
                            context
            | _ ->
                do!
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
                        context
        }
        :> Task
