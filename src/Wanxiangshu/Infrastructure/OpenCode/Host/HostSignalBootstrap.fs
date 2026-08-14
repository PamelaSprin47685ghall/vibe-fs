namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal
open Wanxiangshu.Process
open Wanxiangshu.Session

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
        {
            RegisterOwned: string -> unit
            CancelSignals: SessionId seq -> unit
            BindActiveRun: SessionId -> Role -> string option -> unit
            CurrentPhysicalUserMessage: string -> string option
            ChatMessageHook: obj
            ObserveEvent: obj -> unit
            /// REVIEW-010 deferred-binding park: challenge requests have no
            /// assistant to bind at transform time; VerdictTool bindToRun resolves
            /// the parked seal evidence against the tool ProviderRunId (fail-closed).
            PendingReviewSeals: Dictionary<string, SharedState.PendingSeal>
        }

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

            let reviewerContinuationPort =
                HostReviewGuard.continuationPort sessionPort journal scope.Sessions.NudgeSent

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
                    ?onSnapshot = Some onSnapshot
                )

            /// FALLBACK-003: every Host signal is a wake and nothing else.
            ///
            /// `ProviderFailure` and `ProviderRetry` used to run their own writers here
            /// — a second and third writer of the durable cursor, each deciding from
            /// event fields whether an attempt had failed. Both are gone: the
            /// reconciled snapshot decides, and FallbackController performs the advance.
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
                    // speculation/children here. OrdinaryTurnWorkflow consumes the
                    // same typed mark and suppresses parent terminal publication.
                    if FissionRuntime.isSilentInterrupt sessionId then
                        reconciler.Signal signal
                    else
                        // HOST-027: an exact NEEDHELP armed mark means this abort was
                        // requested by Assistance itself. Do not revoke that attempt's
                        // idle right: the following SessionIdle is the causal proof that
                        // permits the deep consultation. Ordinary operator aborts remain
                        // fail-closed and revoke before reconciliation.
                        if not (scope.NeedHelpSensor.HasArmedSession sessionId) then
                            scope.Sessions.Quiescence.RevokeCurrentAttempt sessionId

                        scope.Strength.StrengthReplicaRuntime
                        |> Option.iter (fun runtime -> runtime.CancelOwner sessionId |> ignore)

                        reconciler.Signal signal
                | SessionDeleted(sessionId, parentSessionIdOpt) ->
                    (emitJsExpr
                        (HostSessionDeletion.handle
                            scope
                            workspaceDirectory
                            finalizeInspector
                            cleanupInspectorDraft
                            reconciler.Signal
                            sessionId
                            parentSessionIdOpt)
                        "$0"
                    : unit)

            // LOOP-002/006 and HOST-027 share one raw Host subscription but own
            // disjoint stream fields. Both abort physically; only their typed armed
            // marks decide the later reconciled-turn meaning.
            let isOwned sessionId =
                scope.Sessions.OwnedSessions.Contains(SessionId.value sessionId)

            let isNeedHelpEligible sessionId =
                if not (isOwned sessionId) then
                    false
                elif scope.Strength.StrengthRuntime.TryFindByReplica sessionId |> Option.isSome then
                    false
                else
                    let companion =
                        journal
                        |> Option.exists (fun durable ->
                            SessionAssociationProjection.isCompanion
                                sessionId
                                (AgentJournal.snapshot durable).AgentProjections.Associations)

                    if companion then
                        false
                    else
                        match HostSessionNudge.tryActiveProfile journal sessionId with
                        | Some profile ->
                            match profile.CanonicalRole with
                            | Role.Blogger
                            | Role.Distiller -> false
                            | _ -> true
                        | None -> false

            let loopSensor =
                LoopSensor(isOwned, (fun sessionId -> sessionPort.AbortSession sessionId))

            let needHelpSensor =
                NeedHelpSensor(isNeedHelpEligible, (fun sessionId -> sessionPort.AbortSession sessionId))

            do scope.AttachLoopSensor loopSensor
            do scope.AttachNeedHelpSensor needHelpSensor

            let assistance =
                AssistanceHost(
                    sessionPort,
                    journal,
                    needHelpSensor,
                    snapshot,
                    (fun childId -> scope.Sessions.OwnedSessions.Add(SessionId.value childId) |> ignore)
                )

            do scope.AttachAssistance(assistance.HandleTurn, assistance.DropSignals, assistance.DropSession)

            let signalRouter =
                HostSignalRouter(
                    scope.Sessions.OwnedSessions,
                    onSignal,
                    onLoopEvent =
                        (fun raw ->
                            if not (needHelpSensor.IsReasoningDelta raw) then
                                loopSensor.Observe raw),
                    onNeedHelpEvent = needHelpSensor.Observe
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
            do! assistance.Recover()

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


            match journal with
            | None -> ()
            | Some durable ->
                do!
                    FissionHost.recoverGroups
                        sessionPort
                        eventPort
                        durable
                        (fun sessionId -> registerOwned (SessionId.value sessionId))
                        bindActiveRun
                        (fun _ -> workspaceDirectory)

            let onAuthorityRoot (mainSessionId: SessionId, root: AuthorityRootUserMessageId) =
                match journal with
                | None -> ()
                | Some durable ->
                    let associations = (AgentJournal.snapshot durable).AgentProjections.Associations

                    match SessionAssociationProjection.tryBloggerOf mainSessionId associations with
                    | None -> ()
                    | Some bloggerId ->
                        BloggerCoordinator.reactivateAfterNewRoot scope.ParkedTransformHost bloggerId root

            let promptIngressHook =
                PromptIngress.createHook
                    journal
                    bindUserMessage
                    bindContinuationMessage
                    registerOwned
                    (Some onAuthorityRoot)

            let chatMessageHook =
                fun (input: obj) (output: obj) ->
                    // Decode once for join wake; authority path re-decodes inside
                    // createHook (PROMPT-004 fail-closed unchanged). Signal even when
                    // mid-run UnknownOrigin — not only after AcceptHumanRoot.
                    // physical id + no PromptKey + not compaction → join wake.
                    let decoded = PromptIngressCodec.decode input output

                    match
                        decoded.SessionId, decoded.PhysicalUserMessageId, decoded.PromptKey, decoded.IsHostCompaction
                    with
                    // An external user message interrupts ONLY the current active
                    // join attempts; with none active it is dropped as a join wake
                    // (the message itself stays in the normal Host queue). No future
                    // join is latched or woken by this older message (EXEC-017).
                    | Some sessionId, Some _, None, false -> scope.Sessions.JoinInterrupts.SignalUserMessage sessionId
                    | _ -> ()

                    promptIngressHook input output

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
                  ObserveEvent = signalRouter.ObserveLocal
                  PendingReviewSeals = SharedState.PendingReviewSeals }
        }
