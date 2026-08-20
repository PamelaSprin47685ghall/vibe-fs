namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Fable.Core.JsInterop
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
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Git.Hook
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Process
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
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
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode

module HostSignalBootstrap =

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
          ObserveEvent: obj -> unit }

    let wire
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (snapshotOpt: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (scope: PluginRuntimeScope)
        (input: obj)
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

            // One ITimerPort for provider-recovery deadlines (G4R-CE S2).
            // Application awaits via IDeadlineHandle; Host owns the Node adapter.
            let recoveryTimerPort = PtyTiming.nodeTimerPort ()

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
                HostTurnObserver.observe
                    recoveryTimerPort
                    sessionPort
                    eventPort
                    journal
                    strengthDurability
                    scope
                    reviewerContinuationPort

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
                // SW-017 ②: do not probe NeedHelpSensor armed presence to branch.
                // The typed AssistanceAbortClaim is consumed once by the owning CE (AssistanceHost)
                // behind the fresh SessionIdle fence. Revoke current attempt unconditionally here;
                // the fresh idle wake mints a new QuiescencePermit for any idle-derived successor.
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
                    // LoopKillArmed must stay until OrdinaryTurnWorkflow bridges TurnAborted
                    // (ResetDetector deliberately does not clear it; LOOP-006).
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
                | AttemptAborted sessionId ->
                    // Fission retires only the replaced physical present. It is not
                    // an owner cancellation: do not revoke owner resources or cancel
                    // speculation/children here. Revoke the physical attempt's idle
                    // continuation capability so the retired conversation never continues.
                    FissionHost.routeAttemptAborted
                        sessionId
                        (fun () ->
                            scope.Sessions.Quiescence.RevokeCurrentAttempt sessionId
                            reconciler.Signal signal)
                        (fun () -> handleOrdinaryAbort sessionId signal)
                | SessionDeleted(sessionId, parentSessionIdOpt) ->
                    scope.RunBackground(fun () ->
                        HostSessionDeletion.handle
                            scope
                            workspaceDirectory
                            finalizeInspector
                            cleanupInspectorDraft
                            reconciler.Signal
                            sessionId
                            parentSessionIdOpt)

            // LOOP-002/006 and HOST-027 share one raw Host subscription but own
            // disjoint stream fields. Both abort physically; only their typed armed
            // marks decide the later reconciled-turn meaning.
            let loopSensor =
                LoopSensor.create scope.Sessions.OwnedSessions scope.Sessions.SessionParents (fun sessionId ->
                    sessionPort.InterruptAttempt sessionId)

            do scope.AttachLoopSensor loopSensor

            let needHelpSensor =
                NeedHelpSensor(
                    NeedHelpSensor.createEligibilityPredicate
                        scope.Sessions.OwnedSessions
                        scope.Sessions.SessionParents
                        journal
                        scope.Strength.StrengthRuntime,
                    (fun sessionId -> sessionPort.InterruptAttempt sessionId)
                )

            let needHelpSensor =
                AssistanceHostWiring.install needHelpSensor sessionPort journal snapshot scope

            let signalRouter =
                HostSignalRouter(
                    scope.Sessions.OwnedSessions,
                    onSignal,
                    onLoopEvent = loopSensor.Observe,
                    onNeedHelpEvent = needHelpSensor.Observe,
                    onProviderStepEnd =
                        (fun sessionId physicalUserMessageId providerRun ->
                            ModelRouting.endProviderStep sessionId physicalUserMessageId providerRun),
                    onPhysicalExecutionEnd =
                        (fun sessionId physicalUserMessageId ->
                            // EMR-007 owns capacity release. Reconciliation
                            // observes the same exact terminal assistant edge as
                            // projection visibility only; it remains outside the
                            // HostSignal/business vocabulary.
                            ModelRouting.releasePhysicalExecution sessionId physicalUserMessageId
                            reconciler.NotifyProjectionChanged(sessionId, physicalUserMessageId)

                            // INTRA-PARTICIPANT-PARALLELISM-009: OpenCode can
                            // complete a prompt_async physical execution without
                            // delivering the later session.status/session.idle
                            // event to the plugin transport. A Fission lane cannot
                            // rely on that lossy coarse wake because its ordinary
                            // terminal is the only route into lane materialization
                            // and eventual takeover of the old logical owner.
                            //
                            // This exact physical-terminal edge still carries no
                            // business outcome. It only opens one snapshot
                            // reconciliation occasion for a durable Fission lane;
                            // Completed/Failed/Aborted remains classified from the
                            // full SDK message snapshot in ReconcilePass. Recording
                            // the projection edge above before Kick also preserves
                            // Scheduler's edge-epoch lost-wakeup protection if the
                            // SDK snapshot is briefly behind the event stream.
                            FissionHost.observePhysicalExecutionEnd
                                reconciler.TryPhysicalUserMessage
                                journal
                                (fun sid -> reconciler.Kick(sid, ReconcileProgram.ReconcileWake.RetryWake))
                                sessionId
                                physicalUserMessageId)
                )

            let! subscriptionResult = HostSignalSubscribe.trySubscribe input signalRouter.Observe None

            let subscription: IDisposable option =
                match subscriptionResult with
                | Error err ->
                    Diagnostic.fatal "signal-subscribe-failed" [ "result", err ]
                    raise (InvalidOperationException err)

                | Ok(sub, _source) ->
                    // TrackSubscription only needs IDisposable; Health stays on the
                    // subscription record for future recovery consumers.
                    sub
                    |> Option.map (fun s ->
                        { new IDisposable with
                            member _.Dispose() = s.Dispose() })

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
                        |> Option.bind (fun profile ->
                            profile.CanonicalRole
                            |> PromptAuthority.roleLabel
                            |> AgentRoleIdentity.roleOfString)

                    reconciler.BindUserMessage(sid, physical, ?agentRole = agentRole)
                    scope.Sessions.AbortedSessions.Remove sessionId |> ignore
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

            let reactivateBlogger mainSessionId durable root =
                let associations = (AgentJournal.snapshot durable).AgentProjections.Associations

                match SessionAssociationProjection.tryBloggerOf mainSessionId associations with
                | None -> ()
                | Some bloggerId -> BloggerCoordinator.reactivateAfterNewRoot scope.ParkedTransformHost bloggerId root

            let onAuthorityRoot (mainSessionId: SessionId, root: AuthorityRootUserMessageId) =
                match journal with
                | None -> ()
                | Some durable -> reactivateBlogger mainSessionId durable root

            let promptIngressHook =
                PromptIngress.createHook
                    journal
                    bindUserMessage
                    bindContinuationMessage
                    registerOwned
                    (Some onAuthorityRoot)

            let durabilityActivation =
                lazy
                    (match workspaceDirectory with
                     | None -> Ok()
                     | Some workspace -> HookDispatcher.ensure workspace)

            let requireDurabilityActivation () =
                match durabilityActivation.Value with
                | Ok() -> ()
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

            let continueRoutedChatMessage routedExecution (decoded: PromptIngressCodec.DecodedMessage) input output =
                task {
                    ModelRouting.projectRoutedModel output routedExecution
                    FissionHostRequestProjection.projectRouted hasPhysicalParent routedExecution output

                    requireDurabilityActivation ()
                    JoinWake.observeChatMessage scope.Sessions.JoinInterrupts decoded
                    do! promptIngressHook input output

                    let dispatchAccepted =
                        journal
                        |> Option.map (fun durable ->
                            let runtime = PromptDispatcher.forJournal durable

                            fun (claim: PromptAuthority.PromptClaim) ->
                                runtime.DispatchAccepted(claim.SessionId, claim))

                    SessionExecutionBinding.acceptRoutedExecution dispatchAccepted routedExecution
                }

            let continueOrdinaryChatMessage (decoded: PromptIngressCodec.DecodedMessage) input output =
                task {
                    let tryPendingClaim j sid key =
                        j
                        |> Option.bind (fun durable -> (PromptDispatcher.forJournal durable).PendingClaim(sid, key))

                    let admission =
                        ModelRouting.chatExecutionAdmission
                            tryPendingClaim
                            journal
                            decoded.IsHostCompaction
                            decoded.SessionId
                            decoded.PhysicalUserMessageId
                            decoded.PromptKey
                            decoded.ExplicitAgent

                    let! routedExecution =
                        ModelRouting.routeChatExecution
                            (fun sessionId ->
                                scope.Sessions.ModelRoutingSessions.Add(SessionId.value sessionId) |> ignore)
                            SessionExecutionBinding.observeUserFacingAgent
                            admission

                    match routedExecution with
                    | ModelRouting.RoutedChatExecution.Superseded -> ()
                    | _ -> do! continueRoutedChatMessage routedExecution decoded input output
                }

            let chatMessageHook =
                fun (input: obj) (output: obj) ->
                    task {
                        // Decode once up front so Host compaction stays entirely outside
                        // execution-model-routing. The authority path re-decodes inside
                        // createHook; this local value only gates routing + join wake.
                        let decoded = PromptIngressCodec.decode input output

                        match decoded.SessionId with
                        | Some sessionId -> do! ensurePhysicalParentDiscovered sessionId
                        | None -> ()

                        // INTRA-PARTICIPANT-PARALLELISM-013: request-local origin
                        // narrowing is independent of business-root admission. In
                        // particular, CRASH-018 explicit /continue still performs a
                        // provider turn even though it deliberately skips PromptIngress.
                        FissionHostRequestProjection.projectExternalManaged hasPhysicalParent decoded output

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
                            // EMR-009 / PROMPT-006: route from typed authority evidence.
                            // Keyless external roots use their explicit managed agent;
                            // plugin prompts use PendingClaim.EffectiveAgent. Host message
                            // agent/model fields are never authority for a continuation.
                            do! continueOrdinaryChatMessage decoded input output
                    }

            let cancelSignals (ids: SessionId seq) =
                ids
                |> Seq.iter (fun id ->
                    scope.LoopSensor.DropSession id
                    scope.NeedHelpSensor.DropSession id
                    scope.DropAssistanceSignals id
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
                        SyncDelegateHostObservation.observe scope.SyncDelegateRuntime raw
                        MessageVisibilitySignal.observeEvent messageVisibility raw
                        signalRouter.ObserveLocal raw) }
        }
