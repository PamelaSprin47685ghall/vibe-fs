namespace Wanxiangshu.OpenCode

open System
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
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Host
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Process
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Mission.Review
open Wanxiangshu.Execution.Fission.OpenCode
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

/// Turn observation policy for one reconciled turn (STRENGTH / RECOVERY-FAMILY / TurnWorkflow).
module HostTurnObserver =

    let private armRecoveryIfEligible (scope: PluginRuntimeScope) isFissionOwner (turn: ReconciledTurn) =
        match isFissionOwner, turn.Outcome with
        | false, (ReconcileProgram.TurnFailed _ | ReconcileProgram.TurnAborted _) ->
            scope.ArmRecovery turn.SessionId

            for KeyValue(_, companion) in scope.Sessions.Companions do
                match companion.BloggerSession with
                | Some bloggerId when bloggerId = turn.SessionId -> companion.StartRecoveryOpportunity() |> ignore
                | _ -> ()
        | _ -> ()

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
                            | StrengthDurableAppend.Applied -> ()
                            | StrengthDurableAppend.SemanticRejected error ->
                                // Durable cut-tail already isolated this one event. Do not
                                // fuse Strength or poison future attempts.
                                Diagnostic.emit "strength-semantic-cut" [ "result", error ]
                            | StrengthDurableAppend.StorageFailed error ->
                                let reason = "Strength promotion commit storage failure: " + error
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

                    let durableFissionReplacement =
                        journal
                        |> Option.exists (fun durable ->
                            FissionProjection.tryActiveForOwner
                                turn.SessionId
                                (AgentJournal.snapshot durable).AgentProjections.Fission
                            |> Option.isSome)

                    let isFissionOwner =
                        FissionRuntime.isSilentInterrupt turn.SessionId || durableFissionReplacement

                    armRecoveryIfEligible scope isFissionOwner turn
                    do! XWire.reconcileAttempt journal scope turn
                    TurnRuntimePreparation.prepare scope.DisposeExecutorRuntime turn

                    let! fissionHandled =
                        FissionHost.observeLaneTurn sessionPort eventPort journal scope.Sessions.JoinGuardNudges turn

                    if not isFissionOwner && not fissionHandled then
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
