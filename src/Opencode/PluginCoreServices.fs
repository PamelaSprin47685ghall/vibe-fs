module Wanxiangshu.Opencode.PluginCoreServices

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Opencode.Tools
open Wanxiangshu.Opencode.FallbackHooks
open Wanxiangshu.Opencode.SessionLifecycleObserver
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Opencode.FallbackConfigLoader
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackEventBridge
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeClientCodec
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Opencode.PtySpawn

type CoreServices =
    { ReviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore
      ChildAgentRegistry: ChildAgentRegistry
      SessionLifecycleObserver: SessionLifecycleObserver
      Directory: string
      RuntimeScope: RuntimeScope
      BacklogSession: BacklogSession
      Tools: obj
      McpMap: obj
      FallbackConfig: FallbackConfig option
      FallbackRuntime: FallbackRuntimeState }

let createCoreServices (host: Host) (ctx: obj) =
    let reviewStore = Wanxiangshu.Shell.ReviewRuntime.createReviewStore ()
    let childAgentRegistry = ChildAgentRegistry.Create()
    let finderCache = FinderCache()

    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

    let directory = pluginDirectoryFromCtx ctx

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
                directory
                childAgentRegistry
                reviewStore
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
      FallbackConfig = fallbackConfigOpt
      FallbackRuntime = fallbackRuntime }

let createReviewTestSurface (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore) : obj =
    createObj
        [ "applyReviewTaskProjection",
          box (
              System.Func<string, string option, unit>(fun sessionID task ->
                  reviewStore.applyReviewTaskProjection (sessionID, task))
          )
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
