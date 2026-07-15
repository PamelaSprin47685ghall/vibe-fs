module Wanxiangshu.Opencode.SessionLifecycleObserver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Opencode.ProgressObserver
open Wanxiangshu.Opencode.FallbackCoordinator
open Wanxiangshu.Opencode.NudgeTrigger
open Wanxiangshu.Opencode.BacklogSession

type SessionLifecycleObserver
    (
        host: Host,
        ctx: obj,
        reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore,
        registry: ChildAgentRegistry,
        fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option,
        fallbackRuntime: FallbackRuntimeState,
        backlogSession: Wanxiangshu.Opencode.BacklogSession.BacklogSession
    ) =

    let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

    let progress = ProgressObserver(host, ctx, backlogSession, fallbackRuntime)
    let fallback = FallbackCoordinator(fallbackHandler, fallbackRuntime)

    let nudge =
        createNudgeTrigger
            host
            ctx
            fallbackRuntime
            reviewStore
            (fun sid -> fallbackRuntime.MarkForceStopped sid)
            (fun sid -> fallbackRuntime.RemoveForceStopped sid)
            (fun sid -> fallbackRuntime.IsForceStopped sid)

    member _.handleChatMessage
        (sessionID: Wanxiangshu.Kernel.Domain.SessionId, agent: string, parts: obj)
        : JS.Promise<unit> =
        progress.OnChatMessage(sessionID, agent, parts)

    member _.OnNewHumanMessage
        (sessionID: string, agent: string, modelOpt: string option, messageId: string)
        : JS.Promise<unit> =
        promise {
            let directory = if Dyn.isNullish ctx then "" else pluginDirectoryFromCtx ctx

            // Cancel any pending continuation lease on new human turn
            match fallbackRuntime.TryGetPendingLease sessionID with
            | Some lease ->
                do!
                    finishContinuation
                        fallbackRuntime
                        directory
                        sessionID
                        lease
                        ContinuationOutcome.Cancelled
                        "new_human_turn"
            | None -> ()

            let msgId =
                if messageId = "" then
                    "human-" + System.Guid.NewGuid().ToString("N")
                else
                    messageId

            let lastMsgId = fallbackRuntime.GetLastHumanMessageId sessionID

            if msgId <> lastMsgId then
                if directory <> "" then
                    match fallbackRuntime.TryGetPendingNudgeLease sessionID with
                    | Some nudgeLease ->
                        do!
                            appendNudgeCancelledOrFail
                                directory
                                sessionID
                                nudgeLease.NudgeID
                                "new_human_turn"
                                nudgeLease.NudgeOrdinal

                        let _ = fallbackRuntime.ApplyCancelNudgeLease(sessionID, nudgeLease.NudgeID)
                        ()
                    | None -> ()

                    let activeComp = fallbackRuntime.GetActiveCompactionId sessionID
                    let activeCompOrdinal = fallbackRuntime.GetActiveCompactionOrdinal sessionID
                    let settleInfo = fallbackRuntime.TryGetSettleInfo(sessionID, activeComp)

                    match settleInfo with
                    | Some(_, ordinal) ->
                        do! appendCompactionSettledOrFail directory sessionID activeComp "cancelled" ordinal
                        let _ = fallbackRuntime.ApplySettle(sessionID, activeComp)
                        ()
                    | None -> ()

                fallbackRuntime.SetChain sessionID []
                fallbackRuntime.ClearModel sessionID
                fallbackRuntime.ClearInjected sessionID
                Wanxiangshu.Shell.ToolHookRuntime.clearSessionCompliance sessionID
                fallbackRuntime.SetSessionOwner sessionID SessionOwner.Human
                let turnId = fallbackRuntime.IncrementHumanTurnId sessionID
                let humanTurnOrdinal = fallbackRuntime.GetHumanTurnOrdinal sessionID
                fallbackRuntime.SetLastHumanMessageId sessionID msgId
                fallbackRuntime.RemoveForceStopped sessionID

                let currentGen = fallbackRuntime.GetSessionGeneration sessionID
                let currentCancelGen = fallbackRuntime.GetCancelGeneration sessionID
                fallbackRuntime.SetActiveContinuationGeneration sessionID currentGen
                fallbackRuntime.SetActiveContinuationCancelGeneration sessionID currentCancelGen

                match modelOpt with
                | Some m -> fallbackRuntime.SetLatestHumanModel sessionID m
                | None -> fallbackRuntime.ClearLatestHumanModel sessionID

                if agent <> "" then
                    fallbackRuntime.SetAgentName sessionID agent

                let provider, model, variant =
                    match modelOpt with
                    | Some m ->
                        match Wanxiangshu.Shell.FallbackMessageCodec.decodeModelFromObj (box m) with
                        | Some modelObj ->
                            modelObj.ProviderID, modelObj.ModelID, (modelObj.Variant |> Option.defaultValue "")
                        | None -> "", m, ""
                    | None -> "", "", ""

                if directory <> "" then
                    do!
                        appendHumanTurnStartedOrFail
                            directory
                            sessionID
                            turnId
                            provider
                            model
                            variant
                            agent
                            humanTurnOrdinal
                            msgId

                let state = fallbackRuntime.GetOrCreateState sessionID

                let ns =
                    { state with
                        Phase = FallbackPhase.Idle
                        ContinueCount = 0
                        FailureCount = 0
                        Lifecycle = FallbackLifecycle.Active
                        RecoveryCount = 0 }

                fallbackRuntime.UpdateState sessionID ns
                fallbackRuntime.SetConsumed sessionID false
                fallbackRuntime.ClearConsumed sessionID
            else
                // Duplicate new-human-message hook for the same source message; state already reset.
                ()
        }

    member _.FallbackRuntime = fallbackRuntime

    member _.WorkspaceRoot = if Dyn.isNullish ctx then "" else pluginDirectoryFromCtx ctx

    member _.handleCommandExecuteBefore (input: obj) (_output: obj) : JS.Promise<unit> =
        let _sessionIDStr = sessionIdFromHookInput input ""
        resolvedUnitPromise ()

    member _.handleToolExecuteAfter (input: obj) (output: obj) : JS.Promise<unit> =
        progress.HandleToolExecuteAfter input output

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        promise {
            let eventEnvelope = decodeHostEventEnvelope input

            let sid =
                match eventEnvelope with
                | Some env -> getSessionID env.EventType env.Props
                | None -> ""

            if sid <> "" then
                fallbackRuntime.SetEventHandlingActive sid true

            try
                match eventEnvelope with
                | Some { EventType = "session.status"
                         Props = props } ->
                    let statusObj = Dyn.get props "status"
                    let agentName = Dyn.str statusObj "agent"
                    let sid = getSessionID "session.status" props

                    if sid <> "" then
                        if agentName <> "" then
                            fallbackRuntime.SetAgentName sid agentName

                        let modelObj = Dyn.get statusObj "model"

                        match Wanxiangshu.Shell.FallbackMessageCodec.decodeModelFromObj modelObj with
                        | Some m -> fallbackRuntime.SetModel sid m
                        | None -> ()
                | Some { EventType = "session.compacted"
                         Props = props } ->
                    let sid = getSessionID "session.compacted" props

                    if sid <> "" then
                        let currentOwner = fallbackRuntime.GetSessionOwner sid
                        let compactionGen = fallbackRuntime.GetCompactionGeneration sid
                        let compactionId = fallbackRuntime.GetActiveCompactionId sid
                        let compactionOrdinal = fallbackRuntime.GetActiveCompactionOrdinal sid

                        // Compaction advances only the context generation.
                        // The session generation identifies the human/session
                        // lifecycle and must remain stable across compaction.
                        if
                            currentOwner = SessionOwner.Compaction
                            && compactionId <> ""
                            && not (fallbackRuntime.IsCompacted sid)
                        then
                            let nextContextGen = compactionGen + 1
                            fallbackRuntime.SetCompactionGeneration sid nextContextGen
                            let directory = pluginDirectoryFromCtx ctx

                            do!
                                appendCompactionContextGenerationChangedOrFail
                                    directory
                                    sid
                                    nextContextGen
                                    compactionId
                                    compactionOrdinal

                            fallbackRuntime.SetCompacted sid true
                | Some { EventType = "message.updated"
                         Props = props } ->
                    let sid = getSessionID "message.updated" props

                    if sid <> "" then
                        let info = Dyn.get props "info"
                        let role = Dyn.str info "role"

                        if role = "user" then
                            let parts = Dyn.get props "parts"

                            if not (Dyn.isNullish parts) && Dyn.isArray parts then
                                let partsArr = parts :?> obj array

                                let isCompactionContinue =
                                    partsArr
                                    |> Array.exists (fun part ->
                                        let isSynth = Dyn.get part "synthetic"
                                        let meta = Dyn.get part "metadata"

                                        (not (Dyn.isNullish isSynth) && unbox<bool> isSynth)
                                        && (not (Dyn.isNullish meta)
                                            && (Dyn.get meta "compaction_continue" |> unbox<bool>)))

                                if isCompactionContinue then
                                    let currentOwner = fallbackRuntime.GetSessionOwner sid

                                    if currentOwner = SessionOwner.Compaction && fallbackRuntime.IsCompacted sid then
                                        fallbackRuntime.SetCompactionContinuationObserved sid true
                | _ -> ()

                fallback.UpdateBusyCount eventEnvelope
                do! nudge.TrackLifetimeEvents eventEnvelope

                let! fbConsumed = fallback.TryConsumeEvent input

                if fbConsumed then
                    return ()
                else
                    do! nudge.HandleNaturalStop eventEnvelope
            finally
                if sid <> "" then
                    fallbackRuntime.SetEventHandlingActive sid false
        }

let createSessionLifecycleObserver
    (
        host: Host,
        ctx: obj,
        reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore,
        registry: ChildAgentRegistry,
        fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option,
        fallbackRuntime: FallbackRuntimeState,
        backlogSession: Wanxiangshu.Opencode.BacklogSession.BacklogSession
    ) : SessionLifecycleObserver =
    SessionLifecycleObserver(host, ctx, reviewStore, registry, fallbackHandler, fallbackRuntime, backlogSession)
