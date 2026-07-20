module Wanxiangshu.Hosts.Opencode.PluginHooks

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel
open Wanxiangshu.Hosts.Opencode.PluginCleanup
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
open Wanxiangshu.Runtime.Messaging.OpencodeSessionEventCodec
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
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
                        do! PluginCleanup.handleSessionCleanup services ctx env
                        do! services.SessionLifecycleObserver.handleEvent input
                    }))

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
                    do! PluginCleanup.handleSessionPostError services sessionID outcome errorMsg
            }))

    setKey
        result
        "session.userQuery.post"
        (twoArgHook (fun input output ->
            promise {
                let errorMsg = Wanxiangshu.Runtime.Dyn.str input "error"

                if errorMsg <> "" then
                    let sessionID = Wanxiangshu.Runtime.Dyn.str input "sessionID"
                    do! PluginCleanup.handleSessionPostError services sessionID "" errorMsg
            }))

let registerHooks (result: obj) (host: Host) (ctx: obj) (services: CoreServices) =
    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

    PluginCleanup.registerGlobalCleanup services

    registerToolHooks result host services
    registerTransformHooks result client services
    registerEventHooks result ctx services
    registerSessionPostHooks result services
