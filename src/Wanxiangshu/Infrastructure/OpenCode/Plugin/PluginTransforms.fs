namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Domain.SessionRecovery
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Recovery
open Wanxiangshu.Session
open PluginHostInterop

module PluginTransforms =

    /// Provider-facing transform composition: order only.
    /// Strength replay/trace → StrengthReplay; speculation → StrengthSpeculate;
    /// narrative → ManagerNarrativeTransform; seal → ReviewSeal; replica fast path unchanged.
    let create (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : obj -> obj -> Task<unit> =
        let scope = boot.Scope
        let journal = boot.Journal
        let sessionPort = host.SessionPort
        let snapshotOpt = host.SnapshotOpt
        let strengthDurability = host.StrengthDurability
        let wired = host.Wired
        let workspaceDirectory = boot.WorkspaceDirectory
        // Boot carries the fuse as `string -> unit`; re-open it as 'a so the
        // fail-closed branches can type as whatever the enclosing match needs
        // (the raise never returns).
        let strengthFailClosed (reason: string) : 'a =
            boot.StrengthFailClosed reason
            raise (InvalidOperationException reason)

        let normalTransform (projectionSessionIdOpt: string option) (inObj: obj) (outObj: obj) : Task<unit> =
            task {
                // HOST-004：新 provider request 开始构建 → 旧 idle permit
                // 立即失效。必须在该 transform 的最早同步位置（任何 let!
                // 之前）调用，不得等 request 已运行才标 Running。
                projectionSessionIdOpt
                |> Option.iter (fun sessionId ->
                    scope.Sessions.Quiescence.BeginProviderAttempt(SessionId.create sessionId))

                let strengthReplayPlans =
                    match projectionSessionIdOpt with
                    | Some sessionId ->
                        StrengthReplay.applyBeforeXTrace journal strengthDurability strengthFailClosed sessionId outObj
                    | None -> []

                // COMPANION-003/007: keep the XTrace in step with the
                // provider-visible semantic projection at the transform
                // boundary — BEFORE the Companion rewrite and X-wire run,
                // so the ingest cursor maps against the trace that now
                // exists (not the previous round's mirror) and the XTrace
                // never absorbs synthetic heads (Companion memory / prefix
                // replacement) as raw parts.
                // Idempotent by (turn, part) provenance; a lagging trace
                // would stall BlogObservationCommitted.
                match projectionSessionIdOpt with
                | Some sessionId ->
                    let rawMessages = unbox<obj array> outObj?messages |> Array.toList
                    let capturedMessages = ProviderWireCapture.decodeCapturedMessageView rawMessages

                    let semantic =
                        ProviderWireCapture.wireMessageView capturedMessages
                        |> ProviderProjection.toSemantic

                    // COMPANION-003/007 + STRENGTH-008: new Host-runtime
                    // traces use stable Host-message identity so a promoted
                    // frame may be inserted before its not-yet-captured target
                    // output without renumbering already-captured history.
                    let sessionIdentity = SessionId.create sessionId

                    let stableMessageIds =
                        let ids = rawMessages |> List.map ProviderWireDecode.hostMessageId

                        if ids |> List.forall Option.isSome then
                            Some(ids |> List.map Option.get)
                        else
                            None

                    let traceState =
                        match stableMessageIds with
                        | Some ids when XTraceCapture.supportsStableInsertion journal sessionIdentity ->
                            match
                                XTraceCapture.captureMessageViewStable journal sessionIdentity ids capturedMessages
                            with
                            | Ok state -> state
                            | Error error -> strengthFailClosed error
                        | _ -> XTraceCapture.captureMessageView journal sessionIdentity capturedMessages

                    StrengthReplay.commitTracedAfterCapture
                        journal
                        strengthDurability
                        strengthFailClosed
                        traceState
                        strengthReplayPlans

                    traceState
                    |> Option.iter (fun updated ->
                        match scope.Sessions.Companions.TryGetValue sessionId with
                        | true, host -> host.RefreshXTrace updated
                        | false, _ -> ())

                    // GLORY-013: after durable X capture, before any further
                    // rewrite: open a Manager Life and rewrite the Birth /
                    // Reawakening narrative on the provider-facing transcript.
                    // Idempotent by (session, message, source) and by the Life
                    // projection itself; durable Opening stays the raw text.
                    match ManagerNarrativeTransform.tryTransform journal (Some sessionId) traceState rawMessages with
                    | Some rewritten -> HostMessageProjection.replaceMessagesInPlace outObj rewritten
                    | None -> ()

                    // GLORY-021: once the Activation continuation has been
                    // physically accepted, fix the compression floor at the
                    // XTrace head (just after the Activation prompt).
                    ManagerNarrativeTransform.applyAcceptedActivation journal (Some sessionId) traceState rawMessages
                | None -> ()

                do!
                    CompanionTransform.handleCompanionTransform
                        scope.Sessions.Companions
                        scope.Sessions.CompanionGate
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

                                // rabbit §13: Infrastructure closes Application fallback
                                // ledger + budget into the Session-facing admission capability.
                                let confirmedFailure: ConfirmedFailurePort =
                                    fun targetSessionId providerRun reason ->
                                        FallbackLedger.admitConfirmedFailure
                                            durable
                                            AgentPairCursor.DefaultAutoRecoveryBudget
                                            targetSessionId
                                            providerRun
                                            reason
                                        |> Result.map (function
                                            | Wanxiangshu.Recovery.RecoveryAdmission.ContinueRecovery ->
                                                Wanxiangshu.Session.RecoveryAdmission.ContinueRecovery
                                            | Wanxiangshu.Recovery.RecoveryAdmission.RecoveryExhausted ->
                                                Wanxiangshu.Session.RecoveryAdmission.RecoveryExhausted)

                                // ENFORCER-153: the recovery stage probe derives
                                // nudge/AABB state from durable claim + transcript.
                                let recoveryProbe
                                    (durable: AgentJournal)
                                    (sid: SessionId)
                                    (rawMessages: obj list)
                                    : EnforcerContinuation.RecoveryStageProbe =
                                    fun ctx ->
                                        let terminalRun =
                                            match EnforcerCycleDecode.lastAssistantStep rawMessages with
                                            | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) ->
                                                ProviderRunIdentity.create messageId
                                            | _ -> ProviderRunIdentity.create "unknown-prose-run"

                                        let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId ctx)

                                        BloggerRecoveryProbe.repairState durable sid requestKey terminalRun rawMessages

                                let! outcome =
                                    EnforcerHost.handleContinuation
                                        scope.ParkedTransformHost
                                        journal
                                        (Some repairNudge)
                                        (Some confirmedFailure)
                                        recoveryProbe
                                        sid
                                        bloggerMessages

                                match outcome with
                                | EnforcerContinuation.ContinuationOutcome.ProjectMessages messages ->
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
                                | EnforcerContinuation.ContinuationOutcome.StopPhysicalRun(messages, reason) ->
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

                // STRENGTH-009: freeze the post-Enforcer semantic view and
                // complete any eligible speculation before the Pair marker.
                // Prepared publication precedes Candidate visibility.
                do! StrengthSpeculate.tryApply snapshotOpt journal strengthDurability scope outObj

                // HOST-013：永久 pair-programming auto-injected。
                // XTrace 之后、ReviewSeal 之前。恢复 durable 历史 pair，
                // 再在全局末尾追加本次 tool-call + tool-result。
                // Companion / Blogger 整段跳过：结对编程约束干扰 blog 工具合同。
                let skipGuideline =
                    match journal, projectionSessionIdOpt with
                    | Some durable, Some sessionId ->
                        SessionAssociationProjection.isCompanion
                            (SessionId.create sessionId)
                            (AgentJournal.snapshot durable).AgentProjections.Associations
                    | _ -> false

                if not skipGuideline then
                    let messages = unbox<obj array> outObj?messages |> Array.toList

                    let guideline =
                        match projectionSessionIdOpt with
                        | Some sessionId ->
                            ProviderLanguageBinding.ensureRoot (SessionId.create sessionId)
                            |> ProjectionConstants.pairProgrammingGuidelineTextFor
                        | None -> ProjectionConstants.PairProgrammingGuidelineText

                    let markerText =
                        match journal, projectionSessionIdOpt with
                        | Some durable, Some sessionId ->
                            // Main session id: resolveTipGuidance maps owner via association.
                            match EnforcerTipGuidance.latestTipGuidance durable (SessionId.create sessionId) with
                            | Some guidance -> guidance + "\n\n" + guideline
                            | None -> guideline
                        | _ -> guideline

                    match
                        PairProgrammingThoughtTransform.tryInject journal projectionSessionIdOpt markerText messages
                    with
                    | Ok newMessages -> HostMessageProjection.replaceMessagesInPlace outObj newMessages
                    | Error reason ->
                        // HOST-013 fail closed：synthetic 历史无法按 durable anchor
                        // 字节级重建时，禁止把 raw transcript 原样发给 provider
                        // （那会静默破坏 append-only prefix）。中止本次物理 run。
                        Diagnostic.emit
                            "host-013-fail-closed"
                            [ "session_id", (defaultArg projectionSessionIdOpt ""); "result", reason ]

                        match projectionSessionIdOpt with
                        | Some sessionId ->
                            let! _ = sessionPort.AbortSession(SessionId.create sessionId)
                            ()
                        | None -> ()

                // HOST-016: 对 provider-facing 消息做非空 content 兜底保障，
                // 避免仅推理/空 content 导致上游 API 报 400 messages[i].content cannot be empty。
                let currentMessages = unbox<obj array> outObj?messages |> Array.toList
                let sanitized = HostMessageProjection.sanitizeMessages currentMessages
                HostMessageProjection.replaceMessagesInPlace outObj sanitized

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
                            (ProviderWireCapture.decodeMessageView rawMessages)
                            (ProviderWireCapture.lastUserMessageId rawMessages)
                            wired.PendingReviewSeals

                do! sealTask
                ()
            }

        let transform (inObj: obj) (outObj: obj) : Task<unit> =
            task {
                let projectionSessionIdOpt = projectionSessionIdFromMessages outObj

                projectionSessionIdOpt |> Option.iter wired.RegisterOwned

                let strengthReplica =
                    match projectionSessionIdOpt, scope.Strength.StrengthReplicaRuntime with
                    | Some sessionId, Some runtime when runtime.IsReplica(SessionId.create sessionId) -> Some runtime
                    | _ -> None

                match strengthReplica with
                | Some runtime ->
                    // STRENGTH-004/009: Replica uses exactly one request-plan
                    // writer plus its mirror/K gate. XTrace, Manager narrative,
                    // Companion, Enforcer, Pair and Review are owner-only.
                    do! XWire.applyTransform snapshotOpt journal scope outObj
                    let! handled = runtime.HandleTransform outObj

                    if not handled then
                        raise (InvalidOperationException "StrengthReplica transform lost its live decision binding")

                    let currentMessages = unbox<obj array> outObj?messages |> Array.toList
                    let sanitized = HostMessageProjection.sanitizeMessages currentMessages
                    HostMessageProjection.replaceMessagesInPlace outObj sanitized
                | None -> do! normalTransform projectionSessionIdOpt inObj outObj
            }

        transform
