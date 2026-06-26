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

open Wanxiangshu.Shell
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Opencode.NudgeEffect
open Wanxiangshu.Opencode.BacklogSession

type SessionLifecycleObserver(host: Host, ctx: obj, reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore, registry: ChildAgentRegistry) =
    let holder = StateHolder<NudgeShellState>(emptyState)

    member _.handleChatMessage(sessionID: SessionId, agent: string, parts: obj) : JS.Promise<unit> =
        holder.Mutate(fun state ->
            let text = getPartsText parts
            let sid = Id.sessionIdValue sessionID
            if isNudgePrompt text then state, ()
            else
                let agentOpt = if agent <> "" then Some agent else None
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
            elif tool = "submit_review" then
                match hookOutputString output with
                | Some text when isSubmitReviewWipProgressOutput text ->
                    holder.Mutate(fun state -> resumeSession state sessionIDStr, ())
                | _ -> ()
        }

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        let claimed =
            holder.Mutate(fun state ->
                try
                    match decodeHostEventEnvelope input with
                    | None -> state, None
                    | Some { EventType = eventType; Props = props } ->
                        let sessionIDStr = getSessionID eventType props
                        match Id.trySessionId sessionIDStr with
                        | None -> state, None
                        | Some sessionID ->
                            let nudgeEvent = decodeNudgeHostEvent eventType props
                            let nextState, wantsNudge = NudgeState.handleEvent state (Id.sessionIdValue sessionID) nudgeEvent
                            nextState, (if wantsNudge then Some sessionID else None)
                with _ ->
                    state, None)
        match claimed with
        | Some sessionID ->
            match getClientFromPluginCtx ctx with
            | Ok client -> startNudgeFlow holder client reviewStore registry sessionID
            | Error _ -> ()
        | None -> ()
        resolvedUnitPromise ()

let createSessionLifecycleObserver (host: Host) (ctx: obj) (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) (registry: ChildAgentRegistry) : SessionLifecycleObserver =
    SessionLifecycleObserver(host, ctx, reviewStore, registry)