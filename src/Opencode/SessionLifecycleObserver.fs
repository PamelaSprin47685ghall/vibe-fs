module VibeFs.Opencode.SessionLifecycleObserver

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Domain
open VibeFs.Kernel.Nudge
open VibeFs.Kernel.NudgeState
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.PromptFragments
open VibeFs.Kernel.Methodology
open VibeFs.Shell
open VibeFs.Shell.OpencodeClientCodec
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.OpencodeHookInputCodec
open VibeFs.Opencode.NudgeEffect
open VibeFs.Opencode.NudgeEventCodec
open VibeFs.Opencode.BacklogSession

type SessionLifecycleObserver(host: Host, ctx: obj, reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore, registry: ChildAgentRegistry) =
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
            if normalizeToolName host (toolNameFromHookInput input) = "todowrite" then
                let methodologies = selectMethodologiesFromHookArgs (argsFromHookInput input)
                match hookOutputString output with
                | Some _ ->
                    let rewritten = todoResultText methodologies
                    let withNudge = if rewritten.Contains meditatorNudge then rewritten else rewritten + "\n" + meditatorNudge
                    setHookOutputString output withNudge
                | None -> ()
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

let createSessionLifecycleObserver (host: Host) (ctx: obj) (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore) (registry: ChildAgentRegistry) : SessionLifecycleObserver =
    SessionLifecycleObserver(host, ctx, reviewStore, registry)