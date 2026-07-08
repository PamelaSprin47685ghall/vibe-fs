module Wanxiangshu.Opencode.PluginCore

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Opencode.CommandHooks
open Wanxiangshu.Opencode.ChatHooks
open Wanxiangshu.Opencode.MessageTransform
open Wanxiangshu.Opencode.ToolDefinitionHooks
open Wanxiangshu.Opencode.EventHooks
open Wanxiangshu.Opencode.Tools
open Wanxiangshu.Opencode.HookExecute
open Wanxiangshu.Opencode.PtySpawn
open Wanxiangshu.Shell.TitleFetchGuardCommon
open Wanxiangshu.Opencode.SessionLifecycleObserver
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell
open Wanxiangshu.Shell.Clock
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec

let private twoArgHook (f: obj -> obj -> JS.Promise<unit>) =
    box (System.Func<obj, obj, JS.Promise<unit>>(f))

type private CoreServices =
    { ReviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore
      ChildAgentRegistry: ChildAgentRegistry
      SessionLifecycleObserver: SessionLifecycleObserver
      Directory: string
      RuntimeScope: RuntimeScope
      BacklogSession: BacklogSession
      Tools: obj
      McpMap: obj
      FallbackConfig: FallbackConfig option }

let private createCoreServices (host: Host) (ctx: obj) =
    let reviewStore = Wanxiangshu.Shell.ReviewRuntime.createReviewStore ()
    let childAgentRegistry = ChildAgentRegistry.Create()
    let finderCache = FinderCache()

    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

    let directory = pluginDirectoryFromCtx ctx

    // Fallback config: read AGENTS.md frontmatter `models:` section
    let fallbackConfigOpt =
        Wanxiangshu.Opencode.FallbackConfigLoader.loadFallbackConfig directory

    let fallbackRuntime = Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()

    let fallbackConfigLookup: Wanxiangshu.Shell.FallbackEventBridge.ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> Wanxiangshu.Shell.FallbackConfigCodec.emptyConfig)

    let fallbackHandler =
        Some(
            Wanxiangshu.Opencode.FallbackHooks.createOpencodeFallbackHandler
                client
                fallbackRuntime
                fallbackConfigLookup
                childAgentRegistry
        )

    let scope = create ()
    let backlogSession = BacklogSession(host, scope)

    let lifecycleObserver =
        createSessionLifecycleObserver (
            host,
            ctx,
            reviewStore,
            childAgentRegistry,
            fallbackHandler,
            fallbackRuntime,
            backlogSession
        )

    let tools =
        createTools host childAgentRegistry finderCache ctx reviewStore scope fallbackRuntime

    scope.OnInit <-
        Some(fun dir ->
            Wanxiangshu.Shell.EventLogRuntime.syncAllSessionsFromEventLogDedicated host reviewStore scope dir)

    scope.TriggerInit(directory)

    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

    if not (Dyn.isNullish client) then
        storePtyClient client

    let mcps =
        box
            {| ``type`` = "local"
               command =
                Wanxiangshu.Kernel.Config
                    .getStealthBrowserMcpLocalConfig(envVar "STEALTH_BROWSER_MCP_REF")
                    .command |}

    let mcpMap = box {| ``stealth-browser-mcp`` = mcps |}

    { ReviewStore = reviewStore
      ChildAgentRegistry = childAgentRegistry
      SessionLifecycleObserver = lifecycleObserver
      Directory = directory
      RuntimeScope = scope
      BacklogSession = backlogSession
      Tools = tools
      McpMap = mcpMap
      FallbackConfig = fallbackConfigOpt }

