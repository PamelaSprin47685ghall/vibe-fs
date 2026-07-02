module Wanxiangshu.Opencode.SessionLifecycleObserver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.FallbackKernel.Types

open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.NudgeRuntime
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

    let mutable forceStoppedSessions: Set<string> = Set.empty

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
    let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

    let isNaturalStop (eventType: string) (props: obj) : bool =
        if eventType = "session.idle" then true
        elif eventType = "session.status" then
            let statusObj = Dyn.get props "status"
            let status =
                let fromStatus = Dyn.str statusObj "status"
                if fromStatus <> "" then fromStatus else Dyn.str statusObj "type"
            status = "idle"
        else false

    member _.handleChatMessage(sessionID: SessionId, agent: string, parts: obj) : JS.Promise<unit> =
        let text = getPartsText parts
        let sid = Id.sessionIdValue sessionID
        if not (isNudgePrompt text) && agent <> "" then
            fallbackRuntime.SetAgentName sid agent
        resolvedUnitPromise ()

    member _.handleCommandExecuteBefore(input: obj) (_output: obj) : JS.Promise<unit> =
        let _sessionIDStr = sessionIdFromHookInput input ""
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
                | Some text when isSubmitReviewWipProgressOutput text -> ()
                | _ -> ()
        }

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        promise {
            let eventEnvelope = decodeHostEventEnvelope input

            match eventEnvelope with
            | Some { EventType = "session.status"; Props = props } ->
                let statusObj = Dyn.get props "status"
                let agentName = Dyn.str statusObj "agent"
                if agentName <> "" then
                    let sid = getSessionID "session.status" props
                    if sid <> "" then
                        fallbackRuntime.SetAgentName sid agentName
            | _ -> ()

            // Track force-stopped sessions to block nudge after abort
            match eventEnvelope with
            | Some envelope ->
                let sessionIDStr = getSessionID envelope.EventType envelope.Props
                if sessionIDStr <> "" then
                    match envelope.EventType with
                    | "stream-abort" ->
                        forceStoppedSessions <- Set.add sessionIDStr forceStoppedSessions
                    | "session.error" ->
                        let errorObj = Dyn.get envelope.Props "error"
                        if not (Dyn.isNullish errorObj) then
                            let name = Dyn.str errorObj "name"
                            let tag = Dyn.str errorObj "_tag"
                            let msg = Dyn.str errorObj "message"
                            if name = "AbortError" || name = "MessageAbortedError"
                               || tag = "MessageAborted"
                               || containsAbortText msg then
                                forceStoppedSessions <- Set.add sessionIDStr forceStoppedSessions
                    | "session.next.prompted" ->
                        forceStoppedSessions <- Set.remove sessionIDStr forceStoppedSessions
                    | _ -> ()
            | None -> ()

            let! fbConsumed =
                match fallbackHandler with
                | Some handler ->
                    promise {
                        let! r = handler input
                        return r.Consumed
                    }
                | None -> Promise.lift false

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
                match eventEnvelope with
                | None -> ()
                | Some envelope ->
                    let eventType = envelope.EventType
                    let props = envelope.Props
                    let sessionIDStr = getSessionID eventType props
                    match Id.trySessionId sessionIDStr with
                    | None -> ()
                    | Some sessionID ->
                        if isNaturalStop eventType props
                           && not (Set.contains sessionIDStr forceStoppedSessions) then
                            match getClientFromPluginCtx ctx with
                            | Ok client ->
                                do! dispatchPostStopFromHistory client sessionID
                            | Error _ -> ()
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
