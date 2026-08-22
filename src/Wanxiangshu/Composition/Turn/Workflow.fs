namespace Wanxiangshu.Composition.Turn

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.Journal

/// Sole Application entry for a reconciled turn observation (rabbit §6.5).
///
/// Host hands a stable observation here; this module owns bounded-context fan-out:
/// SyncDelegate-owned → Reviewer → Manager → Ordinary. Host must not retain three
/// sequential `handled` bools for SyncDelegate / Reviewer / Manager.
module TurnWorkflow =

    /// Route one stable observation to SyncDelegate-owned / Reviewer / Manager /
    /// Ordinary. Ordinary falls through when Manager does not claim the turn.
    let observe
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (recoveryScope: IBloggerRuntimeHost)
        (armRecovery: SessionId -> unit)
        (syncDelegate: SyncDelegateRuntime option)
        (reviewerContinuationPort: ReviewerContinuationPort)
        (nudgeSent: HashSet<string>)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (abortCause: AbortCause)
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
                    | Some Role.Reviewer, Some ReconcileProgram.TurnUnknown, _ -> do! observeIdleOrdinary context
                    | Some Role.Reviewer, _, _ -> do! observeIdleOrdinary context
                    | _ -> do! observeIdleOrdinary context
                }

            let observeOrdinary current =
                OrdinaryTurnWorkflow.observe
                    sessionPort
                    eventPort
                    journal
                    recoveryScope
                    armRecovery
                    joinGuardNudges
                    hasLivePty
                    abortCause
                    quiescence
                    current

            let observeObservation () : Task =
                task {
                    // Linked-child prompt authority is Application ownership: establish it
                    // once from durable linkage before bounded-context workflows consume the fact.
                    let! _ = ChildPromptAuthority.ensureForLinkedChild journal turn

                    match turn.Role, turn.Observation, turn.Outcome with
                    | Some Role.Reviewer, Some ReconcileProgram.TurnUnknown, _ -> do! observeOrdinary context
                    | Some Role.Reviewer, _, ReconcileProgram.TurnCompleted ->
                        do!
                            ReviewerWorkflow.observe
                                reviewerContinuationPort
                                eventPort
                                journal
                                turn
                                (SessionId.value turn.SessionId)
                    | Some Role.Reviewer, _, _ -> do! observeOrdinary context
                    | Some Role.Manager, _, _ ->
                        do!
                            ManagerWorkflow.observe
                                sessionPort
                                eventPort
                                journal
                                nudgeSent
                                joinGuardNudges
                                hasLivePty
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
