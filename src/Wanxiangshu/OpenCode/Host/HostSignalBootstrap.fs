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
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode

module HostSignalBootstrap =

    [<RequireQualifiedAccess>]
    type private ChatExecutionAdmission =
        | NoRoute
        | ExternalManaged of SessionId * string
        | PluginManaged of PromptDispatcher.Runtime * PromptAuthority.PromptClaim * string
        | Rejected of string

    [<RequireQualifiedAccess>]
    type private RoutedChatExecution =
        | NoRoute
        | ExternalManaged of SessionId * string * OpencodeModel
        | PluginManaged of PromptDispatcher.Runtime * PromptAuthority.PromptClaim * string * OpencodeModel

    let private managedAgent (value: string option) =
        value
        |> Option.map (fun agent -> agent.Trim())
        |> Option.filter (fun agent ->
            not (String.IsNullOrWhiteSpace agent) && (ManagedAgent.requiredNames |> List.contains agent))

    let private outputSessionId (output: obj) =
        let message = if isNull output then null else output?message

        if isNull message || isNull message?sessionID then
            None
        else
            string message?sessionID |> fun value -> value.Trim() |> SessionId.create |> Some

    let private externalAdmission sessionId explicitAgent =
        match managedAgent explicitAgent with
        | Some agent -> ChatExecutionAdmission.ExternalManaged(sessionId, agent)
        | None -> ChatExecutionAdmission.NoRoute

    let private pluginAdmission journal sessionId promptKey =
        result {
            let! durable = journal |> Result.requireSome "PROMPT-006: plugin prompt has no durable authority journal"
            let runtime = PromptDispatcher.forJournal durable

            let! claim =
                runtime.PendingClaim(sessionId, promptKey)
                |> Result.requireSome (
                    sprintf "PROMPT-006: PromptKey %s has no pending execution claim" (PromptKey.value promptKey)
                )

            let! agent =
                managedAgent claim.EffectiveAgent
                |> Result.requireSome (
                    sprintf "PROMPT-006: PromptKey %s has no managed EffectiveAgent" (PromptKey.value promptKey)
                )

            return ChatExecutionAdmission.PluginManaged(runtime, claim, agent)
        }
        |> function
            | Ok admission -> admission
            | Error error -> ChatExecutionAdmission.Rejected error

    let private chatExecutionAdmission journal (decoded: PromptIngressCodec.DecodedMessage) output =
        let sessionId = decoded.SessionId |> Option.orElseWith (fun () -> outputSessionId output)

        match decoded.IsHostCompaction, sessionId, decoded.PromptKey with
        | true, _, _ -> ChatExecutionAdmission.NoRoute
        | false, Some sid, Some promptKey -> pluginAdmission journal sid promptKey
        | false, Some sid, None -> externalAdmission sid decoded.ExplicitAgent
        | _ -> ChatExecutionAdmission.NoRoute

    let private routeChatExecution (scope: PluginRuntimeScope) admission =
        task {
            match admission with
            | ChatExecutionAdmission.NoRoute -> return RoutedChatExecution.NoRoute
            | ChatExecutionAdmission.Rejected error -> return invalidOp error
            | ChatExecutionAdmission.ExternalManaged(sessionId, agent) ->
                let sessionText = SessionId.value sessionId
                scope.Sessions.ModelRoutingSessions.Add sessionText |> ignore
                SessionExecutionBinding.observeUserFacingAgent sessionId agent
                let! target = ModelRouting.acquireManaged sessionId agent
                return RoutedChatExecution.ExternalManaged(sessionId, agent, ModelRouting.toOpenCodeModel target)
            | ChatExecutionAdmission.PluginManaged(runtime, claim, agent) ->
                let sessionText = SessionId.value claim.SessionId
                scope.Sessions.ModelRoutingSessions.Add sessionText |> ignore
                let! target = ModelRouting.acquireManaged claim.SessionId agent
                return RoutedChatExecution.PluginManaged(runtime, claim, agent, ModelRouting.toOpenCodeModel target)
        }

    let private routedModel = function
        | RoutedChatExecution.NoRoute -> None
        | RoutedChatExecution.ExternalManaged(_, _, model)
        | RoutedChatExecution.PluginManaged(_, _, _, model) -> Some model

    let private projectRoutedModel output routed =
        match routedModel routed with
        | None -> ()
        | Some model ->
            let message = if isNull output then null else output?message
            if isNull message then invalidOp "EMR-009: managed chat.message routing has no mutable output.message"
            message?model <- box model

    let private acceptPluginExecution
        (runtime: PromptDispatcher.Runtime)
        (claim: PromptAuthority.PromptClaim)
        (agent: string)
        (model: OpencodeModel)
        =
        if runtime.DispatchAccepted(claim.SessionId, claim) then
            SessionExecutionBinding.acceptPromptExecution claim.SessionId claim.PromptKey agent model
        else
            invalidOp (
                sprintf "PROMPT-006: PromptKey %s did not reach durable PhysicalAccepted" (PromptKey.value claim.PromptKey)
            )

    let private commitExecutionCapability = function
        | RoutedChatExecution.PluginManaged(runtime, claim, agent, model) ->
            acceptPluginExecution runtime claim agent model
        | RoutedChatExecution.NoRoute
        | RoutedChatExecution.ExternalManaged _ -> ()

    let private eventString (value: obj) =
        if isNull value then
            None
        else
            let text = string value
            if String.IsNullOrWhiteSpace text then None else Some text

    let private observeRoleOfTool (runtime: SyncDelegateRuntime) owner messageId callId toolName =
        SyncDelegate.tryRoleOfToolName toolName
        |> Option.iter (fun role ->
            runtime.ObserveProviderToolCall(
                owner,
                ProviderRunIdentity.create messageId,
                role,
                ToolCallId.create callId
            ))

    let private observeToolCallIdentity (runtime: SyncDelegateRuntime) owner (part: obj) =
        match eventString part?messageID, eventString part?callID, eventString part?tool with
        | Some messageId, Some callId, Some toolName -> observeRoleOfTool runtime owner messageId callId toolName
        | _ -> ()

    let private observeToolPart (runtime: SyncDelegateRuntime) owner raw =
        let properties = raw?properties
        let part = if isNull properties then null else properties?part

        if not (isNull part) && eventString part?``type`` = Some "tool" then
            observeToolCallIdentity runtime owner part

    let private observeSyncDelegateEvent (runtime: SyncDelegateRuntime) (raw: obj) =
        match HostEventCodec.eventTypeOf raw, HostEventCodec.trySessionId raw with
        | "message.part.updated", Some owner -> observeToolPart runtime owner raw
        | _ -> ()

    let private observeSyncDelegateBatch (scope: PluginRuntimeScope) (rawInput: obj) =
        scope.SyncDelegateRuntime
        |> Option.iter (fun runtime -> observeSyncDelegateEvent runtime (HostEventCodec.unwrap rawInput))

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
                    // speculation/children here. Revoke the physical attempt's idle
                    // continuation capability so the retired conversation never continues.
                    if FissionRuntime.isSilentInterrupt sessionId then
                        scope.Sessions.Quiescence.RevokeCurrentAttempt sessionId
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

            let durabilityActivation =
                lazy
                    (match workspaceDirectory with
                     | None -> Ok()
                     | Some workspace -> HookDispatcher.ensure workspace)

            let chatMessageHook =
                fun (input: obj) (output: obj) ->
                    task {
                        // Decode once up front so Host compaction stays entirely outside
                        // execution-model-routing. The authority path re-decodes inside
                        // createHook; this local value only gates routing + join wake.
                        let decoded = PromptIngressCodec.decode input output

                        // EMR-009 / PROMPT-006: route from typed authority evidence.
                        // Keyless external roots use their explicit managed agent;
                        // plugin prompts use PendingClaim.EffectiveAgent. Host message
                        // agent/model fields are never authority for a continuation.
                        let admission = chatExecutionAdmission journal decoded output
                        let! routedExecution = routeChatExecution scope admission
                        projectRoutedModel output routedExecution

                        match durabilityActivation.Value with
                        | Ok() -> ()
                        | Error error -> Diagnostic.emit "durability-activation-failed" [ "result", error ]

                        // Signal even when mid-run UnknownOrigin — not only after
                        // AcceptHumanRoot. physical id + no PromptKey + not compaction
                        // → join wake.
                        match
                            decoded.SessionId,
                            decoded.PhysicalUserMessageId,
                            decoded.PromptKey,
                            decoded.IsHostCompaction
                        with
                        // An external user message interrupts ONLY the current active
                        // join attempts; with none active it is dropped as a join wake
                        // (the message itself stays in the normal Host queue). No future
                        // join is latched or woken by this older message (EXEC-017).
                        | Some sessionId, Some _, None, false ->
                            scope.Sessions.JoinInterrupts.SignalUserMessage sessionId
                        | _ -> ()

                        do! promptIngressHook input output
                        commitExecutionCapability routedExecution
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
                        observeSyncDelegateBatch scope raw
                        signalRouter.ObserveLocal raw)
                  PendingReviewSeals = SharedState.PendingReviewSeals }
        }
