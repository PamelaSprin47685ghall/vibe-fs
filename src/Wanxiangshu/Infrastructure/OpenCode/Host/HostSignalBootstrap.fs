namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
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
                HostReviewGuard.continuationPort sessionPort journal scope.NudgeSent

            let resolveProjection (sessionId: SessionId) : AgentProjectionSet option =
                match journal with
                | None -> None
                | Some j -> Some((AgentJournal.snapshot j).AgentProjections)

            let binding = TurnBinding.Store()

            let onTurn (context: ReconciledTurnContext) : Task =
                task {
                    let turn = context.Turn

                    let strengthHandled =
                        scope.Strength.StrengthReplicaRuntime
                        |> Option.exists (fun runtime -> runtime.HandleTurn turn)

                    if strengthHandled then
                        // STRENGTH-004/011: Replica observations are leaf-local. They
                        // only reconcile the request plan for cleanup; family recovery,
                        // owner fallback, Companion, Review and ordinary TurnWorkflow
                        // must never observe them.
                        XWire.reconcileAttempt journal scope turn
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
                            match durability.LoadProjection() with
                            | Error error ->
                                let reason = "Strength promotion projection failed: " + error
                                scope.Strength.TripStrengthFuse reason
                                raise (InvalidOperationException reason)
                            | Ok projection ->
                                match StrengthLifecycle.reconcileEvent projection turn with
                                | None -> ()
                                | Some event ->
                                    match durability.Append event with
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
                                for KeyValue(_, companion) in scope.Companions do
                                    match companion.BloggerSession with
                                    | Some bloggerId when bloggerId = turn.SessionId ->
                                        companion.StartRecoveryOpportunity() |> ignore
                                    | _ -> ()
                            | ReconcileProgram.TurnCompleted
                            | ReconcileProgram.TurnNeedsContinuation _
                            | ReconcileProgram.TurnInProgress -> ()


                            XWire.reconcileAttempt journal scope turn
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
                                    scope.NudgeSent
                                    scope.JoinGuardNudges
                                    scope.HasLivePty
                                    scope.AbortedSessions
                                    (Some scope.LoopSensor)
                                    scope.Quiescence
                                    context
                }

            /// HOST-006 containment: observe every reconciled snapshot for compaction
            /// pseudo-runs and reanchor at most one per pass.
            ///
            /// Wired here rather than inside `onTurn` because a compaction pseudo-run
            /// belongs to no Logical Run of ours — a manual `/compact` produces one with no
            /// active root at all — so a turn-shaped callback would never see it.
            ///
            /// No journal means no durable epoch and nothing to reanchor. Silent rather
            /// than an error: a journal-less run has no PrefixEpoch to retire, so there is
            /// no state that could drift.
            let onSnapshot (sessionId: SessionId) (messages: SessionMessage list) : Task =
                task {
                    // RECOVERY-FAMILY: family recovery before compaction probe effects.
                    let! recovery = scope.EnsureRecoveryDone sessionId

                    match recovery with
                    | FamilyRecovery.FamilyBlocked _ -> return ()
                    | FamilyRecovery.FamilyWaiting _
                    | FamilyRecovery.FamilyReady _ -> ()

                    // HOST-006 prevention layer's second half: the runtime probe.
                    //
                    // Judged before containment, and once per plugin instance. If the Host
                    // compacts outside the configuration the plugin can reach, the correct
                    // response is to refuse to run — not to reanchor and carry on, which would
                    // hide the condition behind behaviour that looks correct.
                    if scope.IsStartupProbeOpen then
                        match HostCompactionGate.judgeStartup scope.CompactionSettingGap sessionId messages with
                        | None -> () // Not a completed first turn yet; the probe stays armed.
                        | Some verdict ->
                            if scope.TryClaimStartupProbe() then
                                match verdict with
                                | CompactionGateVerdict.Satisfied -> ()
                                | failed ->
                                    raise (InvalidOperationException(HostCompactionPolicy.describeVerdict failed))

                    match journal with
                    | None -> ()
                    | Some durable ->
                        let observed =
                            messages
                            |> List.filter (fun message ->
                                HostCompactionPolicy.isContainableCompaction message.IsCompaction)
                            |> List.map (fun message -> ProviderRunIdentity.create message.Id)

                        if List.isEmpty observed then
                            ()
                        else
                            match HostCompactionGate.reanchorObserved durable sessionId observed with
                            | Ok None
                            | Ok(Some _) -> ()
                            // A failed append here is not fatal to the turn that just
                            // completed. PERSIST-003's fail-closed path already owns a poisoned
                            // journal; what this must not do is throw inside the reconcile loop
                            // and leave `Running` set, which would stop every later pass for
                            // this session.
                            | Error reason ->
                                HostCompactionGate.logReanchorFailure sessionId reason
                                ()

                }

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
                    let permit = scope.Quiescence.ObserveIdle sessionId
                    reconciler.SignalIdle(sessionId, permit)
                | ProviderRetry _
                | ProviderFailure _ -> reconciler.Signal signal
                // HOST-002/004: operator abort immediately revokes the current
                // attempt's idle permits, then routes to the
                // reconciler. Never ProviderFailure — it does not advance fallback.
                | AttemptAborted sessionId ->
                    scope.Quiescence.RevokeCurrentAttempt sessionId

                    scope.Strength.StrengthReplicaRuntime
                    |> Option.iter (fun runtime -> runtime.CancelOwner sessionId |> ignore)

                    reconciler.Signal signal
                | SessionDeleted(sessionId, parentSessionIdOpt) ->
                    scope.LoopSensor.DropSession sessionId

                    // STRENGTH-004/011: owner deletion cancels the decision-local
                    // InternalLeaf immediately. CancelOwner completes the waiting
                    // decision before its best-effort physical abort, so no deleted
                    // owner can keep a Replica eligible for later collection.
                    scope.Strength.StrengthReplicaRuntime
                    |> Option.iter (fun runtime -> runtime.CancelOwner sessionId |> ignore)

                    // OpenCode recursively emits child SessionDeleted before the owner
                    // SessionDeleted. An attached Inspector child must retire its live
                    // binding without clearing the Casebook draft; the later owner
                    // event is the graceful ReuseScope-close signal that finalizes it.
                    // A continued owner Invoke consumes the staged child as unexpected
                    // deletion and cleans its draft instead of reusing the dead child.
                    let finished =
                        task {
                            let stagedInspector =
                                match scope.SyncDelegateRuntime, parentSessionIdOpt with
                                | Some runtime, Some parentSessionId ->
                                    runtime.StageDeletedInspector(parentSessionId, sessionId)
                                | _ -> false

                            if not stagedInspector then
                                match scope.SyncDelegateRuntime with
                                | Some runtime ->
                                    match runtime.TryFindForScopeClose(sessionId, SyncDelegateRole.Inspector) with
                                    | Some inspectorId ->
                                        match workspaceDirectory with
                                        | Some root ->
                                            let! _ = finalizeInspector root (SessionId.value inspectorId)
                                            ()
                                        | None -> ()
                                    | None -> ()

                                    runtime.CancelSession sessionId
                                | None -> ()

                                // Residual draft if the deleted id itself held Inspector Q/A.
                                cleanupInspectorDraft (SessionId.value sessionId)

                            scope.Quiescence.DropSession sessionId
                            scope.DisposeSession(SessionId.value sessionId)
                            reconciler.Signal signal
                        }

                    (emitJsExpr finished "$0": unit)

            // LOOP-002/006: edge sensor shares the same event subscription, aborts via
            // the session port, and leaves AABB to OrdinaryTurnWorkflow on TurnAborted.
            let loopSensor =
                LoopSensor(
                    (fun sessionId -> scope.OwnedSessions.Contains(SessionId.value sessionId)),
                    (fun sessionId -> sessionPort.AbortSession sessionId)
                )

            do scope.AttachLoopSensor loopSensor

            let signalRouter =
                HostSignalRouter(scope.OwnedSessions, onSignal, onLoopEvent = loopSensor.Observe)

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
                    scope.OwnedSessions.Add sessionId |> ignore
                    signalRouter.RegisterOwned(SessionId.create sessionId)

            let bindUserMessage (sessionId: string) (messageId: string) =
                if
                    not (String.IsNullOrWhiteSpace sessionId)
                    && not (String.IsNullOrWhiteSpace messageId)
                then
                    let sid = SessionId.create sessionId
                    let physical = PhysicalUserMessageId.create messageId
                    scope.UserMessageBindings.[sessionId] <- physical

                    let agentRole =
                        HostSessionNudge.tryActiveProfile journal sid
                        |> Option.bind (fun profile ->
                            profile.CanonicalRole
                            |> PromptAuthority.roleLabel
                            |> AgentRoleIdentity.roleOfString)

                    reconciler.BindUserMessage(sid, physical, ?agentRole = agentRole)
                    scope.AbortedSessions.Remove sessionId |> ignore
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
                    match scope.UserMessageBindings.TryGetValue key with
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
                    | Some sessionId, Some _, None, false -> scope.JoinInterrupts.SignalUserMessage sessionId
                    | _ -> ()

                    promptIngressHook input output

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
                  ObserveEvent = signalRouter.ObserveLocal
                  PendingReviewSeals = SharedState.PendingReviewSeals }
        }
