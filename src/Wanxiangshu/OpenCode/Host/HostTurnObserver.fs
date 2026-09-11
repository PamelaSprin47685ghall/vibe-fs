namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Persistence.Journal

/// Turn observation policy for one reconciled turn (STRENGTH / RECOVERY-FAMILY / TurnWorkflow).
module HostTurnObserver =

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
            // DEG-OWN: exact-run owned consumption. Only an anomaly armed for this
            // SessionId + ProviderRun transfers, so a wrong/late run can never
            // consume a newer attempt's anomaly and session-only recovery is
            // never authorized.
            scope.LoopSensor.ConsumeAbortCause(context.Turn.SessionId, context.Turn.ProviderRun, context.Turn.Directory)
        | _ -> AbortCause.External

    /// DEG-OWN: settle the owned interrupt/continuation task before admitting
    /// later continuation/business observation. The owned task is awaited,
    /// never discarded.
    /// The wait addresses the exact current run: a mismatched run waits on
    /// nothing while the stored run's task remains owned.
    let private awaitOwnedInterrupt
        (scope: PluginRuntimeScope)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        : Task =
        task {
            match scope.LoopSensor.ActiveInterruptTask(sessionId, providerRun) with
            | None -> return ()
            | Some owned -> do! owned
        }

    let private observeApplicationTurn
        (observeTurnWorkflow: AbortCause -> ReconciledTurnContext -> Task)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
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
                // multiplexes SyncDelegate / Manager handled-bools.
                do! observeTurnWorkflow abortCause context
        }

    let private observeCurrentTurn
        (observeTurnWorkflow: AbortCause -> ReconciledTurnContext -> Task)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (scope: PluginRuntimeScope)
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
                        observeTurnWorkflow
                        sessionPort
                        rootWorkspace
                        eventPort
                        journal
                        scope
                        abortCause
                        context
        }

    let private observeBusinessTurn
        (observeTurnWorkflow: AbortCause -> ReconciledTurnContext -> Task)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (handlePreTurn: (ReconciledTurn -> Task<bool>) option)
        (observePrimaryTurn: (ReconciledTurn -> Task<unit>) option)
        (scope: PluginRuntimeScope)
        (context: ReconciledTurnContext)
        : Task =
        task {
            let turn = context.Turn
            let abortCause = abortCauseOfTurn scope context

            // DEG-OWN: the owned interrupt/continuation settles before later
            // business observation is admitted.
            do! awaitOwnedInterrupt scope turn.SessionId turn.ProviderRun

            let! preTurnHandled =
                match handlePreTurn with
                | Some handler -> handler turn
                | None -> Task.FromResult false

            if preTurnHandled then
                // STRENGTH-004/011: Replica observations are leaf-local. They
                // only reconcile the request plan for cleanup; family recovery,
                // owner fallback, Companion and ordinary TurnWorkflow
                // must never observe them.
                do! XWire.reconcileAttempt journal scope turn
                return ()
            else
                // SPEC-INV-013 / STRENGTH-010 / STRENGTH-007: primary turn observation
                // (closing dry run, primary symbol evidence collection, durable append)
                // executes before observeCurrentTurn.
                match observePrimaryTurn with
                | Some observePrimary -> do! observePrimary turn
                | None -> ()

                // Current-process Host observation proceeds from its exact facts.
                // No durable-family gate is fabricated here: Join tools admit via
                // their exact current-process permit, and explicit /continue owns
                // future user-driven work.
                return!
                    observeCurrentTurn
                        observeTurnWorkflow
                        sessionPort
                        rootWorkspace
                        eventPort
                        journal
                        scope
                        abortCause
                        context
        }

    let observe
        (observeTurnWorkflow: AbortCause -> ReconciledTurnContext -> Task)
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (handlePreTurn: (ReconciledTurn -> Task<bool>) option)
        (observePrimaryTurn: (ReconciledTurn -> Task<unit>) option)
        (scope: PluginRuntimeScope)
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
                observeTurnWorkflow
                sessionPort
                rootWorkspace
                eventPort
                journal
                handlePreTurn
                observePrimaryTurn
                scope
                context
