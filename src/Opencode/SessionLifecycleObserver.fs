module Wanxiangshu.Opencode.SessionLifecycleObserver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeState
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.FallbackKernel.Types

open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.SerialStateHolder
open Wanxiangshu.Opencode.NudgeEffect
open Wanxiangshu.Opencode.BacklogSession

type SessionLifecycleObserver
    ( host              : Host
    , ctx               : obj
    , reviewStore       : Wanxiangshu.Shell.ReviewRuntime.ReviewStore
    , registry          : ChildAgentRegistry
    , fallbackHandler   : (obj -> JS.Promise<FallbackHookResult>) option
    , fallbackRuntime   : FallbackRuntimeState
    , backlogSession    : Wanxiangshu.Opencode.BacklogSession.BacklogSession
    ) =

    let abortSession (client: obj) (sid: string) : JS.Promise<unit> =
        promise {
            try
                let arg = box {| path = box {| id = sid |} |}
                do! client?session?abort(arg) |> Promise.map ignore
            with _ -> ()
        }

    let promptSession (client: obj) (sid: string) (text: string) : JS.Promise<unit> =
        promise {
            try
                do! client?session?prompt(sid, box text) |> Promise.map ignore
            with _ -> ()
        }
    let holder = StateHolder<NudgeShellState>(emptyState)

    member _.handleChatMessage(sessionID: SessionId, agent: string, parts: obj) : JS.Promise<unit> =
        holder.Mutate(fun state ->
            let text = getPartsText parts
            let sid = Id.sessionIdValue sessionID
            if isNudgePrompt text then state, ()
            else
                let agentOpt = if agent <> "" then Some agent else None
                fallbackRuntime.SetAgentName sid agent
                resumeSession (rememberAgent state sid agentOpt) sid, ())
        resolvedUnitPromise ()

    member _.handleCommandExecuteBefore(input: obj) (_output: obj) : JS.Promise<unit> =
        let sessionIDStr = sessionIdFromHookInput input ""
        holder.Mutate(fun (state: NudgeShellState) -> resumeSession state sessionIDStr, ())
        resolvedUnitPromise ()

    member _.handleToolExecuteAfter(input: obj) (output: obj) : JS.Promise<unit> =
        promise {
            let sessionIDStr = sessionIdFromHookInput input ""
            let tool = normalizeToolName host (toolNameFromHookInput input)
            if tool = "todowrite" then
                let methodologies = selectMethodologiesFromHookArgs (argsFromHookInput input)
                match hookOutputString output with
                | Some _ -> setHookOutputString output (todoWriteOutput methodologies true)
                | None -> ()
            elif tool = "task_complete" then
                let sid = sessionIdFromHookInput input ""
                if sid <> "" then
                    let st = fallbackRuntime.GetOrCreateState sid
                    fallbackRuntime.UpdateState sid { st with TaskComplete = true }
            elif tool = "submit_review" then
                match hookOutputString output with
                | Some text when isSubmitReviewWipProgressOutput text ->
                    holder.Mutate(fun state -> resumeSession state sessionIDStr, ())
                | _ -> ()
        }

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        promise {
            let eventEnvelope = decodeHostEventEnvelope input

            // Bind agent name from session.status busy events before fallback
            match eventEnvelope with
            | Some { EventType = "session.status"; Props = props } ->
                let statusObj = Dyn.get props "status"
                let agentName = Dyn.str statusObj "agent"
                if agentName <> "" then
                    let sid = getSessionID "session.status" props
                    if sid <> "" then
                        fallbackRuntime.SetAgentName sid agentName
            | _ -> ()

            // Fallback intercepts first; consumed → skip nudge
            let! fbConsumed =
                match fallbackHandler with
                | Some handler ->
                    promise {
                        let! r = handler input
                        return r.Consumed
                    }
                | None -> Promise.lift false

            // Track local session busyCount: busy→1, idle→0
            match eventEnvelope with
            | Some { EventType = "session.status"; Props = props } ->
                let statusObj = Dyn.get props "status"
                let status =
                    let fromStatus = Dyn.str statusObj "status"
                    if fromStatus <> "" then fromStatus else Dyn.str statusObj "type"
                let sid = getSessionID "session.status" props
                if sid <> "" && status = "busy" then
                    fallbackRuntime.SetBusyCount sid 1
                elif sid <> "" && status = "idle" then
                    fallbackRuntime.SetBusyCount sid 0
                elif sid <> "" && status = "busy" then
                    fallbackRuntime.SetBusyCount sid (fallbackRuntime.GetBusyCount sid + 1)
            | _ -> ()

            if fbConsumed then
                return ()
            else
                let claimed =
                    holder.Mutate(fun state ->
                        try
                            match eventEnvelope with
                            | None -> state, None
                            | Some { EventType = eventType; Props = props } ->
                                let sessionIDStr = getSessionID eventType props
                                match Id.trySessionId sessionIDStr with
                                | None -> state, None
                                | Some session ->
                                    let nudgeEvent = decodeNudgeHostEvent eventType props
                                    let nextState, wantsNudge = NudgeState.handleEvent state (Id.sessionIdValue session) nudgeEvent
                                    nextState, (if wantsNudge then Some session else None)
                        with _ ->
                            state, None)
                match claimed with
                | Some sessionID ->
                    match getClientFromPluginCtx ctx with
                    | Ok client -> startNudgeFlow holder client reviewStore registry backlogSession sessionID
                    | Error _ -> ()
                | None -> ()
        }

let createSessionLifecycleObserver
    ( host               : Host
    , ctx                : obj
    , reviewStore        : Wanxiangshu.Shell.ReviewRuntime.ReviewStore
    , registry           : ChildAgentRegistry
    , fallbackHandler    : (obj -> JS.Promise<FallbackHookResult>) option
    , fallbackRuntime    : FallbackRuntimeState
    , backlogSession     : Wanxiangshu.Opencode.BacklogSession.BacklogSession
    ) : SessionLifecycleObserver =
    SessionLifecycleObserver(host, ctx, reviewStore, registry, fallbackHandler, fallbackRuntime, backlogSession)
