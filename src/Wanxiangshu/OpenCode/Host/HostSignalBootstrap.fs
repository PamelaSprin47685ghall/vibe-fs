namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git.Hook
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process
open Wanxiangshu.Resources
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence

module HostSignalBootstrap =

    type internal ChatAdmissionHookFailure =
        | IntentRejected of ChatAdmissionIntent.Rejection
        | TransactionFailed of ChatAdmissionTransactionError
        | TransactionStopped of ChatAdmissionTransactionOutcome

    type internal ChatAdmissionHookException(failure: ChatAdmissionHookFailure, executionKey: ChatExecutionKey option) =
        inherit Exception(sprintf "Managed chat admission failed: %A" failure)
        member _.Failure = failure
        member _.ExecutionKey = executionKey

    /// What the composition root needs back from `wire`.
    ///
    /// Exactly the members `SpikePlugin` calls. Six more used to hang here —
    /// `Reconciler`, `SignalRouter`, `Subscription`, `UnregisterOwned`,
    /// `RegisterSource`, `BindUserMessage` — with no consumer anywhere: the
    /// subscription is already tracked by the scope inside `wire`, and the three
    /// functions are called internally by the binding helpers. Handing them out as
    /// well made the signal stack look like it had six more entry points than it does.
    type WiredSignals =
        { RegisterOwned: string -> unit
          CancelSignals: SessionId seq -> unit
          BindActiveRun: SessionId -> Role -> string option -> unit
          CurrentPhysicalUserMessage: string -> string option
          ChatMessageHook: obj
          ObserveEvent: obj -> Task<unit> }

    let private observeSessionIdentity (sessionId: SessionId) (hasParent: bool) (agent: string option) =
        if hasParent then
            SessionExecutionBinding.observeHostAuxiliaryChild sessionId

        agent
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.iter (SessionExecutionBinding.observeUserFacingAgent sessionId)

    let private observeSessionEvent (raw: obj) =
        raw
        |> HostIngressCodec.sessionObservation
        |> Option.iter (fun observation ->
            observeSessionIdentity observation.SessionId observation.HasParent observation.Agent)

    let wire
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (snapshotOpt: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (scope: PluginRuntimeScope)
        (input: obj)
        /// Exact process-local private-agent attachment; it cannot establish a public authority profile.
        (tryConsumeHostInternalPrompt: SessionId -> string option -> string option -> bool)
        /// Exact assistant terminal evidence for private agents outside public PromptAuthority.
        (observeHostInternalTerminal: ExactProviderTerminalObservation -> unit)
        /// Workspace root for graceful Casebook finalize (SpikePlugin → CasebookLifecycle).
        (workspaceDirectory: string option)
        /// Owner-scope graceful close: finalize inspector draft once (root → inspectorSessionId).
        /// Returns a Task so SessionDeleted can await CaseFinalize before CancelSession.
        (tryFinalizeInspector: (string -> string -> Task<Result<unit, string>>) option)
        /// Unexpected / residual draft cleanup (inspectorSessionId).
        (cleanupInspector: (string -> unit) option)
        : Task<WiredSignals> =
        task {
            let finalizeInspector =
                defaultArg tryFinalizeInspector (fun _ _ -> Task.FromResult(Ok()))

            let cleanupInspectorDraft = defaultArg cleanupInspector (fun _ -> ())

            let snapshot =
                match snapshotOpt with
                | Some port -> port
                | None ->
                    { new ISessionSnapshotPort with
                        member _.GetMessages _ =
                            Task.FromResult(Ok([]: SessionMessage list)) }

            journal
            |> Option.iter (fun durable ->
                let recovery = SessionRecoveryHost(durable, snapshot, scope.Recovery, None)
                scope.AttachChatRecoveryRuntime recovery

                scope.AttachDurabilityActivation(fun () ->
                    scope.RunBackground(fun () ->
                        scope.SignalChatRecovery ChatExecutionRecoveryLifecycleEvent.DurabilityActivated)))

            // Host visibility catch-up still owns a Node timer backstop. Provider
            // recovery itself is causal and no longer uses a wall-clock deadline.
            let recoveryTimerPort = NodeTiming.nodeTimerPort ()

            // HOST-BOUNDARY-008: projection catch-up wakes on the session's
            // message.updated signal; recoveryTimerPort supplies the backstop.
            let messageVisibility = MessageVisibilityHub(recoveryTimerPort)
            scope.AttachMessageVisibility messageVisibility

            let reviewerContinuationPort = HostReviewGuard.continuationPort sessionPort journal

            let resolveProjection (sessionId: SessionId) : AgentProjectionSet option =
                match journal with
                | None -> None
                | Some j -> Some((AgentJournal.snapshot j).AgentProjections)

            let binding = TurnBinding.Store()

            let onTurn =
                HostTurnObserver.observe sessionPort eventPort journal strengthDurability scope reviewerContinuationPort

            let onSnapshot = HostCompactionObserver.observe scope journal

            let reconciler =
                Reconciler.Scheduler(
                    snapshot,
                    binding,
                    onTurn,
                    ?projection = Some resolveProjection,
                    ?onSnapshot = Some onSnapshot,
                    ?durableUnavailable = Some(fun () -> journal |> Option.exists AgentJournal.isPoisoned)
                )

            do scope.TrackReconcileShutdown(fun () -> reconciler.StopAndDrain())

            let handleOrdinaryAbort sessionId signal =
                scope.Sessions.Quiescence.RevokeCurrentAttempt sessionId

                scope.Strength.StrengthReplicaRuntime
                |> Option.iter (fun runtime -> runtime.CancelOwner sessionId |> ignore)

                reconciler.Signal signal

            /// FALLBACK-003: no Host signal may name the failed ProviderRun.
            ///
            /// `ProviderFailure` and `ProviderRetry` used to run their own writers here
            /// — a second and third writer of the durable cursor, each deciding from
            /// event fields whether an attempt had failed. Both are gone: the
            /// reconciled snapshot supplies exact run identity, and FallbackController
            /// performs the advance. ProviderFailure contributes only failure finality;
            /// Scheduler freezes the current physical identity at signal admission and
            /// reconciliation must match it to the snapshot assistant before publishing.
            let onSignal (signal: HostSignal) =
                match signal with
                | SessionIdle sessionId ->
                    // LOOP-005: idle ends the attempt → fresh detector for the next stream.
                    // Armed anomaly must survive until TurnAborted reconciliation consumes
                    // guard ownership (ResetDetector deliberately does not clear it; DG-008).
                    scope.LoopSensor.ResetDetector sessionId

                    // HOST-004: the idle observation mints the quiescence permit that
                    // idle-derived continuations must hold at send time. The permit is
                    // process-local — never journalled.
                    let permit = scope.Sessions.Quiescence.ObserveIdle sessionId
                    reconciler.SignalIdle(sessionId, permit)
                | ProviderRetry _
                | ProviderFailure _ -> reconciler.Signal signal
                // HOST-002/004: operator abort immediately revokes the current
                // attempt's idle permits, then routes to the
                // reconciler. Never ProviderFailure — it does not advance fallback.
                | AttemptAborted failure ->
                    // Fission retires only the replaced physical present. It is not
                    // an owner cancellation: do not revoke owner resources or cancel
                    // speculation/children here. Revoke the physical attempt's idle
                    // continuation capability so the retired conversation never continues.
                    FissionHost.routeAttemptAborted
                        failure.SessionId
                        (fun () ->
                            scope.Sessions.Quiescence.RevokeCurrentAttempt failure.SessionId
                            reconciler.Signal signal)
                        (fun () -> handleOrdinaryAbort failure.SessionId signal)
                | SessionDeleted(sessionId, parentSessionIdOpt) ->
                    let deletion = HostSessionDeletion.prepare scope sessionId parentSessionIdOpt

                    scope.RunBackground(fun () ->
                        task {
                            do!
                                HostSessionDeletion.finalizePreparedInspector
                                    scope
                                    workspaceDirectory
                                    finalizeInspector
                                    deletion

                            do!
                                scope.SignalChatRecoverySession
                                    sessionId
                                    ChatExecutionRecoveryLifecycleEvent.SessionDeleted

                            do! scope.DrainChatRecovery sessionId

                            do!
                                HostSessionDeletion.handle
                                    scope
                                    cleanupInspectorDraft
                                    reconciler.Signal
                                    sessionId
                                    deletion
                        })

            // LOOP-002/006 and HOST-027 share one raw Host subscription but own
            // disjoint stream fields. Both abort physically; only their typed armed
            // marks decide the later reconciled-turn meaning.
            let loopSensor =
                LoopSensor.create
                    scope.Sessions.OwnedSessions
                    scope.Sessions.SessionParents
                    (fun sessionId -> sessionPort.InterruptAttempt sessionId)
                    (fun sessionId kind directory ->
                        task {
                            let prompt =
                                ProviderProse.documentFor sessionId (LoopSensor.continuationPath kind) Map.empty

                            let! outcome =
                                HostSessionNudge.sendContinuationResult
                                    sessionPort
                                    sessionId
                                    prompt
                                    PromptAuthority.ContinuationKind.DegenerationGuard
                                    directory
                                    journal
                                    PromptDispatcher.AwaitMode.Detached
                                    None

                            return outcome |> Result.map ignore
                        })
                    Diagnostic.emit

            do scope.AttachLoopSensor loopSensor

            let exactStarted (key: ChatExecutionKey) : ProviderStartedEvidence option =
                journal
                |> Option.bind (fun durable ->
                    AgentJournal.snapshot durable
                    |> fun projection -> projection.AgentProjections.ChatExecutions
                    |> ChatExecutionProjection.byKey key
                    |> Option.bind _.ProviderStarted)

            let rejectProviderTerminal (observation: ExactProviderTerminalObservation) =
                Diagnostic.emit
                    "provider-terminal-evidence-mismatch"
                    [ "session_id", SessionId.value observation.SessionId
                      "physical_user_message_id", PhysicalUserMessageId.value observation.PhysicalUserMessageId ]

            let applyObservedTerminal
                (observation: ExactProviderTerminalObservation)
                (evidence: ProviderStartedEvidence)
                (disposition: ChatExecutionTerminalDisposition)
                =
                task {
                    do!
                        scope.SignalChatRecovery(
                            ChatExecutionRecoveryLifecycleEvent.ExactAssistantTerminal(evidence, disposition)
                        )

                    reconciler.NotifyProjectionChanged(observation.SessionId, observation.PhysicalUserMessageId)

                    FissionHost.observePhysicalExecutionEnd
                        reconciler.TryPhysicalUserMessage
                        journal
                        (fun sid -> reconciler.Kick(sid, ReconcileProgram.ReconcileWake.RetryWake))
                        observation.SessionId
                        observation.PhysicalUserMessageId
                }

            let startedEvidenceForTerminal (observation: ExactProviderTerminalObservation) =
                let key =
                    { SessionId = observation.SessionId
                      PhysicalUserMessageId = observation.PhysicalUserMessageId }

                exactStarted key

            let settleExactTerminal (observation: ExactProviderTerminalObservation) =
                match observation.Outcome, observation.Disposition, startedEvidenceForTerminal observation with
                | HostProviderTerminalOutcome.ProviderFailure failure, None, Some _ ->
                    reconciler.Kick(
                        observation.SessionId,
                        ReconcileProgram.ReconcileWake.FailureWake(
                            Some observation.PhysicalUserMessageId,
                            failure,
                            "exact-provider-terminal",
                            ReconcileProgram.FailureWakeSource.ExactAssistantProjection
                        )
                    )

                    Task.FromResult()
                | _, Some disposition, Some evidence -> applyObservedTerminal observation evidence disposition
                | _ ->
                    rejectProviderTerminal observation
                    Task.FromResult()

            let settleObservedTerminal (terminal: ExactProviderTerminalObservation option) =
                terminal
                |> Option.map settleExactTerminal
                |> Option.defaultValue (Task.FromResult())

            let continueStartedLifecycle
                (started: ExactProviderStartObservation)
                (providerStepEnded: bool)
                (terminal: ExactProviderTerminalObservation option)
                =
                task {
                    if providerStepEnded then
                        ModelRouting.endProviderStep started.SessionId started.PhysicalUserMessageId started.ProviderRun

                    do! settleObservedTerminal terminal
                }

            let rejectProviderStart
                (started: ExactProviderStartObservation)
                (error: SessionExecutionBinding.ProviderStartObservationError<unit>)
                =
                Diagnostic.emit
                    "provider-start-observation-rejected"
                    [ "session_id", SessionId.value started.SessionId
                      "physical_user_message_id", PhysicalUserMessageId.value started.PhysicalUserMessageId
                      "provider_run", ProviderRunIdentity.value started.ProviderRun
                      "reason", SessionExecutionBinding.providerStartObservationErrorCode error ]

            let signalProviderStarted (started: ExactProviderStartObservation) =
                let key =
                    { SessionId = started.SessionId
                      PhysicalUserMessageId = started.PhysicalUserMessageId }

                exactStarted key
                |> Option.map (
                    ChatExecutionRecoveryLifecycleEvent.ExactAssistantStarted
                    >> scope.SignalChatRecovery
                )
                |> Option.defaultValue (Task.FromResult() :> Task)

            let signalNewProviderStart started providerStarted =
                if providerStarted then
                    signalProviderStarted started
                else
                    Task.FromResult() :> Task

            let continueProviderStart
                (started: ExactProviderStartObservation)
                (providerStepEnded: bool)
                (terminal: ExactProviderTerminalObservation option)
                (persistence: Result<bool, SessionExecutionBinding.ProviderStartObservationError<unit>>)
                =
                match persistence with
                | Error error ->
                    rejectProviderStart started error
                    Task.FromResult() :> Task
                | Ok providerStarted ->
                    task {
                        reconciler.BindPhysicalUserMaterial(started.SessionId, started.PhysicalUserMessageId)
                        do! signalNewProviderStart started providerStarted
                        do! continueStartedLifecycle started providerStepEnded terminal
                    }
                    :> Task

            let signalRouter =
                HostSignalRouter(
                    scope.Sessions.OwnedSessions,
                    onSignal,
                    onLoopEvent = loopSensor.Observe,
                    onExactAssistantObservation =
                        (fun
                            (started: ExactProviderStartObservation)
                            (providerStepEnded: bool)
                            (terminal: ExactProviderTerminalObservation option) ->
                            task {
                                let! providerStarted =
                                    SessionExecutionBinding.persistProviderStartedFromObservation
                                        journal
                                        scope.TryBindAttemptPlan
                                        started

                                do! continueProviderStart started providerStepEnded terminal providerStarted
                            })
                )

            let! subscriptionResult = HostSignalSubscribe.trySubscribe input signalRouter.Observe

            let subscription: IDisposable option =
                match subscriptionResult with
                | Error error ->
                    let evidence = sprintf "%A" error
                    Diagnostic.fatal "signal-subscribe-failed" [ "result", evidence ]
                    raise (InvalidOperationException evidence)

                | Ok HostSignalSubscribe.HostSignalSubscriptionMode.LocalEventHook -> None
                | Ok(HostSignalSubscribe.HostSignalSubscriptionMode.EventsListen active) -> Some(active :> IDisposable)

            do scope.TrackSubscription subscription

            let registerOwned (sessionId: string) =
                if not (String.IsNullOrWhiteSpace sessionId) then
                    scope.Sessions.OwnedSessions.Add sessionId |> ignore
                    let sid = SessionId.create sessionId
                    signalRouter.RegisterOwned sid
                    // HOST-026: root / first-touch bind from global preference (idempotent).
                    ProviderLanguageBinding.ensureRoot sid |> ignore

            let bindUserMessage (sessionId: string) (messageId: string) =
                if
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                then
                    let sid = SessionId.create sessionId
                    let physical = PhysicalUserMessageId.create messageId
                    scope.Sessions.UserMessageBindings.[sessionId] <- physical

                    let agentRole =
                        HostSessionNudge.tryActiveProfile journal sid
                        |> Option.map (fun profile -> profile.CanonicalRole)

                    reconciler.BindUserMessage(sid, physical, ?agentRole = agentRole)
                    registerOwned sessionId

            let bindContinuationMessage (sessionId: string) (messageId: string) =
                if
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                then
                    reconciler.BindContinuationUserMessage(
                        SessionId.create sessionId,
                        PhysicalUserMessageId.create messageId
                    )

            let bindHumanContinuationMessage (sessionId: string) (messageId: string) =
                if
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                then
                    scope.Sessions.UserMessageBindings.[sessionId] <- PhysicalUserMessageId.create messageId
                    bindContinuationMessage sessionId messageId

            let bindActiveRun (sessionId: SessionId) (role: Role) (directory: string option) =
                let key = SessionId.value sessionId
                registerOwned key

                // A host-registered run knows its physical opening message; the
                // Authority Root is derived from it by PROMPT-002 promotion rather than
                // read out of a second binding table.
                let physical =
                    match scope.Sessions.UserMessageBindings.TryGetValue key with
                    | true, bound -> Some bound
                    | false, _ -> None

                reconciler.BindActiveRun
                    { SessionId = sessionId
                      RunId = None
                      AuthorityRootUserMessageId = physical |> Option.map PhysicalUserMessageId.promoteToAuthorityRoot
                      PhysicalUserMessageId = physical
                      ContinuationMessageIds = Set.empty
                      Role = Some role
                      Directory = directory }

            let admissionTransaction =
                journal
                |> Option.map (fun durable ->
                    let runtime = PromptDispatcher.forJournal durable
                    ChatAdmissionTransaction.production durable runtime.AcceptManagedChatIntent)

            let durabilityActivation =
                lazy
                    (match workspaceDirectory with
                     | None -> Ok()
                     | Some workspace -> HookDispatcher.ensure workspace)

            let requireDurabilityActivation () =
                match durabilityActivation.Value with
                | Ok() -> scope.ActivateDurability()
                | Error error -> Diagnostic.fatal "durability-activation-failed" [ "result", error ]

            let observePhysicalAdmission output sessionId physicalId =
                scope.Sessions.Quiescence.ObservePhysicalUserMessage(sessionId, physicalId)

                if ExplicitResumeSuppression.requiresPhysicalBinding sessionId physicalId output then
                    reconciler.BindPhysicalUserMaterial(sessionId, physicalId)

            let ensurePhysicalParentDiscovered (sessionId: SessionId) =
                task {
                    let key = SessionId.value sessionId

                    if
                        not (scope.Sessions.SessionParents.ContainsKey key)
                        && (SessionExecutionBinding.tryParent sessionId).IsNone
                    then
                        match! sessionPort.TryGetParentSession sessionId with
                        | Ok(Some parentId) -> scope.Sessions.SessionParents.[key] <- SessionId.value parentId
                        | _ -> ()
                }

            let hasPhysicalParent sessionId =
                scope.Sessions.SessionParents.ContainsKey(SessionId.value sessionId)

            let continueUnmanagedChatMessage intent =
                requireDurabilityActivation ()
                JoinWake.observeChatMessage scope.Sessions.JoinInterrupts intent

            let observePendingContinuation (evidence: ChatAdmissionIntent.PendingPromptEvidence) =
                match evidence.Origin with
                | PromptAuthority.PromptOrigin.Continuation PromptAuthority.ContinuationKind.ProviderRetryAttempt ->
                    scope.ArmRecovery(evidence.Key.SessionId, evidence.Key.PhysicalUserMessageId)
                | _ -> ()

            let continueManagedChatMessage intent output =
                match intent with
                | ChatAdmissionIntent.Decision.ExternalRootIntent evidence ->
                    scope.Sessions.ModelRoutingSessions.Add(SessionId.value evidence.Key.SessionId)
                    |> ignore

                    bindUserMessage
                        (SessionId.value evidence.Key.SessionId)
                        (PhysicalUserMessageId.value evidence.Key.PhysicalUserMessageId)
                | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent evidence ->
                    let sessionId = SessionId.value evidence.Key.SessionId
                    let physicalId = PhysicalUserMessageId.value evidence.Key.PhysicalUserMessageId

                    scope.Sessions.ModelRoutingSessions.Add sessionId |> ignore
                    bindHumanContinuationMessage sessionId physicalId
                    registerOwned sessionId
                | ChatAdmissionIntent.Decision.PendingPromptIntent evidence ->
                    let sessionId = SessionId.value evidence.Key.SessionId
                    let physicalId = PhysicalUserMessageId.value evidence.Key.PhysicalUserMessageId

                    scope.Sessions.ModelRoutingSessions.Add sessionId |> ignore
                    bindContinuationMessage sessionId physicalId
                    registerOwned sessionId
                    observePendingContinuation evidence
                | _ -> ()

                FissionHostRequestProjection.projectPendingManaged hasPhysicalParent intent output
                requireDurabilityActivation ()
                JoinWake.observeChatMessage scope.Sessions.JoinInterrupts intent

            let currentExecution durable intent =
                let key =
                    match intent with
                    | ChatAdmissionIntent.Decision.ExternalRootIntent evidence ->
                        { SessionId = evidence.Key.SessionId
                          PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
                    | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent evidence ->
                        { SessionId = evidence.Key.SessionId
                          PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
                    | ChatAdmissionIntent.Decision.PendingPromptIntent evidence ->
                        { SessionId = evidence.Key.SessionId
                          PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
                    | _ -> invalidArg "intent" "managed chat transaction requires a managed intent"

                (AgentJournal.snapshot durable).AgentProjections.ChatExecutions
                |> ChatExecutionProjection.byKey key

            let executionKey intent =
                match intent with
                | ChatAdmissionIntent.Decision.ExternalRootIntent evidence ->
                    Some
                        { SessionId = evidence.Key.SessionId
                          PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
                | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent evidence ->
                    Some
                        { SessionId = evidence.Key.SessionId
                          PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
                | ChatAdmissionIntent.Decision.PendingPromptIntent evidence ->
                    Some
                        { SessionId = evidence.Key.SessionId
                          PhysicalUserMessageId = evidence.Key.PhysicalUserMessageId }
                | ChatAdmissionIntent.Decision.NoManagedExecution _
                | ChatAdmissionIntent.Decision.HostInternal _
                | ChatAdmissionIntent.Decision.Reject _ -> None

            let admitManagedChatMessage durable createTransaction intent output =
                task {
                    let ports = createTransaction (ModelRouting.projectHostModel output)

                    match!
                        ChatAdmissionTransaction.execute
                            ports
                            { Intent = intent
                              CurrentState = currentExecution durable intent }
                    with
                    | Ok(ChatAdmissionTransactionOutcome.Settled _) -> continueManagedChatMessage intent output
                    | Ok outcome -> raise (ChatAdmissionHookException(TransactionStopped outcome, executionKey intent))
                    | Error error -> raise (ChatAdmissionHookException(TransactionFailed error, executionKey intent))
                }

            let rejectedChatMessage failure =
                let completion =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                completion.SetException(ChatAdmissionHookException(failure, None))
                completion.Task

            let continueClassifiedChatMessage intent output =
                match intent, journal, admissionTransaction with
                | ChatAdmissionIntent.Decision.NoManagedExecution _, _, _
                | ChatAdmissionIntent.Decision.HostInternal _, _, _ ->
                    continueUnmanagedChatMessage intent
                    Task.FromResult()
                | ChatAdmissionIntent.Decision.Reject rejection, _, _ -> rejectedChatMessage (IntentRejected rejection)
                | ChatAdmissionIntent.Decision.ExternalRootIntent _, Some durable, Some createTransaction
                | ChatAdmissionIntent.Decision.PendingPromptIntent _, Some durable, Some createTransaction ->
                    admitManagedChatMessage durable createTransaction intent output
                | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent _, Some durable, Some createTransaction ->
                    JoinWake.observeChatMessage scope.Sessions.JoinInterrupts intent
                    admitManagedChatMessage durable createTransaction intent output
                | ChatAdmissionIntent.Decision.ExternalRootIntent _, _, _
                | ChatAdmissionIntent.Decision.ActiveHumanContinuationIntent _, _, _
                | ChatAdmissionIntent.Decision.PendingPromptIntent _, _, _ ->
                    rejectedChatMessage (IntentRejected ChatAdmissionIntent.Rejection.DurableAuthorityUnavailable)

            let client = if isNull input then null else input?client

            let sessionAgentOfResponse (rawBody: obj) : string option = HostIngressCodec.sessionAgent rawBody

            let executeSessionGet (sessObj: obj) (getFn: obj) (sId: string) : Task<string option> =
                task {
                    let payload =
                        createObj
                            [ "path", box (createObj [ "id", box sId ])
                              "sessionID", box sId
                              "headers", box (createObj []) ]

                    let! res = unbox<Task<obj>> (getFn?call (sessObj, payload))
                    return sessionAgentOfResponse res
                }

            let canQuerySession =
                not (isNull client)
                && not (isNull client?session)
                && not (isNull client?session?get)

            let safeQuerySession (sessionId: SessionId) : Task<string option> =
                task {
                    try
                        return! executeSessionGet client?session client?session?get (SessionId.value sessionId)
                    with _ ->
                        return None
                }

            let tryGetSessionAgent (sessionId: SessionId) : Task<string option> =
                if not canQuerySession then
                    Task.FromResult None
                else
                    safeQuerySession sessionId

            let queryMissingAgent sid =
                task {
                    let! fetched = tryGetSessionAgent sid
                    fetched |> Option.iter (SessionExecutionBinding.observeUserFacingAgent sid)
                    return fetched
                }

            let resolveAgentForSession sid =
                task {
                    match SessionExecutionBinding.tryAgent sid with
                    | Some agent -> return Some agent
                    | None -> return! queryMissingAgent sid
                }

            let applyResolvedAgent agentOpt (decoded: PromptIngressCodec.DecodedMessage) =
                match agentOpt with
                | Some agent ->
                    { decoded with
                        ExplicitAgent = Some agent }
                | None -> decoded

            let resolveAgentForDecodedMessage (decoded: PromptIngressCodec.DecodedMessage) =
                task {
                    match decoded.ExplicitAgent, decoded.SessionId with
                    | Some _, _
                    | _, None -> return decoded
                    | None, Some sid ->
                        let! agentOpt = resolveAgentForSession sid
                        return applyResolvedAgent agentOpt decoded
                }

            let chatMessageHook =
                fun (input: obj) (output: obj) ->
                    task {
                        requireDurabilityActivation ()

                        // Decode and resolve once; routing and physical authority consume
                        // the same frozen claim and identity evidence.
                        let decoded = PromptIngressCodec.decode input output
                        let! decoded = resolveAgentForDecodedMessage decoded

                        let intent =
                            match decoded.SessionId with
                            | Some sessionId when
                                tryConsumeHostInternalPrompt sessionId decoded.ExplicitAgent decoded.Text
                                ->
                                ChatAdmissionIntent.Decision.HostInternal
                                    { SessionId = decoded.SessionId
                                      PhysicalUserMessageId = decoded.PhysicalUserMessageId
                                      Origin = PromptAuthority.PromptOrigin.HostInternal }
                            | _ -> PromptIngress.resolveDecision journal decoded

                        match decoded.SessionId with
                        | Some sessionId -> do! ensurePhysicalParentDiscovered sessionId
                        | None -> ()

                        // INTRA-PARTICIPANT-PARALLELISM-013: request-local origin
                        // narrowing is independent of business-root admission. In
                        // particular, CRASH-018 explicit /continue still performs a
                        // provider turn even though it deliberately skips managed admission.
                        FissionHostRequestProjection.projectExternalManaged hasPhysicalParent intent output

                        // CRASH-018: command.execute.before has no physical message id.
                        // Carry its dynamic restart disclosure across that one Host seam,
                        // then materialize it on the real chat.message before any owner
                        // policy can observe the turn. Hosts that already forwarded the
                        // marked part only consume the pending handoff here.
                        let explicitResume = ExplicitResumeSuppression.classifyChatMessage decoded output

                        // CRASH-018: bind the disclosure marker to this exact
                        // physical user material before any routing/reconcile wake
                        // can interpret the turn. A later unmarked physical user
                        // message on the same reusable SessionId clears it here.
                        match decoded.SessionId, decoded.PhysicalUserMessageId with
                        | Some sessionId, Some physicalId ->
                            // HOST-004 / CRASH-006: physical admission itself closes
                            // the previous terminal's idle-send window. Waiting until
                            // messages.transform leaves a race where an old idle repair
                            // can enqueue after this message is accepted and supersede
                            // its model-routing lease before chat.params.
                            observePhysicalAdmission output sessionId physicalId
                        | _ -> ()

                        if explicitResume then
                            // Disclosure is transport/reconciliation context, not a new
                            // business root. The Host still performs the provider turn;
                            // Wanxiangshu does not mint PromptIngress/AuthorityRoot,
                            // acquire a managed business lease, wake joins, or commit a
                            // continuation capability for this physical material.
                            ExplicitSessionResume.observeChatMessage
                                (fun sessionId ->
                                    scope.Sessions.ModelRoutingSessions.Add(SessionId.value sessionId) |> ignore)
                                journal
                                decoded
                        else
                            do! continueClassifiedChatMessage intent output
                    }

            let cancelSignals (ids: SessionId seq) =
                ids
                |> Seq.iter (fun id ->
                    scope.LoopSensor.DropSession id
                    signalRouter.UnregisterOwned id)

            return
                { RegisterOwned = registerOwned
                  CancelSignals = cancelSignals
                  BindActiveRun = bindActiveRun
                  CurrentPhysicalUserMessage =
                    (fun sessionId ->
                        reconciler.TryPhysicalUserMessage(SessionId.create sessionId)
                        |> Option.map PhysicalUserMessageId.value)
                  ChatMessageHook = chatMessageHook
                  ObserveEvent =
                    (fun raw ->
                        task {
                            HostEventCodec.tryDecodeExactProviderTerminal raw
                            |> Option.iter observeHostInternalTerminal

                            observeSessionEvent raw
                            do! signalRouter.ObserveLocal raw
                            SyncDelegateHostObservation.observe scope.SyncDelegateRuntime raw
                            MessageVisibilitySignal.observeEvent messageVisibility raw
                        }) }
        }
