namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Optional Host capability bound to exact already-accepted physical material.
/// Absence is not permission to create a replacement PromptClaim or resend text.
type ExactAcceptedMessageRecoveryPort =
    { ResumeAccepted: PreProviderResumeRequest -> Task<bool>
      RequeueAuthorized: ProviderRequeueRequest -> Task<bool> }

type SessionRecoveryHost
    (
        journal: AgentJournal,
        snapshot: ISessionSnapshotPort,
        scope: PluginRecoveryScope,
        acceptedMessageRecovery: ExactAcceptedMessageRecoveryPort option
    ) =

    let drainGate = obj ()
    let drainWaiters = Dictionary<string, TaskCompletionSource<unit>>()

    let keyOfStarted (started: ProviderStartedEvidence) : ChatExecutionKey =
        { SessionId = started.Accepted.SessionId
          PhysicalUserMessageId = started.Accepted.PhysicalUserMessageId }

    let eventKey (event: ChatExecutionRecoveryLifecycleEvent) =
        match event with
        | ChatExecutionRecoveryLifecycleEvent.ExactAssistantStarted started -> Some(keyOfStarted started)
        | ChatExecutionRecoveryLifecycleEvent.ExactAssistantTerminal(started, _) -> Some(keyOfStarted started)
        | ChatExecutionRecoveryLifecycleEvent.SessionAborted key
        | ChatExecutionRecoveryLifecycleEvent.SessionDeleted key
        | ChatExecutionRecoveryLifecycleEvent.SessionCancelled key
        | ChatExecutionRecoveryLifecycleEvent.TypedFailureDecision(key, _) -> Some key
        | _ -> None

    let statesFor (event: ChatExecutionRecoveryLifecycleEvent) =
        let projection = (AgentJournal.snapshot journal).AgentProjections.ChatExecutions

        eventKey event
        |> Option.map (fun key -> ChatExecutionProjection.byKey key projection |> Option.toList)
        |> Option.defaultWith (fun () -> ChatExecutionProjection.current projection)

    let classifyProviderSnapshot (state: ChatExecutionState) (messages: SessionMessage list) =
        let matches =
            messages
            |> List.filter (fun message ->
                message.Role = "assistant"
                && message.ParentId = Some(PhysicalUserMessageId.value state.Key.PhysicalUserMessageId))

        match matches, state.ProviderStarted with
        | [], _ -> ProviderPhysicalObservation.ProviderAbsent state.Key
        | [ message ], Some started when
            message.Id = ProviderRunIdentity.value started.ProviderRun
            && not message.Completed
            ->
            ProviderPhysicalObservation.ProviderAlive started
        | _ -> ProviderPhysicalObservation.ReceiptAmbiguous

    let providerFromSnapshot (state: ChatExecutionState) =
        task {
            match! snapshot.GetMessages state.Key.SessionId with
            | Error _ -> return ProviderPhysicalObservation.ReceiptMissing
            | Ok messages -> return classifyProviderSnapshot state messages
        }

    let providerObservation (event: ChatExecutionRecoveryLifecycleEvent) (state: ChatExecutionState) =
        match event with
        | ChatExecutionRecoveryLifecycleEvent.ExactAssistantStarted started ->
            Task.FromResult(ProviderPhysicalObservation.ProviderAlive started)
        | ChatExecutionRecoveryLifecycleEvent.ExactAssistantTerminal(started, disposition) ->
            Task.FromResult(ProviderPhysicalObservation.ProviderTerminal(started, disposition))
        | ChatExecutionRecoveryLifecycleEvent.SessionDeleted _ ->
            Task.FromResult(ProviderPhysicalObservation.ProviderAbsent state.Key)
        | _ -> providerFromSnapshot state

    let lifecycleCancellation (event: ChatExecutionRecoveryLifecycleEvent) =
        match event with
        | ChatExecutionRecoveryLifecycleEvent.SessionDeleted _
        | ChatExecutionRecoveryLifecycleEvent.SessionCancelled _ -> true
        | _ -> false

    let cancelledProviderDecision (state: ChatExecutionState) (started: ProviderStartedEvidence) =
        ExecutionFailurePolicy.decide
            { Failure = ExecutionFailure.UserCancelled
              Lifecycle = DurableExecutionLifecycle.ProviderStarted
              ExecutionKey = state.Key
              Capacity = CapacityOwnership.NoCapacityFence
              Provider =
                { LogicalRun = started.Accepted.LogicalRunId
                  ProviderRun = started.ProviderRun
                  RequestKind = started.RequestKind
                  RetryBudget = ProviderRecoveryBudget.Exhausted
                  FallbackBudget = ProviderRecoveryBudget.Exhausted
                  Breaker = ProviderBreakerState.Closed } }
        |> RecoveryPolicyEvidence.FailureDecision

    let cancellationFailureEvidence (state: ChatExecutionState) =
        state.ProviderStarted
        |> Option.map (cancelledProviderDecision state)
        |> Option.defaultValue RecoveryPolicyEvidence.NoFailureDecision

    let failureEvidence (event: ChatExecutionRecoveryLifecycleEvent) (state: ChatExecutionState) =
        match event with
        | ChatExecutionRecoveryLifecycleEvent.TypedFailureDecision(_, decision) ->
            RecoveryPolicyEvidence.FailureDecision decision
        | _ when lifecycleCancellation event -> cancellationFailureEvidence state
        | _ -> RecoveryPolicyEvidence.NoFailureDecision

    let currentState (state: ChatExecutionState) =
        (AgentJournal.snapshot journal).AgentProjections.ChatExecutions
        |> ChatExecutionProjection.byKey state.Key
        |> Option.defaultValue state

    let completedLifecycleSettlement
        (state: ChatExecutionState)
        (settlement: Result<PreProviderTerminalWitness, PreProviderSettlementError>)
        =
        match settlement with
        | Ok _ -> currentState state
        | Error error -> raise (InvalidOperationException($"managed chat lifecycle settlement failed: {error}"))

    let settleAcceptedCancellation (event: ChatExecutionRecoveryLifecycleEvent) (state: ChatExecutionState) =
        task {
            match lifecycleCancellation event, state.Lifecycle with
            | true, ChatExecutionLifecycle.Accepted ->
                let! settled =
                    PreProviderSettlement.settle
                        journal
                        state.Key
                        state.Evidence
                        ChatExecutionTerminalDisposition.Cancelled

                return completedLifecycleSettlement state settled
            | _ -> return state
        }

    let persistenceCommitment () =
        if AgentJournal.isPoisoned journal then
            PersistenceCommitment.Unknown
        else
            PersistenceCommitment.NotCommitted

    let release (key: ChatExecutionKey) =
        match ModelRouting.releasePhysicalExecution key.SessionId key.PhysicalUserMessageId with
        | CapacityTransitionOutcome.Applied
        | CapacityTransitionOutcome.AlreadyApplied
        | CapacityTransitionOutcome.StaleFence -> Task.FromResult(()) :> Task
        | CapacityTransitionOutcome.Conflict ->
            raise (InvalidOperationException "managed chat recovery exact capacity release was rejected")

    let requirePersistence (label: string) (result: Result<'witness, ManagedChatProviderLifecycleError>) =
        match result with
        | Ok _ -> ()
        | Error error -> raise (InvalidOperationException($"managed chat recovery {label} persistence failed: {error}"))

    let persistStarted (started: ProviderStartedEvidence) =
        let key = keyOfStarted started

        task {
            let! result =
                ManagedChatProviderLifecycle.providerStarted
                    journal
                    key
                    started.Accepted
                    started.ProviderRun
                    started.RequestKind
                    started.ProjectionChoice

            return requirePersistence "start" result
        }
        :> Task

    let persistTerminal
        (key: ChatExecutionKey)
        (terminalEvidence: ChatExecutionTerminalEvidence)
        (disposition: ChatExecutionTerminalDisposition)
        =
        match terminalEvidence with
        | ChatExecutionTerminalEvidence.PreProvider _ ->
            invalidOp "managed chat recovery Finalize requires provider-started terminal evidence"
        | ChatExecutionTerminalEvidence.AfterProviderStart started ->
            task {
                let! result = ManagedChatProviderLifecycle.terminal journal key started disposition

                requirePersistence "terminal" result
                do! release key
            }
            :> Task

    let reconcile (request: PhysicalReconciliationRequest) =
        match request with
        | PhysicalReconciliationRequest.PersistProviderStarted started -> persistStarted started
        | PhysicalReconciliationRequest.PersistProviderStartedAndTerminal(started, disposition) ->
            task {
                do! persistStarted started

                do!
                    persistTerminal
                        (keyOfStarted started)
                        (ChatExecutionTerminalEvidence.AfterProviderStart started)
                        disposition
            }
            :> Task
        | PhysicalReconciliationRequest.ReleaseTerminalResource(key, _, _) -> release key

    let resume (request: PreProviderResumeRequest) =
        let publish =
            function
            | true -> ()
            | false -> scope.PublishPendingChatResume request

        task {
            match acceptedMessageRecovery with
            | Some port ->
                let! resumed = port.ResumeAccepted request
                publish resumed
            | None -> scope.PublishPendingChatResume request
        }
        :> Task

    let requeue (request: ProviderRequeueRequest) =
        let publish =
            function
            | true -> ()
            | false -> scope.PublishAuthorizedChatRequeue request

        task {
            match acceptedMessageRecovery with
            | Some port ->
                let! requeued = port.RequeueAuthorized request
                publish requeued
            | None -> scope.PublishAuthorizedChatRequeue request
        }
        :> Task

    let finalize (request: TerminalFinalizationRequest) =
        persistTerminal request.ExecutionKey request.TerminalEvidence request.TerminalDisposition

    let actions =
        { ReconcilePhysical = reconcile
          ResumePreProvider = resume
          RequeueEligible = requeue
          Finalize = finalize
          MarkManualIntervention =
            fun request ->
                scope.PublishManualChatIntervention request
                Task.FromResult(()) :> Task }

    let sessionDrained (sessionId: SessionId) =
        (AgentJournal.snapshot journal).AgentProjections.ChatExecutions
        |> ChatExecutionProjection.current
        |> List.filter (fun state -> state.Key.SessionId = sessionId)
        |> List.forall (fun state ->
            match state.Lifecycle, ModelRouting.observePhysicalResource state.Key with
            | ChatExecutionLifecycle.Terminal _, PhysicalResourceObservation.ResourceAbsent _
            | ChatExecutionLifecycle.Terminal _, PhysicalResourceObservation.ResourceReleased _ -> true
            | _ -> false)

    let takeDrainWaiter (sessionId: SessionId) =
        lock drainGate (fun () ->
            let key = SessionId.value sessionId

            match drainWaiters.TryGetValue key with
            | true, completion ->
                drainWaiters.Remove key |> ignore
                Some completion
            | false, _ -> None)

    let pulseDrain (sessionId: SessionId) =
        if sessionDrained sessionId then
            takeDrainWaiter sessionId
            |> Option.iter (fun completion -> AsyncSupport.trySetResult completion () |> ignore)

    let sessionsToPulse (event: ChatExecutionRecoveryLifecycleEvent) =
        match eventKey event with
        | Some key -> [ key.SessionId ]
        | None -> statesFor event |> List.map (fun state -> state.Key.SessionId) |> List.distinct

    let recoverState (event: ChatExecutionRecoveryLifecycleEvent) (state: ChatExecutionState) =
        task {
            let! current = settleAcceptedCancellation event state
            let! provider = providerObservation event current

            let evidence =
                { ExecutionState = current
                  ProviderObservation = provider
                  ResourceObservation = ModelRouting.observePhysicalResource current.Key
                  PersistenceCommitment = persistenceCommitment ()
                  FailureDecisionEvidence = failureEvidence event current }

            let! _ = ChatExecutionRecoveryRuntime.recover actions evidence
            ()
        }

    let waitForDrain (sessionId: SessionId) =
        lock drainGate (fun () ->
            let key = SessionId.value sessionId

            match drainWaiters.TryGetValue key with
            | true, completion -> completion.Task :> Task
            | false, _ ->
                let completion =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                drainWaiters.[key] <- completion
                completion.Task :> Task)

    member _.Signal(event: ChatExecutionRecoveryLifecycleEvent) : Task =
        task {
            for state in statesFor event do
                do! recoverState event state

            sessionsToPulse event |> List.iter pulseDrain
        }
        :> Task

    member this.SignalSession
        (sessionId: SessionId, eventOf: ChatExecutionKey -> ChatExecutionRecoveryLifecycleEvent)
        : Task =
        task {
            let keys =
                (AgentJournal.snapshot journal).AgentProjections.ChatExecutions
                |> ChatExecutionProjection.current
                |> List.choose (fun state ->
                    if state.Key.SessionId = sessionId then
                        Some state.Key
                    else
                        None)

            for key in keys do
                do! this.Signal(eventOf key)
        }
        :> Task

    member _.Drain(sessionId: SessionId) : Task =
        if sessionDrained sessionId then
            Task.FromResult(()) :> Task
        else
            waitForDrain sessionId
