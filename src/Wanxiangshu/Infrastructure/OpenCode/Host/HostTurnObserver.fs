namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process
open Wanxiangshu.Review
open Wanxiangshu.Session

/// Turn observation policy for one reconciled turn (STRENGTH / RECOVERY-FAMILY / TurnWorkflow).
module HostTurnObserver =

    let observe
        (recoveryTimerPort: ITimerPort)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (scope: PluginRuntimeScope)
        (reviewerContinuationPort: ReviewerContinuationPort)
        (context: ReconciledTurnContext)
        : Task =
        task {
            let turn = context.Turn

            // HOST-027: assistance is classified from the exact armed ProviderRun
            // before Strength, family recovery, SyncDelegate, fallback, or ordinary
            // abort handling can interpret the physical abort as business failure.
            match! scope.HandleAssistanceTurn context with
            | AssistanceTurnDisposition.Handled ->
                // HOST-027: if LoopSensor also armed before the physical abort
                // settled, the explicit collaboration request owns this abort.
                // Clear the competing process-local cause so it cannot leak into
                // the next attempt as a phantom loop-kill.
                scope.LoopSensor.ClearArmed turn.SessionId
                do! XWire.reconcileAttempt journal scope turn
                return ()
            | AssistanceTurnDisposition.ClaimedButUnresolved ->
                scope.LoopSensor.ClearArmed turn.SessionId
                do! XWire.reconcileAttempt journal scope turn

                eventPort.NotifyTerminal
                    turn.SessionId
                    (TerminalOutcome.Failed "assistance escalation could not continue")
                |> ignore

                return ()
            | AssistanceTurnDisposition.NotAssistance ->
                match turn.Outcome with
                | ReconcileProgram.TurnCompleted
                | ReconcileProgram.TurnFailed _
                | ReconcileProgram.TurnAborted _ -> scope.NeedHelpSensor.DropAttempt(turn.SessionId, turn.ProviderRun)
                | ReconcileProgram.TurnNeedsContinuation _
                | ReconcileProgram.TurnInProgress -> ()

            let strengthHandled =
                scope.Strength.StrengthReplicaRuntime
                |> Option.exists (fun runtime -> runtime.HandleTurn turn)

            if strengthHandled then
                // STRENGTH-004/011: Replica observations are leaf-local. They
                // only reconcile the request plan for cleanup; family recovery,
                // owner fallback, Companion, Review and ordinary TurnWorkflow
                // must never observe them.
                do! XWire.reconcileAttempt journal scope turn
                return ()
            else
                // STRENGTH-010: only primary (non-Replica) turns feed the
                // counterfactual predictor. Pending shadow/control labels
                // are target-bound inside the scope.
                scope.Strength.ObserveStrengthPrimary(
                    turn.SessionId,
                    turn.ProviderRun,
                    StrengthTurnEvidence.primarySymbol turn.Parts
                )

                // STRENGTH-007: consumption proof closes before any later
                // continuation can be admitted. This writer is independent
                // of rollout/fuse state because a provider may already have
                // consumed a durable Candidate.
                match strengthDurability with
                | None -> ()
                | Some durability ->
                    match! durability.LoadProjection() with
                    | Error error ->
                        let reason = "Strength promotion projection failed: " + error
                        scope.Strength.TripStrengthFuse reason
                        raise (InvalidOperationException reason)
                    | Ok projection ->
                        match StrengthLifecycle.reconcileEvent projection turn with
                        | None -> ()
                        | Some event ->
                            match! durability.Append event with
                            | Ok() -> ()
                            | Error error ->
                                let reason = "Strength promotion commit failed closed: " + error
                                scope.Strength.TripStrengthFuse reason
                                raise (InvalidOperationException reason)

                // RECOVERY-FAMILY: family recovery before business effects of a turn.
                let! recovery = scope.EnsureRecoveryDone turn.SessionId

                match recovery with
                | FamilyRecovery.FamilyBlocked _ ->
                    // Fail closed: definitive block → no business effects.
                    ()
                | FamilyRecovery.FamilyWaiting _
                | FamilyRecovery.FamilyReady _ ->
                    // Ready = permit-eligible; Waiting = incomplete (no permit) but not hard
                    // block. Bounded-context workflows still observe the terminal.

                    match turn.Outcome with
                    | ReconcileProgram.TurnFailed _
                    | ReconcileProgram.TurnAborted _ ->
                        scope.ArmRecovery turn.SessionId

                        // CTX-006 step 1 (Y half): a failed Blogger turn opens a one-shot
                        // recovery opportunity on the Companion that owns it. Opportunity
                        // = pending material waiter Task; material Offer consumes it once.
                        for KeyValue(_, companion) in scope.Sessions.Companions do
                            match companion.BloggerSession with
                            | Some bloggerId when bloggerId = turn.SessionId ->
                                companion.StartRecoveryOpportunity() |> ignore
                            | _ -> ()
                    | ReconcileProgram.TurnCompleted
                    | ReconcileProgram.TurnNeedsContinuation _
                    | ReconcileProgram.TurnInProgress -> ()


                    do! XWire.reconcileAttempt journal scope turn
                    TurnRuntimePreparation.prepare scope.DisposeExecutorRuntime turn

                    // Sole Application turn entry (rabbit §6.5 / §18): Host no longer
                    // multiplexes SyncDelegate / Reviewer / Manager handled-bools.
                    do!
                        TurnWorkflow.observe
                            recoveryTimerPort
                            Pty.abortParent
                            sessionPort
                            eventPort
                            journal
                            scope.SyncDelegateRuntime
                            reviewerContinuationPort
                            scope.Sessions.NudgeSent
                            scope.Sessions.JoinGuardNudges
                            scope.HasLivePty
                            scope.Sessions.AbortedSessions
                            (Some scope.LoopSensor)
                            scope.Sessions.Quiescence
                            context
        }
