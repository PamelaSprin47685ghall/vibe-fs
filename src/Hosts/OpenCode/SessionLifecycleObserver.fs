module Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver

open Fable.Core
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeCompactionPure
open Wanxiangshu.Hosts.Opencode.ProgressObserver
open Wanxiangshu.Hosts.Opencode.Fallback.Coordinator
open Wanxiangshu.Hosts.Opencode.NudgeTrigger
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Hosts.Opencode.SessionLifecycleHumanTurn
open Wanxiangshu.Hosts.Opencode.SessionLifecycleEvents

type SessionLifecycleObserver
    (
        host: Host,
        ctx: obj,
        reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore,
        registry: ChildAgentRegistry,
        fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option,
        fallbackRuntime: FallbackRuntimeStore,
        backlogSession: BacklogSession
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
            (fun sid -> fallbackRuntime.UpdateSession(sid, markForceStopped))
            (fun sid -> fallbackRuntime.UpdateSession(sid, removeForceStopped))
            (fun sid -> (fallbackRuntime.GetSession sid).CompactionForceStopped)

    member _.handleChatMessage(sessionID: SessionId, agent: string, parts: obj) : JS.Promise<unit> =
        progress.OnChatMessage(sessionID, agent, parts)

    member _.OnNewHumanMessage
        (sessionID: string, agent: string, modelOpt: string option, messageId: string)
        : JS.Promise<unit> =
        onNewHumanMessage ctx fallbackRuntime sessionID agent modelOpt messageId

    member _.FallbackRuntime = fallbackRuntime

    member _.WorkspaceRoot = if isNullish ctx then "" else pluginDirectoryFromCtx ctx

    member _.handleCommandExecuteBefore (input: obj) (_output: obj) : JS.Promise<unit> =
        let _sessionIDStr = sessionIdFromHookInput input ""
        resolvedUnitPromise ()

    member _.handleToolExecuteAfter (input: obj) (output: obj) : JS.Promise<unit> =
        progress.HandleToolExecuteAfter input output

    member _.handleEvent(input: obj) : JS.Promise<unit> =
        handleEvent ctx fallbackRuntime fallback nudge input

let createSessionLifecycleObserver
    (
        host: Host,
        ctx: obj,
        reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore,
        registry: ChildAgentRegistry,
        fallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option,
        fallbackRuntime: FallbackRuntimeStore,
        backlogSession: BacklogSession
    ) : SessionLifecycleObserver =
    SessionLifecycleObserver(host, ctx, reviewStore, registry, fallbackHandler, fallbackRuntime, backlogSession)
