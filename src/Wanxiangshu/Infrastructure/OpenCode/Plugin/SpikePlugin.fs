namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session
open PluginHostInterop

module SpikePlugin =

    let initSpikePlugin (input: obj) : Task<obj> =
        task {
            // Fail-fast resource load before any consumer (StaticTools / BlogTool / EnforcerHost).
            RuntimeResources.install (RuntimeResources.load ())

            let portOpt = OpenCodePort.create input

            let journal =
                match PluginHost.createJournal input with
                | Ok value -> value
                | Error err -> raise (InvalidOperationException err)

            let scope = new PluginRuntimeScope(journal)

            PluginHost.restoreSessionParents journal scope.SessionParents

            let familyParent (sessionId: SessionId) =
                match scope.SessionParents.TryGetValue(SessionId.value sessionId) with
                | true, parentId -> Some(SessionId.create parentId)
                | false, _ -> None

            match PluginHost.createHost input portOpt (Some familyParent) with
            | Error err -> return raise (InvalidOperationException err)
            | Ok(eventPort, sessionPort, snapshotOpt, terminalKey, sharedTerminalPort) ->
                scope.AttachSharedTerminal(terminalKey, sharedTerminalPort)

                for KeyValue(childId, parentId) in scope.SessionParents do
                    scope.OwnedSessions.Add childId |> ignore
                    scope.OwnedSessions.Add parentId |> ignore

                let gitTreePort =
                    match PluginHost.gitTreePortFromInput input with
                    | Some port -> Some port
                    | None -> PluginHost.workspaceDirectory input |> Option.map GitTree.create

                // The stable workspace, captured once at plugin init. The transform
                // input carries no directory; the blogger must be pinned to this
                // path (not the manager worktree) so its system prompt survives the
                // worktree release at publish. First boot wins: the main workspace
                // instance starts before the manager worktree instances.
                let workspaceDirectory = PluginHost.workspaceDirectory input

                if SharedState.RootWorkspace.IsNone then
                    SharedState.RootWorkspace <- workspaceDirectory

                let! wired = HostSignalBootstrap.wire sessionPort eventPort snapshotOpt journal gitTreePort scope input

                // GREEN-4: mandatory SessionRecoveryPorts. Real RestoreHandles/RecoverJobs.
                // Missing journal or snapshot → leave ports unattached (RequireFamilyRecovery
                // → FamilyBlocked RecoveryCoordinatorUnavailable). Never attach None ports
                // that collapse to NoRecoveryRequired.
                match journal, snapshotOpt with
                | Some durable, Some snapshot ->
                    let restoreHandles (sessionId: SessionId) : Task<HandleFamilyRecovery> =
                        HostForkRestart.restoreLinkedChildrenWithoutRuntime snapshot durable sessionId

                    let recoverJobs (sessionId: SessionId) : Task<JobFamilyRecovery> =
                        task {
                            let orch = (AgentJournal.snapshot durable).AgentProjections.Orchestrator
                            // Session-scoped: jobs whose ManagerSessionId matches, or any
                            // active job when session is orchestrator root with active set.
                            let related =
                                OrchestratorProjection.activeJobs orch
                                |> List.filter (fun job ->
                                    job.ManagerSessionId = sessionId
                                    || SessionId.value job.ManagerSessionId = SessionId.value sessionId)

                            match NonEmpty.ofList (related |> List.map (fun j -> j.ManagerJobId)) with
                            | None -> return JobFamilyRecovery.NoRelatedJobs
                            | Some ids -> return JobFamilyRecovery.JobsRecovered ids
                        }

                    scope.AttachFamilyRecoveryPorts(
                        { Journal = durable
                          Snapshot = snapshot
                          ParkedHost = scope :> IParkedTransformHost
                          RecoverPromptClaims = SessionRecoveryWorkflow.defaultRecoverPromptClaims durable snapshot
                          RecoverBlogger =
                            SessionRecoveryWorkflow.defaultRecoverBlogger
                                durable
                                (scope :> IParkedTransformHost)
                                snapshot
                          RestoreHandles = restoreHandles
                          RecoverJobs = recoverJobs }
                    )
                | _ -> ()

                let transform inObj outObj : Task<unit> =
                    task {
                        let projectionSessionIdOpt = projectionSessionIdFromMessages outObj

                        projectionSessionIdOpt |> Option.iter wired.RegisterOwned

                        // COMPANION-003/007: keep the XTrace in step with the
                        // provider-visible semantic projection at the transform
                        // boundary — BEFORE the Companion rewrite and X-wire run,
                        // so the ingest cursor maps against the trace that now
                        // exists (not the previous round's mirror) and the XTrace
                        // never absorbs synthetic heads (Companion memory / prefix
                        // replacement) as raw parts.
                        // Idempotent by (turn, part) provenance; a lagging trace
                        // would stall BlogEntryCommitted.
                        match projectionSessionIdOpt with
                        | Some sessionId ->
                            let rawMessages = unbox<obj array> outObj?messages |> Array.toList

                            let semantic =
                                Projection.decodeMessageView rawMessages |> ProviderProjection.toSemantic

                            XTraceCapture.captureProjection journal (SessionId.create sessionId) semantic
                            |> Option.iter (fun updated ->
                                // COMPANION-007: refresh the in-memory mirror so the
                                // chunker maps the ingest cursor against the trace
                                // that now exists, not the empty one from bootstrap.
                                match scope.Companions.TryGetValue sessionId with
                                | true, host -> host.RefreshXTrace updated
                                | false, _ -> ())
                        | None -> ()

                        do!
                            CompanionTransform.handleCompanionTransform
                                scope.Companions
                                scope.CompanionGate
                                scope
                                sessionPort
                                journal
                                (Some(fun bloggerId ->
                                    // Register ownership + ActiveRun so idle→reconcile
                                    // emits TerminalOutcome.Completed for this child.
                                    wired.RegisterOwned(SessionId.value bloggerId)
                                    wired.BindActiveRun bloggerId Role.Blogger None))
                                SharedState.RootWorkspace
                                inObj
                                outObj

                        do! XWire.applyTransform snapshotOpt journal scope outObj

                        // docs/what/enforcer.md ENFORCER-044/047/050: Blogger continuation only.
                        // Main-session material is decided once in
                        // CompanionTransform → BloggerCoordinator.onMainMaterial.
                        match projectionSessionIdOpt with
                        | Some sessionId ->
                            let sid = SessionId.create sessionId

                            match journal with
                            | Some durable ->
                                let associations = (AgentJournal.snapshot durable).AgentProjections.Associations

                                match SessionAssociationProjection.tryMainSessionOf sid associations with
                                | Some _ ->
                                    // RECOVERY-FAMILY: family recovery before blogger continuation effects.
                                    let! recovery = scope.EnsureRecoveryDone sid

                                    match recovery with
                                    | FamilyRecovery.FamilyBlocked _ -> ()
                                    | FamilyRecovery.FamilyWaiting _
                                    | FamilyRecovery.FamilyReady _ ->
                                        let bloggerMessages = unbox<obj array> outObj?messages |> Array.toList

                                        // InteractionRepair port: HostSessionNudge is compile-later;
                                        // EnforcerHost only sees InteractionRepairNudge (Session layer).
                                        let repairNudge: InteractionRepairNudge =
                                            HostSessionNudge.trySendInteractionRepair sessionPort

                                        // ENFORCER-153: the recovery stage probe derives
                                        // nudge/AABB state from durable claim + transcript.
                                        let recoveryProbe
                                            (durable: AgentJournal)
                                            (sid: SessionId)
                                            (rawMessages: obj list)
                                            : EnforcerHost.RecoveryStageProbe =
                                            fun ctx ->
                                                let terminalRun =
                                                    match EnforcerHost.lastAssistantStep rawMessages with
                                                    | Some(messageId, _, _) when
                                                        not (String.IsNullOrWhiteSpace messageId)
                                                        ->
                                                        ProviderRunIdentity.create messageId
                                                    | _ -> ProviderRunIdentity.create "unknown-prose-run"

                                                let requestKey =
                                                    BloggerRequestId.value (BloggerRequestContext.requestId ctx)

                                                BloggerRecoveryProbe.repairState
                                                    durable
                                                    sid
                                                    requestKey
                                                    terminalRun
                                                    rawMessages

                                        let! outcome =
                                            EnforcerHost.handleContinuation
                                                scope
                                                journal
                                                (Some repairNudge)
                                                recoveryProbe
                                                sid
                                                bloggerMessages

                                        match outcome with
                                        | EnforcerHost.ContinuationOutcome.ProjectMessages messages ->
                                            let projected =
                                                if List.isEmpty messages then
                                                    Diagnostic.emit
                                                        "enforcer-empty-project"
                                                        [ "session_id", sessionId
                                                          "result", "ProjectMessages empty; keep raw transcript" ]

                                                    bloggerMessages
                                                else
                                                    messages

                                            HostMessageProjection.replaceMessagesInPlace outObj projected
                                        | EnforcerHost.ContinuationOutcome.StopPhysicalRun(messages, reason) ->
                                            let projected = if List.isEmpty messages then bloggerMessages else messages

                                            HostMessageProjection.replaceMessagesInPlace outObj projected

                                            Diagnostic.emit
                                                "enforcer-stop-physical-run"
                                                [ "session_id", sessionId; "result", reason ]

                                            // Await abort so Host fiber interrupt is pending before
                                            // transform returns → handle.process / provider skipped.
                                            // Transform-initiated abort is not LoopSensor-armed → no
                                            // ProviderRetryAttempt (TurnAborted only).
                                            let! _ = sessionPort.AbortSession sid
                                            ()
                                | None -> ()
                            | None -> ()
                        | None -> ()

                        // HOST-013: the pair-programming thought marker. Runs
                        // after XTrace capture (the marker never enters the
                        // trace) and before ReviewSeal (the seal covers the
                        // final bytes the provider receives). Idempotent per
                        // anchor; no anchor → untouched.
                        let messages = unbox<obj array> outObj?messages |> Array.toList

                        match PairProgrammingThoughtTransform.tryInject projectionSessionIdOpt messages with
                        | Some newMessages -> HostMessageProjection.replaceMessagesInPlace outObj newMessages
                        | None -> ()

                        // REVIEW-010: seal LAST, and only after the Companion rewrite has
                        // mutated `outObj`. The seal must digest the message view the
                        // provider actually receives; sealing before the rewrite would
                        // record bytes the Host never sends.
                        //
                        // Host source awaits every hook in turn (`plugin/index.ts:280-292`),
                        // so returning a Task here makes the SDK read complete before the
                        // provider request is built.
                        let sealTask =
                            match projectionSessionIdOpt with
                            | None -> Task.FromResult()
                            | Some projectionSessionId ->
                                let rawMessages = unbox<obj array> outObj?messages |> Array.toList

                                ReviewSeal.sealTransform
                                    snapshotOpt
                                    journal
                                    (SessionId.create projectionSessionId)
                                    (Projection.decodeMessageView rawMessages)
                                    (Projection.lastUserMessageId rawMessages)
                                    wired.PendingReviewSeals

                        do! sealTask
                        ()
                    }

                let chatParams = ChatParamsHook.create journal

                // HOST-009: the object handed to the Host carries Host hooks and
                // nothing else.
                //
                // Six extra keys used to hang here — `projection`, `events`,
                // `sessions`, `journal`, `hostEventsSubscription`, `bindRunStarted`.
                // None is a hook name in the Host's `Hooks` type, so the Host never
                // read any of them; they were internal ports exposed for test
                // visibility, which is the one thing VERIFY-008 names as forbidden.
                // Two had no reader at all. Layer 1–3 tests reach these modules
                // directly through `dist`.
                let hooks =
                    createObj
                        [ "chat.message", box (curriedHook wired.ChatMessageHook)
                          // Host built-in retry reuses the same user message;
                          // 0.5.0 relies on Agent bindings — chat.params is a no-op.
                          "chat.params", box (curriedHook chatParams)
                          // ONE transform registration.
                          //
                          // Both `chat.transform` and this key used to point at the
                          // same function "for compatibility". Host source has only
                          // the experimental name — the other is absent from the
                          // `Hooks` type and triggered nowhere; `prompt.ts:1255` and
                          // `compaction.ts:350` are the only trigger sites. So the
                          // extra key was never a fallback; it was a second live
                          // registration of one hook, and every provider step ran the
                          // Companion rewrite and the REVIEW-010 seal twice over the
                          // same message array.
                          "experimental.chat.messages.transform", box (pairedHook (box transform))
                          // HOST-006 prevention layer. The config hook is the only
                          // place the plugin can reach the compaction settings: the
                          // Host hands over the live instance-state object and runs
                          // this before other services (`bootstrap.ts:36`), so a write
                          // here is in force before anything reads it.
                          //
                          // `enforceSettings` reports the first key it could not
                          // establish. That is carried to the startup probe rather than
                          // thrown here: HOST-006's verdict needs both halves — the
                          // settings AND the first turn — and failing at config time
                          // would report the symptom without the observation.
                          "config",
                          box (fun (config: obj) ->
                              ManagerConfig.configureManager config
                              scope.RecordCompactionSettingGap(HostCompactionGate.enforceSettings config))
                          // HOST-006: this hook cannot refuse a compaction — its output
                          // has no cancel field (`plugin/index.ts:305`) and
                          // `plugin.trigger` discards the return value. Registered
                          // anyway so the containment layer has a same-turn signal, and
                          // so the absence of a veto is documented at the boundary
                          // rather than inferred from silence.
                          "experimental.session.compacting",
                          box (pairedHook (box HostCompactionGate.onSessionCompacting))
                          // HOST-006: always `enabled = false`. `compaction.auto=false`
                          // already makes the replay branch unreachable, but this is the
                          // one vetoable synthetic-turn injection point, and leaving it
                          // unanswered relies on an upstream default staying harmless.
                          "experimental.compaction.autocontinue",
                          box (pairedHook (box HostCompactionGate.onCompactionAutoContinue)) ]

                hooks?event <- box wired.ObserveEvent

                // HOST-009 dispose: cancel owned Tasks, kill PTYs/processes, dispose
                // sessions. `scope.Dispose` owns all of it, and the Host awaits this
                // hook (`plugin/index.ts:266`), so teardown completes before shutdown
                // proceeds.
                hooks?dispose <- box (fun () -> scope.Dispose())

                let client = if isNull input then null else input?client

                if not (isNull client) then
                    try
                        let! toolModule = importToolModule ()

                        let onRunStarted =
                            Some(fun sessionId role directory -> wired.BindActiveRun sessionId role directory)

                        // EXEC-006 / EXEC-008: same LWR materialiser, direction-dependent Opening.
                        // parent → child: includeOpening=true；child → parent: false.
                        let parentWorkRecordFor =
                            Some(fun sessionId ->
                                XTraceCapture.lifecycleWorkRecord journal (SessionId.create sessionId) true)

                        let childWorkRecordFor =
                            Some(fun sessionId ->
                                XTraceCapture.lifecycleWorkRecord journal (SessionId.create sessionId) false)

                        let toolRegistration =
                            toolHooks
                                toolModule
                                sessionPort
                                journal
                                gitTreePort
                                (PluginHost.workspaceDirectory input)
                                scope
                                wired.CurrentPhysicalUserMessage
                                onRunStarted
                                parentWorkRecordFor
                                childWorkRecordFor
                                snapshotOpt
                                (Some wired.CancelSignals)
                                (Some eventPort)

                        scope.AttachToolRuntime(toolRegistration.Runtime :> ISessionRuntimeOwner)
                        hooks?tool <- toolRegistration.Tools
                    with ex ->
                        raise (InvalidOperationException(sprintf "Failed to load OpenCode tool module: %s" ex.Message))

                return box hooks
        }
