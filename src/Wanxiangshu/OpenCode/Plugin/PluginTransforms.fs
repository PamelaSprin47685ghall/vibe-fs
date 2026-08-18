namespace Wanxiangshu.OpenCode

#nowarn "3511"

open System
open System.Collections.Generic
open System.Threading.Tasks
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
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.Change
open Wanxiangshu.Change.Host
open Wanxiangshu.Context.Companion.Blogger.OpenCode
open Wanxiangshu.Enforcer
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Execution.Delegation.Handle.OpenCode
open Wanxiangshu.Execution.Delegation.OpenCode
open Wanxiangshu.Execution.Delegation.SyncDelegate.OpenCode
open Wanxiangshu.Execution.Fission.OpenCode
open Wanxiangshu.Execution.Session.OpenCode
open Wanxiangshu.Git
open Wanxiangshu.Git.Hook
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Finality.OpenCode
open Wanxiangshu.Mission.Manager.OpenCode
open Wanxiangshu.Mission.Obligation.Todo.OpenCode
open Wanxiangshu.Mission.Review.OpenCode
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.Semble
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Knowledge.Casebook.OpenCode
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Repository.Programming.Js.OpenCode
open Wanxiangshu.Resources
open Wanxiangshu.Strength.OpenCode
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session
open Wanxiangshu.Enforcer
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Resources
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
open PluginHostInterop