let private registerHooks (result: obj) (host: Host) (ctx: obj) (services: CoreServices) =
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

    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

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
            promise {
                do! eventHandler services.ReviewStore services.RuntimeScope input

                let ptyCleanupSessionId =
                    match Wanxiangshu.Shell.OpencodeHookInputCodec.decodeHostEventEnvelope input with
                    | Some e when
                        e.EventType = "session.deleted"
                        || e.EventType = "session.delete"
                        || e.EventType = "session.remove"
                        || e.EventType = "session.close"
                        ->
                        Wanxiangshu.Shell.OpencodeSessionEventCodec.getSessionID e.EventType e.Props
                    | _ -> ""

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
                let outcome = Dyn.str input "outcome"
                let errorMsg = Dyn.str input "error"

                if outcome = "error" || outcome = "cancelled" || errorMsg <> "" then
                    let sessionID = Dyn.str input "sessionID"

                    let isAbort =
                        outcome = "cancelled"
                        || errorMsg.ToLowerInvariant().Contains("abort")
                        || errorMsg.ToLowerInvariant().Contains("cancel")

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
                let errorMsg = Dyn.str input "error"

                if errorMsg <> "" then
                    let sessionID = Dyn.str input "sessionID"

                    let isAbort =
                        errorMsg.ToLowerInvariant().Contains("abort")
                        || errorMsg.ToLowerInvariant().Contains("cancel")

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

let private createReviewTestSurface (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    createObj
        [ "activateReview",
          box (
              System.Func<string, string, int64, unit>(fun sessionID task createdAt ->
                  reviewStore.activateReview (sessionID, task, createdAt))
          )
          "deactivateReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.deactivateReview sessionID))
          "getReviewTask",
          box (System.Func<string, string option>(fun sessionID -> reviewStore.getReviewTask sessionID))
          "tryLockReview", box (System.Func<string, bool>(fun sessionID -> reviewStore.tryLockReview sessionID))
          "unlockReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.unlockReview sessionID)) ]

let applyFallbackModelOverrides (cfg: obj) (fbCfgOpt: FallbackConfig option) : unit =
    match fbCfgOpt with
    | None -> ()
    | Some fbCfg ->
        let overrides =
            Wanxiangshu.Opencode.FallbackConfigLoader.buildAgentModelOverrides fbCfg

        let agentObj = Dyn.get cfg "agent"

        if Dyn.isNullish agentObj then
            ()
        else
            let setModelAndVariant (a: obj) (modelStr: string) =
                let parts = modelStr.Split(':')
                setKey a "model" (box (parts.[0].Trim()))

                if parts.Length > 1 then
                    setKey a "variant" (box (parts.[1].Trim()))
                else
                    Dyn.deleteKey a "variant"

            let agentKeys: string[] = unbox (JS.Constructors.Object.keys agentObj)

            for kv in overrides do
                for i = 0 to agentKeys.Length - 1 do
                    let origKey = agentKeys.[i]

                    if normalizeAgentName origKey = kv.Key then
                        let a = Dyn.get agentObj origKey

                        if not (Dyn.isNullish a) then
                            setModelAndVariant a kv.Value

            match Wanxiangshu.Opencode.FallbackConfigLoader.defaultPreferredModel fbCfg with
            | Some dm ->
                let hasOverride (origKey: string) =
                    overrides |> Seq.exists (fun kv -> kv.Key = normalizeAgentName origKey)

                for i = 0 to agentKeys.Length - 1 do
                    let k = agentKeys.[i]

                    if not (hasOverride k) then
                        let a = Dyn.get agentObj k

                        if not (Dyn.isNullish a) then
                            setModelAndVariant a dm
            | None -> ()

let pluginFor (host: Host) (ctx: obj) : JS.Promise<obj> =
    promise {
        installTitleFetchGuard ()
        let services = createCoreServices host ctx
        let result = emptyObj ()
        setKey result "id" (box "wanxiangshu")
        setKey result "name" (box "wanxiangshu")
        setKey result "mcp" services.McpMap
        setKey result "tool" services.Tools

        setKey
            result
            "config"
            (box (fun (cfg: obj) ->
                promise {
                    let next = applyAgentConfigFor host cfg services.McpMap
                    registerCommands cfg
                    applyFallbackModelOverrides next services.FallbackConfig
                    return assignInto cfg next
                }))

        registerHooks result host ctx services
        services.RuntimeScope.TriggerInit(services.Directory)
        do! services.RuntimeScope.WaitInit()
        setKey result "__reviewStore" (box (createReviewTestSurface services.ReviewStore))
        return result
    }
