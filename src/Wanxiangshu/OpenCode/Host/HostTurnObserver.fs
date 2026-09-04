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

    let private failStrengthProjection (scope: PluginRuntimeScope) (error: string) =
        let reason = "Strength promotion projection failed: " + error
        scope.Strength.TripStrengthFuse reason
        raise (InvalidOperationException reason)

    let private failStrengthStorage (scope: PluginRuntimeScope) (error: string) =
        let reason = "Strength promotion commit storage failure: " + error
        scope.Strength.TripStrengthFuse reason
        raise (InvalidOperationException reason)

    let private applyStrengthAppend (scope: PluginRuntimeScope) (result: StrengthDurableAppend) =
        match result with
        | StrengthDurableAppend.Applied -> ()
        | StrengthDurableAppend.SemanticRejected error ->
            // DURABLE-EVENTS-021: cut/reset keeps the next process recoverable,
            // but the process that produced the rejected live fact is no longer
            // trustworthy. Never continue Strength/turn effects in this process.
            Diagnostic.fatal "strength-semantic-cut" [ "result", error ]
        | StrengthDurableAppend.StorageFailed error -> failStrengthStorage scope error

    let private commitStrengthEvent
        (durability: StrengthDurabilityPort)
        (scope: PluginRuntimeScope)
        (turn: ReconciledTurn)
        (projection: StrengthProjection)
        : Task =
        task {
            match StrengthLifecycle.reconcileEvent projection turn with
            | None -> return ()
            | Some event ->
                let! appendResult = durability.Append event
                return applyStrengthAppend scope appendResult
        }

    let private loadAndCommitStrength
        (durability: StrengthDurabilityPort)
        (scope: PluginRuntimeScope)
        (turn: ReconciledTurn)
        : Task =
        task {
            match! durability.LoadProjection() with
            | Error error -> return failStrengthProjection scope error
            | Ok projection -> return! commitStrengthEvent durability scope turn projection
        }

    let private observeStrengthDurability
        (strengthDurability: StrengthDurabilityPort option)
        (scope: PluginRuntimeScope)
        (turn: ReconciledTurn)
        : Task =
        task {
            match strengthDurability with
            | None -> return ()
            | Some durability -> return! loadAndCommitStrength durability scope turn
        }

    let private isDurableFissionOwner (journal: AgentJournal option) (sessionId: SessionId) =
        journal
        |> Option.exists (fun durable ->
            FissionProjection.tryActiveForOwner sessionId (AgentJournal.snapshot durable).AgentProjections.Fission
            |> Option.isSome)

    let private isFissionOwnerSession (journal: AgentJournal option) (sessionId: SessionId) =
        FissionRuntime.isSilentInterrupt sessionId
        || isDurableFissionOwner journal sessionId

    let private hasBloggerToolEvidence (parts: MessagePart array) =
        parts
        |> Array.exists (function
            | MessagePart.ToolCall _
            | MessagePart.ToolResult _ -> true
            | _ -> false)

    let private needsBloggerIdleProtocolRepair (context: ReconciledTurnContext) =
        context.Quiescence.IsSome
        && context.Turn.Role = Some Role.Blogger
        && not (hasBloggerToolEvidence context.Turn.Parts)
        && match context.Turn.Outcome with
           | ReconcileProgram.TurnFailed _
           | ReconcileProgram.TurnAborted _ -> false
           | ReconcileProgram.TurnCompleted
           | ReconcileProgram.TurnInProgress
           | ReconcileProgram.TurnNeedsContinuation _ -> true

    /// Host boundary consumes the guard's one-shot armed anomaly. Consumption
    /// also schedules the guard-owned continuation at this existing reconcile point.
    let private abortCauseOfTurn (scope: PluginRuntimeScope) (context: ReconciledTurnContext) : AbortCause =
        match context.Turn.Outcome with
        | ReconcileProgram.TurnAborted _ ->
            scope.LoopSensor.ConsumeAbortCause(context.Turn.SessionId, context.Turn.Directory)
        | _ -> AbortCause.External

    let private observeApplicationTurn
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (reviewerContinuationPort: ReviewerContinuationPort)
        (abortCause: AbortCause)
        (context: ReconciledTurnContext)
        : Task =
        task {
            if needsBloggerIdleProtocolRepair context then
                // A prose-only Blogger terminal has no tool-loop request after it,
                // so provider transform cannot own recovery. The idle wake is the
                // causal boundary that can still send exact-one nudge / AABB.
                return!
                    InteractionRepairWorkflow.repairBloggerProtocol
                        scope.BloggerRuntimeHost
                        scope.Sessions.Quiescence
                        context
                        sessionPort
                        rootWorkspace
                        eventPort
                        journal
            else
                // Sole Application turn entry (rabbit §6.5 / §18): Host no longer
                // multiplexes SyncDelegate / Reviewer / Manager handled-bools.
                do!
                    TurnWorkflow.observe
                        sessionPort
                        rootWorkspace
                        eventPort
                        journal
                        scope.BloggerRuntimeHost
                        scope.SyncDelegateRuntime
                        reviewerContinuationPort
                        scope.Sessions.NudgeSent
                        scope.Sessions.JoinGuardNudges
                        (fun s -> scope.HasLivePty s)
                        abortCause
                        scope.Sessions.Quiescence
                        context
        }

    let private observeFamilyReady
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (reviewerContinuationPort: ReviewerContinuationPort)
        (abortCause: AbortCause)
        (context: ReconciledTurnContext)
        : Task =
        task {
            let turn = context.Turn
            let isFissionOwner = isFissionOwnerSession journal turn.SessionId
            do! XWire.reconcileAttempt journal scope turn
            do! TurnRuntimePreparation.prepare scope.DisposeExecutorRuntime turn

            let! fissionHandled =
                FissionHost.observeLaneTurn
                    sessionPort
                    rootWorkspace
                    eventPort
                    journal
                    scope.Sessions.JoinGuardNudges
                    scope.Sessions.Quiescence
                    context.Quiescence
                    abortCause
                    turn

            if not isFissionOwner && not fissionHandled then
                do!
                    observeApplicationTurn
                        sessionPort
                        rootWorkspace
                        eventPort
                        journal
                        scope
                        reviewerContinuationPort
                        abortCause
                        context
        }

    let private observeAfterStrength
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
        (reviewerContinuationPort: ReviewerContinuationPort)
        (abortCause: AbortCause)
        (context: ReconciledTurnContext)
        : Task =
        task {
            let! recovery = scope.EnsureRecoveryDone context.Turn.SessionId

            match recovery with
            | FamilyRecovery.FamilyBlocked _ -> return ()
            | FamilyRecovery.FamilyWaiting _
            | FamilyRecovery.FamilyReady _ ->
                return!
                    observeFamilyReady
                        sessionPort
                        rootWorkspace
                        eventPort
                        journal
                        scope
                        reviewerContinuationPort
                        abortCause
                        context
        }

    let private observeBusinessTurn
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (scope: PluginRuntimeScope)
        (reviewerContinuationPort: ReviewerContinuationPort)
        (context: ReconciledTurnContext)
        : Task =
        let closeDryRunAtPrimaryTerminal (turn: ReconciledTurn) =
            match scope.Strength.StrengthReplicaRuntime with
            | Some runtime -> runtime.CloseDryRunAtTargetTerminal turn
            | None -> AsyncSupport.completedTask ()

        task {
            let turn = context.Turn
            let abortCause = abortCauseOfTurn scope context

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
                // SPEC-INV-013: an observation-only Replica that did not finish
                // first is closed by the exact owner target run terminal. This is
                // a causal horizon boundary, not a wall-clock timeout.
                do! closeDryRunAtPrimaryTerminal turn

                // STRENGTH-010: only primary (non-Replica) turns feed the
                // counterfactual predictor. Pending shadow/control labels
                // are target-bound inside the scope.
                let _ =
                    scope.Strength.ObserveStrengthPrimary(
                        turn.SessionId,
                        turn.ProviderRun,
                        StrengthTurnEvidence.primarySymbol turn.Parts
                    )

                // STRENGTH-007: consumption proof closes before any later
                // continuation can be admitted. This writer is independent
                // of rollout/fuse state because a provider may already have
                // consumed a durable Candidate.
                do! observeStrengthDurability strengthDurability scope turn

                return!
                    observeAfterStrength
                        sessionPort
                        rootWorkspace
                        eventPort
                        journal
                        scope
                        reviewerContinuationPort
                        abortCause
                        context
        }

    let observe
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (scope: PluginRuntimeScope)
        (reviewerContinuationPort: ReviewerContinuationPort)
        (context: ReconciledTurnContext)
        : Task =
        let turn = context.Turn

        if ExplicitResumeSuppression.isPhysicalMaterial turn.SessionId turn.PhysicalUserMessageId then
            // CRASH-018: the /continue provider turn is disclosure-only. Reconcile
            // may observe it for transport bookkeeping, but Wanxiangshu must not
            // derive Strength, recovery, fallback, Companion, review, manager-idle
            // or interaction-repair effects from this physical material.
            Task.FromResult(()) :> Task
        else
            observeBusinessTurn
                sessionPort
                rootWorkspace
                eventPort
                journal
                strengthDurability
                scope
                reviewerContinuationPort
                context
