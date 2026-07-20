module Wanxiangshu.Hosts.Opencode.PluginCleanup

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Hosts.Opencode.ChatHooksMessageIdDedup
open Wanxiangshu.Hosts.Opencode.PtySpawn
open Wanxiangshu.Hosts.Opencode.PluginServices

let handleSessionCleanup (services: CoreServices) (ctx: obj) (env: HostEventEnvelope) : JS.Promise<unit> =
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
            let sid = SessionId.create ptyCleanupSessionId
            let eventStore = SubsessionEventStore.create services.Directory
            do! eventStore.Append(sid, [ PhysicalSessionClosed sid ])
            SubsessionActorRegistry.ClearPoison services.Directory ptyCleanupSessionId
            SubsessionActorRegistry.Remove services.Directory ptyCleanupSessionId

            let client =
                match getClientFromPluginCtx ctx with
                | Ok c -> c
                | Error _ -> box null

            let children = services.ChildAgentRegistry.ResolveChildren ptyCleanupSessionId

            for childId in children do
                try
                    do!
                        SubagentIoCleanup.abortAndUnregister
                            services.ChildAgentRegistry
                            client
                            services.Directory
                            childId
                with _ ->
                    ()

            services.ChildAgentRegistry.UnregisterChildAgent ptyCleanupSessionId
    }

let handleSessionPostError
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

let registerGlobalCleanup (services: CoreServices) =
    SubsessionActorRegistry.RegisterGlobalCleanup(fun workspaceRoot sessionId ->
        if workspaceRoot = services.Directory && sessionId <> "" then
            services.FallbackRuntime.CleanupSession sessionId
            Wanxiangshu.Runtime.RuntimeScopeForgetSession.forgetSession services.RuntimeScope sessionId
            Wanxiangshu.Runtime.RunnerBackground.abortRunnerJobCore services.RuntimeScope sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.closeSession sessionId
            services.ReviewStore.CleanupSession sessionId
            Wanxiangshu.Runtime.SubsessionPendingEvidence.SubsessionPendingEvidence.ForgetSession sessionId
            services.ChildAgentRegistry.UnregisterChildAgent sessionId

            let ws =
                Wanxiangshu.Kernel.Primitives.Identity.Id.workspaceIdQuick ("opencode:" + services.Directory)

            sharedDispatchRegistry.NotifySessionClosed ws sessionId
            forget sessionId
            cleanupPtyBySession sessionId)
