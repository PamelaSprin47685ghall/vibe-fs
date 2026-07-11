module Wanxiangshu.Opencode.PluginCoreHooks

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel
open Wanxiangshu.Opencode.ChatHooks
open Wanxiangshu.Opencode.ToolDefinitionHooks
open Wanxiangshu.Opencode.HookExecute
open Wanxiangshu.Opencode.CommandHooks
open Wanxiangshu.Opencode.EventHooks
open Wanxiangshu.Opencode.MessageTransform
open Wanxiangshu.Opencode.HookTransform
open Wanxiangshu.Opencode.SessionLifecycleObserver
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodec
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Opencode.PtySpawn
open Wanxiangshu.Opencode.PluginCoreServices

let private twoArgHook (f: obj -> obj -> JS.Promise<unit>) =
    box (System.Func<obj, obj, JS.Promise<unit>>(f))

let registerHooks (result: obj) (host: Host) (ctx: obj) (services: CoreServices) =
    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

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
            compactingTransform
                services.ChildAgentRegistry
                services.Directory
                services.RuntimeScope
                services.BacklogSession
                client
                input
                output))

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
                promise {
                    do! EventHooks.eventHandler services.ReviewStore services.RuntimeScope ctx input

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
                        Wanxiangshu.Shell.LivelockGuard.cleanup services.RuntimeScope ptyCleanupSessionId

                    do! services.SessionLifecycleObserver.handleEvent input
                }))

    setKey
        result
        "session.post"
        (twoArgHook (fun input output ->
            promise {
                let outcome = Wanxiangshu.Shell.Dyn.str input "outcome"
                let errorMsg = Wanxiangshu.Shell.Dyn.str input "error"

                if outcome = "error" || outcome = "cancelled" || errorMsg <> "" then
                    let sessionID = Wanxiangshu.Shell.Dyn.str input "sessionID"

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
            }))

    setKey
        result
        "session.userQuery.post"
        (twoArgHook (fun input output ->
            promise {
                let errorMsg = Wanxiangshu.Shell.Dyn.str input "error"

                if errorMsg <> "" then
                    let sessionID = Wanxiangshu.Shell.Dyn.str input "sessionID"

                    let isAbort = FinishReason.isAbort (FinishReason.fromString errorMsg)

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
            }))

    setKey
        result
        "experimental.chat.system.transform"
        (twoArgHook (fun input output -> HookTransform.systemTransform services.Directory input output))
