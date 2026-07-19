module Wanxiangshu.Hosts.Opencode.PluginHooks

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel
open Wanxiangshu.Hosts.Opencode.ChatHooks
open Wanxiangshu.Hosts.Opencode.ToolDefinitionHooks
open Wanxiangshu.Hosts.Opencode.HookExecute
open Wanxiangshu.Hosts.Opencode.CommandHooks
open Wanxiangshu.Hosts.Opencode.EventHooks
open Wanxiangshu.Hosts.Opencode.CompactionHook
open Wanxiangshu.Hosts.Opencode.HookTransform
open Wanxiangshu.Hosts.Opencode.CompactionTransform
open Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.OpencodeSessionEventCodec
open Wanxiangshu.Hosts.Opencode.OpencodeHostEvent
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Hosts.Opencode.PtySpawn
open Wanxiangshu.Hosts.Opencode.PluginServices
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup

let private twoArgHook (f: obj -> obj -> JS.Promise<unit>) =
    box (System.Func<obj, obj, JS.Promise<unit>>(f))

let private registerToolHooks (result: obj) (host: Host) (services: CoreServices) =
    setKey
        result
        "chat.message"
        (twoArgHook (fun input output ->
            chatMessageFor host services.ChildAgentRegistry services.SessionLifecycleObserver input output))

    setKey result "tool.definition" (twoArgHook (fun input output -> toolDefinitionFor host input output))
    setKey result "tool.execute.before" (twoArgHook (fun input output -> toolExecuteBeforeFor host input output))

    setKey
        result
        "tool.execute.after"
        (twoArgHook (fun input output ->
            toolExecuteAfterFor
                host
                services.Directory
                services.SessionLifecycleObserver
                services.ChildAgentRegistry
                services.RuntimeScope
                input
                output))

let private registerTransformHooks (result: obj) (client: obj) (services: CoreServices) =
    setKey
        result
        "experimental.chat.messages.transform"
        (twoArgHook (fun input output ->
            messagesTransform
                services.ChildAgentRegistry
                services.Directory
                services.RuntimeScope
                services.BacklogSession
                services.ReviewStore
                client
                input
                output))

    setKey
        result
        "experimental.session.compacting"
        (twoArgHook (fun input output ->
            CompactionTransform.compactingTransform
                services.Directory
                services.RuntimeScope
                services.BacklogSession
                input
                output))

    setKey
        result
        "experimental.compaction.autocontinue"
        (twoArgHook (fun input output -> compactionAutocontinue input output))

    setKey
        result
        "experimental.chat.system.transform"
        (twoArgHook (fun input output -> HookTransform.systemTransform services.Directory input output))

let private handleSessionCleanup (services: CoreServices) (env: HostEventEnvelope) : JS.Promise<unit> =
    promise {
        let ptyCleanupSessionId =
            if
                env.EventType = "session.deleted"
                || env.EventType = "session.delete"
                || env.EventType = "session.remove"
                || env.EventType = "session.close"
            then
                getSessionID env.EventType env.Props
            else
                ""

        if ptyCleanupSessionId <> "" then
            cleanupPtyBySession ptyCleanupSessionId

            Wanxiangshu.Runtime.RuntimeScopeForgetSession.forgetSession services.RuntimeScope ptyCleanupSessionId

            services.FallbackRuntime.CleanupSession ptyCleanupSessionId
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance ptyCleanupSessionId
            Wanxiangshu.Runtime.ToolHookRuntime.closeSession ptyCleanupSessionId

            let sid = SessionId.create ptyCleanupSessionId
            let eventStore = SubsessionEventStore.create services.Directory
            do! eventStore.Append(sid, [ PhysicalSessionClosed sid ])
            SubsessionActorRegistry.ClearPoison services.Directory ptyCleanupSessionId
            SubsessionActorRegistry.Remove services.Directory ptyCleanupSessionId
            Wanxiangshu.Runtime.SubsessionPendingEvidence.SubsessionPendingEvidence.ForgetSession ptyCleanupSessionId

            // Tear down the per-session dispatch mailbox in
            // one place.  NotifySessionClosed is idempotent: it is a
            // no-op if no dispatcher is registered for the session.
            let ws =
                Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick ("opencode:" + services.Directory)

            sharedDispatchRegistry.NotifySessionClosed ws ptyCleanupSessionId
            Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup.forget ptyCleanupSessionId
    }