module PluginTransforms =

    let private raiseStrengthFailClosed (fuse: string -> unit) (reason: string) : 'a =
        fuse reason
        raise (InvalidOperationException reason)

    /// HOST-013: bind session start or abort + fail-closed.
    let private bindSessionStartedAtOrAbort
        (durable: AgentJournal)
        (sessionId: string)
        (candidate: DateTimeOffset)
        (sessionPort: ISessionHostPort)
        : Task<DateTimeOffset option> =
        task {
            match! SessionStartedAtLedger.bind durable (SessionId.create sessionId) candidate with
            | Ok startedAt -> return Some startedAt
            | Error reason ->
                Diagnostic.emit "host-013-session-start-bind-failed" [ "session_id", sessionId; "result", reason ]

                let! _ = sessionPort.AbortSession(SessionId.create sessionId)

                return raise (InvalidOperationException("HOST-013 SessionStartedAt bind failed: " + reason))
        }

    let private tryBindSessionStartedAt
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (sessionStartCandidate: DateTimeOffset option)
        (sessionPort: ISessionHostPort)
        : Task<DateTimeOffset option> =
        match journal, projectionSessionIdOpt, sessionStartCandidate with
        | Some durable, Some sessionId, Some candidate ->
            bindSessionStartedAtOrAbort durable sessionId candidate sessionPort
        | _ -> Task.FromResult None

    let private strengthReplayPlansFor
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailFuse: string -> unit)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        : Task<StrengthReplayPlan list> =
        match projectionSessionIdOpt with
        | Some sessionId ->
            StrengthReplay.applyBeforeXTrace
                journal
                strengthDurability
                (raiseStrengthFailClosed strengthFailFuse)
                sessionId
                outObj
        | None -> Task.FromResult []

    /// Strip AssistancePrompt sentinel from reasoning wire parts only.
    let private stripReasoningSentinel (part: ProviderWireCapture.CapturedWirePart) =
        match part.WirePart with
        | WireReasoning text ->
            { part with
                WirePart = WireReasoning(AssistancePrompt.stripSentinel text) }
        | WireText _
        | WireToolCall _
        | WireToolResult _
        | WireMedia _ -> part

    let private capturedMessagesStripped (rawMessages: obj list) =
        ProviderWireCapture.decodeCapturedMessageView rawMessages
        |> List.map (fun message ->
            { message with
                Parts = message.Parts |> List.map stripReasoningSentinel })

    /// Evidence → Decision: all Host message ids present → stable id list.
    let private tryStableHostMessageIds (rawMessages: obj list) : string list option =
        let ids = rawMessages |> List.map ProviderWireDecode.hostMessageId

        if ids |> List.forall Option.isSome then
            Some(ids |> List.map Option.get)
        else
            None

    let private captureStableOrFailClosed
        (journal: AgentJournal option)
        (sessionIdentity: SessionId)
        (ids: string list)
        (capturedMessages: ProviderWireCapture.CapturedWireMessage list)
        (strengthFailFuse: string -> unit)
        : Task<XTraceProjectionState option> =
        task {
            match! XTraceCapture.captureMessageViewStable journal sessionIdentity ids capturedMessages with
            | Ok state -> return state
            | Error error -> return raiseStrengthFailClosed strengthFailFuse error
        }

    let private captureTraceState
        (journal: AgentJournal option)
        (sessionIdentity: SessionId)
        (stableMessageIds: string list option)
        (capturedMessages: ProviderWireCapture.CapturedWireMessage list)
        (strengthFailFuse: string -> unit)
        : Task<XTraceProjectionState option> =
        match stableMessageIds with
        | Some ids when XTraceCapture.supportsStableInsertion journal sessionIdentity ->
            captureStableOrFailClosed journal sessionIdentity ids capturedMessages strengthFailFuse
        | _ -> XTraceCapture.captureMessageView journal sessionIdentity capturedMessages

    let private refreshCompanionXTrace
        (companions: Dictionary<string, CompanionHost>)
        (sessionId: string)
        (updated: XTraceProjectionState)
        =
        match companions.TryGetValue sessionId with
        | true, host -> host.RefreshXTrace updated
        | false, _ -> ()

    let private applyManagerNarrativeRewrite
        (journal: AgentJournal option)
        (sessionIdOpt: string option)
        (traceState: XTraceProjectionState option)
        (rawMessages: obj list)
        (outObj: obj)
        : Task =
        task {
            match! ManagerNarrativeTransform.tryTransform journal sessionIdOpt traceState rawMessages with
            | Some rewritten -> HostMessageProjection.replaceMessagesInPlace outObj rewritten
            | None -> ()
        }

    let private applySessionXTraceAndNarrative
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailFuse: string -> unit)
        (scope: PluginRuntimeScope)
        (sessionId: string)
        (outObj: obj)
        (strengthReplayPlans: StrengthReplayPlan list)
        : Task =
        task {
            let rawMessages = unbox<obj array> outObj?messages |> Array.toList
            let capturedMessages = capturedMessagesStripped rawMessages

            let _semantic =
                ProviderWireCapture.wireMessageView capturedMessages
                |> ProviderProjection.toSemantic

            let sessionIdentity = SessionId.create sessionId
            let stableMessageIds = tryStableHostMessageIds rawMessages

            let! traceState =
                captureTraceState journal sessionIdentity stableMessageIds capturedMessages strengthFailFuse

            do!
                StrengthReplay.commitTracedAfterCapture
                    journal
                    strengthDurability
                    (raiseStrengthFailClosed strengthFailFuse)
                    traceState
                    strengthReplayPlans

            traceState
            |> Option.iter (refreshCompanionXTrace scope.Sessions.Companions sessionId)

            do! applyManagerNarrativeRewrite journal (Some sessionId) traceState rawMessages outObj
        }

    let private applySessionXTracePipeline
        (journal: AgentJournal option)
        (strengthDurability: StrengthDurabilityPort option)
        (strengthFailFuse: string -> unit)
        (scope: PluginRuntimeScope)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        (strengthReplayPlans: StrengthReplayPlan list)
        : Task =
        match projectionSessionIdOpt with
        | Some sessionId ->
            applySessionXTraceAndNarrative
                journal
                strengthDurability
                strengthFailFuse
                scope
                sessionId
                outObj
                strengthReplayPlans
        | None -> Task.FromResult()

    let private projectOrKeepRaw (sessionId: string) (bloggerMessages: obj list) (messages: obj list) : obj list =
        if List.isEmpty messages then
            Diagnostic.emit
                "enforcer-empty-project"
                [ "session_id", sessionId
                  "result", "ProjectMessages empty; keep raw transcript" ]

            bloggerMessages
        else
            messages

    let private messagesOrRaw (bloggerMessages: obj list) (messages: obj list) : obj list =
        if List.isEmpty messages then bloggerMessages else messages

    let private terminalRunFromMessages (rawMessages: obj list) : ProviderRunIdentity =
        match EnforcerCycleDecode.lastAssistantStep rawMessages with
        | Some(messageId, _, _) when not (String.IsNullOrWhiteSpace messageId) -> ProviderRunIdentity.create messageId
        | _ -> ProviderRunIdentity.create "unknown-prose-run"

    let private makeRecoveryProbe
        (durable: AgentJournal)
        (sid: SessionId)
        (rawMessages: obj list)
        : EnforcerContinuation.RecoveryStageProbe =
        fun ctx ->
            let terminalRun = terminalRunFromMessages rawMessages
            let requestKey = BloggerRequestId.value (BloggerRequestContext.requestId ctx)
            BloggerRecoveryProbe.repairState durable sid requestKey terminalRun rawMessages

    let private handlePhysicalStopResult
        (sid: SessionId)
        (sessionId: string)
        (physicalUserMessageId: PhysicalUserMessageId option)
        result
        =
        match result with
        | Ok() -> physicalUserMessageId |> Option.iter (ModelRouting.releasePhysicalExecution sid)
        | Error error ->
            Diagnostic.emit "enforcer-stop-physical-run" [ "session_id", sessionId; "result", "abort-error: " + error ]

    let private requestPhysicalStop
        (sessionPort: ISessionHostPort)
        (sid: SessionId)
        (sessionId: string)
        (physicalUserMessageId: PhysicalUserMessageId option)
        =
        task {
            try
                let! result = sessionPort.AbortSession sid
                handlePhysicalStopResult sid sessionId physicalUserMessageId result
            with ex ->
                Diagnostic.emit
                    "enforcer-stop-physical-run"
                    [ "session_id", sessionId; "result", "abort-exception: " + ex.Message ]
        }
        |> ignore

    let private applyContinuationOutcome
        (sessionPort: ISessionHostPort)
        (sid: SessionId)
        (sessionId: string)
        (bloggerMessages: obj list)
        (outObj: obj)
        (outcome: EnforcerContinuation.ContinuationOutcome)
        : Task =
        task {
            match outcome with
            | EnforcerContinuation.ContinuationOutcome.ProjectMessages messages ->
                HostMessageProjection.replaceMessagesInPlace
                    outObj
                    (projectOrKeepRaw sessionId bloggerMessages messages)
            | EnforcerContinuation.ContinuationOutcome.StopPhysicalRun(messages, reason) ->
                HostMessageProjection.replaceMessagesInPlace outObj (messagesOrRaw bloggerMessages messages)

                Diagnostic.emit "enforcer-stop-physical-run" [ "session_id", sessionId; "result", reason ]

                requestPhysicalStop sessionPort sid sessionId (ProviderWireCapture.lastUserMessageId bloggerMessages)
        }

    let private runEnforcerWhenFamilyReady
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (sessionPort: ISessionHostPort)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        : Task =
        task {
            let bloggerMessages = unbox<obj array> outObj?messages |> Array.toList

            let confirmedFailure: ConfirmedFailurePort =
                fun targetSessionId providerRun reason ->
                    task {
                        let! outcome =
                            FallbackLedger.admitConfirmedFailure
                                durable
                                AgentPairCursor.DefaultAutoRecoveryBudget
                                targetSessionId
                                providerRun
                                reason

                        return outcome
                    }

            let! outcome =
                EnforcerHost.handleContinuation
                    scope.ParkedTransformHost
                    journal
                    (Some confirmedFailure)
                    makeRecoveryProbe
                    sid
                    bloggerMessages

            do! applyContinuationOutcome sessionPort sid sessionId bloggerMessages outObj outcome
        }

    let private runEnforcerAfterRecovery
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (sessionPort: ISessionHostPort)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        (recovery: FamilyRecovery)
        : Task =
        match recovery with
        | FamilyRecovery.FamilyBlocked _ -> Task.FromResult()
        | FamilyRecovery.FamilyWaiting _
        | FamilyRecovery.FamilyReady _ ->
            runEnforcerWhenFamilyReady scope journal durable sessionPort sid sessionId outObj

    let private runEnforcerForMainSession
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (sessionPort: ISessionHostPort)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        : Task =
        task {
            let! recovery = scope.EnsureRecoveryDone sid
            do! runEnforcerAfterRecovery scope journal durable sessionPort sid sessionId outObj recovery
        }

    let private runEnforcerIfMainAssociated
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (durable: AgentJournal)
        (sessionPort: ISessionHostPort)
        (sid: SessionId)
        (sessionId: string)
        (outObj: obj)
        : Task =
        let associations = (AgentJournal.snapshot durable).AgentProjections.Associations

        match SessionAssociationProjection.tryMainSessionOf sid associations with
        | Some _ -> runEnforcerForMainSession scope journal durable sessionPort sid sessionId outObj
        | None -> Task.FromResult()

    let private applyBloggerEnforcerContinuation
        (scope: PluginRuntimeScope)
        (journal: AgentJournal option)
        (sessionPort: ISessionHostPort)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        : Task =
        match projectionSessionIdOpt, journal with
        | Some sessionId, Some durable ->
            let sid = SessionId.create sessionId
            runEnforcerIfMainAssociated scope journal durable sessionPort sid sessionId outObj
        | _ -> Task.FromResult()

    let private skipPairGuideline (journal: AgentJournal option) (projectionSessionIdOpt: string option) : bool =
        match journal, projectionSessionIdOpt with
        | Some durable, Some sessionId ->
            SessionAssociationProjection.isCompanion
                (SessionId.create sessionId)
                (AgentJournal.snapshot durable).AgentProjections.Associations
        | _ -> false

    let private languageFor (projectionSessionIdOpt: string option) : ProviderLanguage =
        match projectionSessionIdOpt with
        | Some sessionId -> ProviderLanguageBinding.ensureRoot (SessionId.create sessionId)
        | None -> ProviderLanguage.English

    let private toolEstimateText
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (language: ProviderLanguage)
        : string option =
        match journal, projectionSessionIdOpt with
        | Some durable, Some sessionId ->
            DelegatedToolEstimateLedger.tryRemaining durable (SessionId.create sessionId)
            |> Option.map (PairProgrammingCalibration.renderToolEstimate language)
        | _ -> None

    let private composeMarkerText
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (elapsed: string option)
        (toolEstimate: string option)
        (guideline: string)
        : Task<string> =
        match journal, projectionSessionIdOpt with
        | Some durable, Some sessionId ->
            task {
                let! guidance = EnforcerTipGuidance.latestTipGuidance durable (SessionId.create sessionId)

                return PairProgrammingCalibration.composeWithElapsed guidance elapsed toolEstimate guideline
            }
        | _ -> Task.FromResult(PairProgrammingCalibration.composeWithElapsed None elapsed toolEstimate guideline)

    let private abortSessionIfPresent (sessionPort: ISessionHostPort) (projectionSessionIdOpt: string option) : Task =
        match projectionSessionIdOpt with
        | Some sessionId ->
            task {
                let! _ = sessionPort.AbortSession(SessionId.create sessionId)
                ()
            }
        | None -> Task.FromResult()

    let private applyPairInjectResult
        (sessionPort: ISessionHostPort)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        (injectResult: Result<obj list, string>)
        : Task =
        match injectResult with
        | Ok newMessages ->
            HostMessageProjection.replaceMessagesInPlace outObj newMessages
            Task.FromResult()
        | Error reason ->
            Diagnostic.emit
                "host-013-fail-closed"
                [ "session_id", (defaultArg projectionSessionIdOpt ""); "result", reason ]

            abortSessionIfPresent sessionPort projectionSessionIdOpt

    let private injectPairProgrammingGuideline
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (sessionStartedAt: DateTimeOffset option)
        (clock: IClockPort)
        (sessionPort: ISessionHostPort)
        (outObj: obj)
        : Task =
        task {
            let messages = unbox<obj array> outObj?messages |> Array.toList
            let language = languageFor projectionSessionIdOpt

            let guideline =
                ProviderProse.render language ProjectionConstants.PairProgrammingGuidelinePath Map.empty

            let elapsed =
                sessionStartedAt
                |> Option.map (fun startedAt ->
                    let elapsedMilliseconds = (clock.UtcNow() - startedAt).TotalMilliseconds
                    PairProgrammingCalibration.renderElapsed language elapsedMilliseconds)

            let toolEstimate = toolEstimateText journal projectionSessionIdOpt language

            let! markerText = composeMarkerText journal projectionSessionIdOpt elapsed toolEstimate guideline

            let! injectResult =
                PairProgrammingThoughtTransform.tryInject journal projectionSessionIdOpt markerText messages

            do! applyPairInjectResult sessionPort projectionSessionIdOpt outObj injectResult
        }

    let private maybeInjectPairGuideline
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (sessionStartedAt: DateTimeOffset option)
        (clock: IClockPort)
        (sessionPort: ISessionHostPort)
        (outObj: obj)
        : Task =
        if skipPairGuideline journal projectionSessionIdOpt then
            Task.FromResult()
        else
            injectPairProgrammingGuideline journal projectionSessionIdOpt sessionStartedAt clock sessionPort outObj

    let private projectRequirementGrounding
        (journal: AgentJournal option)
        (workspaceDirectory: string option)
        (projectionSessionIdOpt: string option)
        (sessionPort: ISessionHostPort)
        (outObj: obj)
        : Task =
        let applyProjection sessionId projected =
            match projected with
            | Ok values ->
                HostMessageProjection.replaceMessagesInPlace outObj values
                Task.FromResult()
            | Error reason ->
                Diagnostic.emit
                    "requirement-grounding-projection-fail-closed"
                    [ "session_id", sessionId; "result", reason ]

                task {
                    let! _ = sessionPort.AbortSession(SessionId.create sessionId)
                    ()
                }

        match journal, workspaceDirectory, projectionSessionIdOpt with
        | Some durable, Some _, Some sessionId when not (String.IsNullOrWhiteSpace sessionId) ->
            task {
                let messages = unbox<obj array> outObj?messages |> Array.toList

                let! projected =
                    Wanxiangshu.OpenCode.Host.RequirementGrounding.RequirementGroundingTransform.tryProject
                        durable
                        sessionId
                        messages

                do! applyProjection sessionId projected
            }
        | _ -> Task.FromResult()

    let private bloggerChronicleThoughtText (language: ProviderLanguage) =
        match language with
        | ProviderLanguage.SimplifiedChinese -> "对于简单的记账请求，完全不需要触发思考。让我直接调用 chronicle 工具。"
        | ProviderLanguage.English ->
            "For simple bookkeeping requests, there is no need to trigger thinking at all. Let me call the chronicle tool directly."

    let private bloggerChronicleThoughtModelIds: Set<string> = Set.empty

    let private bloggerChronicleThoughtEnabled (projectionSessionIdOpt: string option) =
        if Set.isEmpty bloggerChronicleThoughtModelIds then
            false
        else
            projectionSessionIdOpt
            |> Option.map SessionId.create
            |> Option.bind SessionExecutionBinding.currentProviderModel
            |> Option.exists (fun model -> Set.contains model.modelID bloggerChronicleThoughtModelIds)

    let private rawMessageRole (message: obj) =
        if isNull message then
            ""
        elif not (isNull message?info) && not (isNull message?info?role) then
            string message?info?role
        elif not (isNull message?role) then
            string message?role
        else
            ""

    let private rawMessageParts (message: obj) : obj array =
        if isNull message || isNull message?parts then
            [||]
        else
            unbox<obj array> message?parts

    let private isBloggerChronicleThoughtPart (part: obj) =
        if isNull part || isNull part?``type`` || isNull part?text then
            false
        else
            let text = string part?text

            string part?``type`` = "reasoning"
            && (text = bloggerChronicleThoughtText ProviderLanguage.SimplifiedChinese
                || text = bloggerChronicleThoughtText ProviderLanguage.English)

    let private isBloggerChronicleThought (message: obj) =
        let parts = rawMessageParts message

        rawMessageRole message = "assistant"
        && parts.Length = 1
        && isBloggerChronicleThoughtPart parts.[0]

    let private bloggerChronicleThoughtMessageId (projectionSessionIdOpt: string option) (messages: obj list) =
        let frontier =
            messages
            |> List.tryLast
            |> Option.bind ProviderWireDecode.hostMessageId
            |> Option.defaultValue "start"

        let digest =
            HostDigest.sha256Hex (
                String.concat
                    "\u001f"
                    [ defaultArg projectionSessionIdOpt ""
                      "blogger-chronicle-reasoning"
                      frontier ]
            )

        "reasoning-" + digest.Substring(0, 24)

    let private bloggerChronicleThoughtMessage (messageId: string) (text: string) =
        let part = createObj [ "type", box "reasoning"; "text", box text ]

        createObj
            [ "info", box (createObj [ "id", box messageId; "role", box "assistant" ])
              "parts", box [| part |] ]

    let private insertBloggerChronicleThoughtAtFrontier (marker: obj) (messages: obj list) =
        match List.rev messages with
        | last :: rest when rawMessageRole last = "user" -> List.rev rest @ [ marker; last ]
        | _ -> messages @ [ marker ]

    let private injectBloggerChronicleThought
        (journal: AgentJournal option)
        (projectionSessionIdOpt: string option)
        (outObj: obj)
        =
        match journal, projectionSessionIdOpt with
        | Some durable, Some sessionId when
            bloggerChronicleThoughtEnabled projectionSessionIdOpt
            && SessionAssociationProjection.isCompanion
                (SessionId.create sessionId)
                (AgentJournal.snapshot durable).AgentProjections.Associations
            ->
            let messages =
                unbox<obj array> outObj?messages
                |> Array.toList
                |> List.filter (isBloggerChronicleThought >> not)

            let messageId = bloggerChronicleThoughtMessageId projectionSessionIdOpt messages
            let text = bloggerChronicleThoughtText (languageFor projectionSessionIdOpt)
            let reasoning = bloggerChronicleThoughtMessage messageId text

            HostMessageProjection.replaceMessagesInPlace
                outObj
                (insertBloggerChronicleThoughtAtFrontier reasoning messages)
        | _ -> ()

    let private strengthReplicaRuntime
        (projectionSessionIdOpt: string option)
        (scope: PluginRuntimeScope)
        : StrengthReplicaRuntime option =
        match projectionSessionIdOpt, scope.Strength.StrengthReplicaRuntime with
        | Some sessionId, Some runtime when runtime.IsReplica(SessionId.create sessionId) -> Some runtime
        | _ -> None

    let private requireReplicaHandled (handled: bool) =
        if not handled then
            raise (InvalidOperationException "StrengthReplica transform lost its live decision binding")

    let private beginPhysicalProviderAttempt (scope: PluginRuntimeScope) (sessionText: string) (outObj: obj) =
        let sessionId = SessionId.create sessionText

        let rawMessages =
            ProviderWireDecode.rawArray (ProviderWireDecode.readField outObj "messages")

        scope.Sessions.Quiescence.BeginProviderAttempt sessionId

        match
            SessionExecutionBinding.beginProviderAttempt
                sessionId
                (ProviderWireCapture.lastUserMessageId rawMessages)
                (ProviderWireCapture.lastUserPromptKey rawMessages)
        with
        | Ok() -> ()
        | Error error -> invalidOp error

    /// Provider-facing transform composition: order only.
    /// Strength replay/trace → StrengthReplay; speculation → StrengthSpeculate;
    /// narrative → ManagerNarrativeTransform; seal → ReviewSeal; replica fast path unchanged.
    let create (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : obj -> obj -> Task<unit> =
        let scope = boot.Scope
        let journal = boot.Journal
        let clock = boot.Clock
        let workspaceDirectory = boot.WorkspaceDirectory
        let sessionPort = host.SessionPort
        let snapshotOpt = host.SnapshotOpt
        let strengthDurability = host.StrengthDurability
        let wired = host.Wired
        let strengthFailFuse = boot.StrengthFailClosed

        let applyCompanionForOrdinaryMaterial projectionSessionIdOpt inObj outObj =
            if ExplicitResumeSuppression.isCurrentMaterial outObj then
                AsyncSupport.completedTask ()
            else
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

        let normalTransform (projectionSessionIdOpt: string option) (inObj: obj) (outObj: obj) : Task<unit> =
            task {
                // HOST-004：新 provider request 开始构建 → 旧 idle permit
                // 立即失效。必须在该 transform 的最早同步位置（任何 let!
                // 之前）调用，不得等 request 已运行才标 Running。
                projectionSessionIdOpt
                |> Option.iter (fun sessionId -> beginPhysicalProviderAttempt scope sessionId outObj)

                // TIME-007: the first provider-facing prompt is the session's
                // creation boundary. Sample synchronously before any await, then
                // bind once durably; later prompts reuse the projection value.
                let sessionStartCandidate =
                    projectionSessionIdOpt |> Option.map (fun _ -> clock.UtcNow())

                let! sessionStartedAt =
                    tryBindSessionStartedAt journal projectionSessionIdOpt sessionStartCandidate sessionPort

                let! strengthReplayPlans =
                    strengthReplayPlansFor journal strengthDurability strengthFailFuse projectionSessionIdOpt outObj

                // COMPANION-003/007: keep the XTrace in step with the
                // provider-visible semantic projection at the transform
                // boundary — BEFORE the Companion rewrite and X-wire run,
                // so the ingest cursor maps against the trace that now
                // exists (not the previous round's mirror) and the XTrace
                // never absorbs synthetic heads (Companion memory / prefix
                // replacement) as raw parts.
                // Idempotent by (turn, part) provenance; a lagging trace
                // would stall BlogObservationCommitted.
                do!
                    applySessionXTracePipeline
                        journal
                        strengthDurability
                        strengthFailFuse
                        scope
                        projectionSessionIdOpt
                        outObj
                        strengthReplayPlans

                do! applyCompanionForOrdinaryMaterial projectionSessionIdOpt inObj outObj

                do! XWire.applyTransform snapshotOpt journal scope outObj

                // docs/what/enforcer.md ENFORCER-044/047/050: Blogger continuation only.
                // Main-session material is decided once in
                // CompanionTransform → BloggerCoordinator.onMainMaterial.
                do! applyBloggerEnforcerContinuation scope journal sessionPort projectionSessionIdOpt outObj

                // STRENGTH-009: freeze the post-Enforcer semantic view and
                // complete any eligible speculation before the Pair marker.
                // Prepared publication precedes Candidate visibility.
                do! StrengthSpeculate.tryApply snapshotOpt journal strengthDurability scope outObj

                // HOST-013：永久 pair-programming auto-injected。
                // XTrace 之后、ReviewSeal 之前。恢复 durable 历史 pair，
                // 再在 ResultGap 写入本次 completed synthetic skill({name:""}) Host 行。
                // Companion / Blogger 整段跳过：结对编程约束干扰 blog 工具合同。
                do! maybeInjectPairGuideline journal projectionSessionIdOpt sessionStartedAt clock sessionPort outObj

                // REQUIREMENT-GROUNDING-007/012: permanent requirement reads use
                // the same append-only placement discipline, after HOST-013 so
                // ordinary and Cursor order is always pseudo-skill → read(s).
                do! projectRequirementGrounding journal workspaceDirectory projectionSessionIdOpt sessionPort outObj

                injectBloggerChronicleThought journal projectionSessionIdOpt outObj

                // HOST-016: 对 provider-facing 消息做非空 content 兜底保障，
                // 避免仅推理/空 content 导致上游 API 报 400 messages[i].content cannot be empty。
                let currentMessages = unbox<obj array> outObj?messages |> Array.toList
                let sanitized = HostMessageProjection.sanitizeMessages currentMessages
                HostMessageProjection.replaceMessagesInPlace outObj sanitized
                ()
            }

        let ordinaryProviderTransform projectionSessionIdOpt inObj outObj =
            task {
                projectionSessionIdOpt |> Option.iter wired.RegisterOwned

                match strengthReplicaRuntime projectionSessionIdOpt scope with
                | Some runtime ->
                    // STRENGTH-004/009: Replica uses exactly one request-plan
                    // writer plus its mirror/K gate. XTrace, Manager narrative,
                    // Companion, Enforcer, Pair and Review are owner-only.
                    do! XWire.applyTransform snapshotOpt journal scope outObj
                    let! handled = runtime.HandleTransform outObj
                    requireReplicaHandled handled

                    let currentMessages = unbox<obj array> outObj?messages |> Array.toList
                    let sanitized = HostMessageProjection.sanitizeMessages currentMessages
                    HostMessageProjection.replaceMessagesInPlace outObj sanitized
                | None -> do! normalTransform projectionSessionIdOpt inObj outObj
            }

        let transform (inObj: obj) (outObj: obj) : Task<unit> =
            task {
                let projectionSessionIdOpt =
                    projectionSessionIdFromMessages outObj
                    |> Option.orElseWith (fun () ->
                        if not (isNull inObj) && not (isNull inObj?sessionID) then
                            let sid = string inObj?sessionID
                            if String.IsNullOrWhiteSpace sid then None else Some sid
                        elif not (isNull inObj) && not (isNull inObj?sessionId) then
                            let sid = string inObj?sessionId
                            if String.IsNullOrWhiteSpace sid then None else Some sid
                        else
                            None)

                if ExplicitResumeSuppression.isCurrentMaterial outObj then
                    // CRASH-018: the exact /continue material stays disclosure-only
                    // for every provider step, including steps after tool results.
                    // Do not reinterpret it through ordinary semantic transforms.
                    let currentMessages = unbox<obj array> outObj?messages |> Array.toList
                    let sanitized = HostMessageProjection.sanitizeMessages currentMessages
                    HostMessageProjection.replaceMessagesInPlace outObj sanitized
                else
                    do! ordinaryProviderTransform projectionSessionIdOpt inObj outObj
            }

        transform
