module Wanxiangshu.Hosts.Opencode.PluginServiceLoader

open Fable.Core
module FablePromise = Promise

open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Hosts.Opencode.AgentConfig
open Wanxiangshu.Hosts.Opencode.Tools
open Wanxiangshu.Hosts.Opencode.Fallback.Hook
open Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver
open Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.EventLogRuntimeRecovery
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Hosts.Opencode.PtySpawn
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter

type PluginServiceParts =
    { ReviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore
      ChildAgentRegistry: ChildAgentRegistry
      FinderCache: Wanxiangshu.Runtime.FuzzyFinderShell.FinderCache
      Directory: string
      FallbackConfigOpt: FallbackConfig option
      FallbackRuntime: FallbackRuntimeStore
      FallbackConfigLookup: Wanxiangshu.Runtime.Fallback.Ports.ConfigLookup
      FallbackHandler: (obj -> JS.Promise<FallbackHookResult>) option
      Scope: RuntimeScope
      LifecycleObserver: SessionLifecycleObserver
      Tools: obj }

let buildScopeInit
    (host: Host)
    (ctx: obj)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (scope: RuntimeScope)
    =
    fun dir ->
        promise {
            do!
                Wanxiangshu.Runtime.SubsessionReconcile.reconcileUnfinishedRuns
                    dir
                    (Some(fun _ ->
                        match getClientFromPluginCtx ctx with
                        | Ok client -> createHost client "" dir
                        | Error _ -> createHost (box null) "" dir))
                |> FablePromise.map ignore

            do! Wanxiangshu.Runtime.EventLogRuntimeSync.syncAllSessionsFromEventLogDedicated host reviewStore scope dir

            do! recoverRequestedFallbackLeases scope dir
        }

let buildMcpMap () : obj =
    let mcps =
        box
            {| ``type`` = "local"
               command =
                Wanxiangshu.Kernel.Config
                    .getStealthBrowserMcpLocalConfig(envVar "STEALTH_BROWSER_MCP_REF")
                    .command |}

    box {| ``stealth-browser-mcp`` = mcps |}

let private getClient (ctx: obj) =
    match getClientFromPluginCtx ctx with
    | Ok c -> c
    | Error _ -> box null

let buildFallbackHandler
    (client: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigLookup: Wanxiangshu.Runtime.Fallback.Ports.ConfigLookup)
    (directory: string)
    (childAgentRegistry: ChildAgentRegistry)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (ctx: obj)
    =
    Some(
        Wanxiangshu.Hosts.Opencode.Fallback.Hook.createOpencodeFallbackHandler
            client
            fallbackRuntime
            fallbackConfigLookup
            directory
            childAgentRegistry
            reviewStore
            (Some ctx)
    )

let loadPluginServices (host: Host) (ctx: obj) : PluginServiceParts =
    let reviewStore = Wanxiangshu.Runtime.ReviewRuntime.createReviewStore ()
    let childAgentRegistry = ChildAgentRegistry.Create()
    let finderCache = FinderCache()

    let client = getClient ctx
    let directory = pluginDirectoryFromCtx ctx

    let fallbackConfigOpt =
        Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader.loadFallbackConfig directory

    let fallbackRuntime = FallbackRuntimeStore()

    let fallbackConfigLookup: Wanxiangshu.Runtime.Fallback.Ports.ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.emptyConfig)

    let fallbackHandler =
        buildFallbackHandler client fallbackRuntime fallbackConfigLookup directory childAgentRegistry reviewStore ctx

    let scope = create ()
    scope.Add("fallbackRuntime", box fallbackRuntime)

    registerFallbackExecutor
        scope
        (Wanxiangshu.Hosts.Opencode.Fallback.ActionExecutor.opencodeActionExecutorWithDir
            fallbackRuntime
            client
            directory)

    let lifecycleObserver =
        createSessionLifecycleObserver (
            host,
            ctx,
            reviewStore,
            childAgentRegistry,
            fallbackHandler,
            fallbackRuntime,
            scope
        )

    let tools =
        createTools host childAgentRegistry finderCache ctx reviewStore scope fallbackRuntime

    { ReviewStore = reviewStore
      ChildAgentRegistry = childAgentRegistry
      FinderCache = finderCache
      Directory = directory
      FallbackConfigOpt = fallbackConfigOpt
      FallbackRuntime = fallbackRuntime
      FallbackConfigLookup = fallbackConfigLookup
      FallbackHandler = fallbackHandler
      Scope = scope
      LifecycleObserver = lifecycleObserver
      Tools = tools }
