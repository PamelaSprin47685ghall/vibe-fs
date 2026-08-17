namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open System.Threading.Tasks
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
open Wanxiangshu.Mission.Manager
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
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review
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
        (reviewerContinuationPort: ReviewerContinuationPort)
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

            let observeIdleOrdinary current =
                OrdinaryTurnWorkflow.observeIdle quiescence sessionPort eventPort journal current

            let observeIdleDelivery () : Task =
                task {
                    match turn.Role, turn.Observation, turn.Outcome with
                    | Some Role.Manager, None, ReconcileProgram.TurnCompleted ->
                        do!
                            ManagerWorkflow.observeIdle
                                sessionPort
                                eventPort
                                journal
                                nudgeSent
                                hasLivePty
                                quiescence
                                context
                    | Some Role.Reviewer, _, ReconcileProgram.TurnCompleted ->
                        // A completed reviewer may have delivered its terminal observation on a
                        // non-idle wake before the just-recorded verdict/challenge facts
                        // were visible. Re-evaluate the durable ReviewerEvidence on the
                        // fresh idle capability; Host nudge keys and terminal provider-run
                        // dedupe keep the physical effects exactly-once.
                        do!
                            ReviewerWorkflow.observe
                                reviewerContinuationPort
                                eventPort
                                journal
                                turn
                                (SessionId.value turn.SessionId)
                    | Some Role.Reviewer, _, _ -> do! observeIdleOrdinary context
                    | _ -> do! observeIdleOrdinary context
                }

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

            let observeObservation () : Task =
                task {
                    // Linked-child prompt authority is Application ownership: establish it
                    // once from durable linkage before bounded-context workflows consume the fact.
                    let! _ = ChildPromptAuthority.ensureForLinkedChild journal turn

                    match turn.Role, turn.Outcome with
                    | Some Role.Reviewer, ReconcileProgram.TurnCompleted ->
                        do!
                            ReviewerWorkflow.observe
                                reviewerContinuationPort
                                eventPort
                                journal
                                turn
                                (SessionId.value turn.SessionId)
                    | Some Role.Reviewer, _ -> do! observeOrdinary context
                    | Some Role.Manager, _ ->
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

            match context.Delivery with
            | ReconciledTurnDelivery.IdleRevisit -> return! observeIdleDelivery ()
            | ReconciledTurnDelivery.Observation -> return! observeObservation ()
        }
        :> Task