let private registerEventHooks (result: obj) (ctx: obj) (services: CoreServices) =
    setKey
        result
        "command.execute.before"
        (twoArgHook (fun input output ->
            promise {
                do! services.SessionLifecycleObserver.handleCommandExecuteBefore input output

                do!
                    commandExecuteBefore
                        services.ChildAgentRegistry
                        ctx
                        services.ReviewStore
                        services.RuntimeScope
                        input
                        output
            }))

    setKey
        result
        "event"
        (box (fun (input: obj) ->
            match decodeHostEventEnvelope input with
            | None -> promise { return () }
            | Some env when not (isPluginObservedHostEvent env.EventType) -> promise { return () }
            | Some env ->
                // Filter: discard assistant streaming events for non-child (main) sessions.
                // The fallback FSM has no use for incremental token updates — it only
                // needs the terminal session.idle / stream-end / session.error signals.
                let isAssistantStream =
                    env.EventType = "message.updated"
                    && (let info = Dyn.get env.Props "info"
                        not (Dyn.isNullish info) && Dyn.str info "role" = "assistant")

                let sessionId = getSessionID env.EventType env.Props
                let isChild = isChildSession services.Directory sessionId

                if isAssistantStream && not isChild then
                    promise { return () }
                else
                    promise {
                        do! EventHooks.eventHandler services.ReviewStore services.RuntimeScope ctx input
                        do! handleSessionCleanup services env
                        do! services.SessionLifecycleObserver.handleEvent input
                    }))

let private handleSessionPostError
    (services: CoreServices)
    (sessionID: string)
    (outcome: string)
    (errorMsg: string)
    : JS.Promise<unit> =
    promise {
        let isAbort =
            FinishReason.isAbort (FinishReason.fromString outcome)
            || FinishReason.isAbort (FinishReason.fromString errorMsg)

        let errName = if isAbort then "MessageAbortedError" else "APIError"

        let rawEvent =
            box
                {| event =
                    {| ``type`` = "session.error"
                       properties =
                        {| sessionID = sessionID
                           info = {| sessionID = sessionID |}
                           error =
                            {| name = errName
                               message = errorMsg
                               isRetryable = (not isAbort) |} |} |} |}

        do! services.SessionLifecycleObserver.handleEvent rawEvent
    }

let private registerSessionPostHooks (result: obj) (services: CoreServices) =
    setKey
        result
        "session.post"
        (twoArgHook (fun input output ->
            promise {
                let outcome = Wanxiangshu.Runtime.Dyn.str input "outcome"
                let errorMsg = Wanxiangshu.Runtime.Dyn.str input "error"

                if outcome = "error" || outcome = "cancelled" || errorMsg <> "" then
                    let sessionID = Wanxiangshu.Runtime.Dyn.str input "sessionID"
                    do! handleSessionPostError services sessionID outcome errorMsg
            }))

    setKey
        result
        "session.userQuery.post"
        (twoArgHook (fun input output ->
            promise {
                let errorMsg = Wanxiangshu.Runtime.Dyn.str input "error"

                if errorMsg <> "" then
                    let sessionID = Wanxiangshu.Runtime.Dyn.str input "sessionID"
                    do! handleSessionPostError services sessionID "" errorMsg
            }))

let registerHooks (result: obj) (host: Host) (ctx: obj) (services: CoreServices) =
    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

    registerToolHooks result host services
    registerTransformHooks result client services
    registerEventHooks result ctx services
    registerSessionPostHooks result services
