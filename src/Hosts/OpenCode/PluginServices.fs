module Wanxiangshu.Hosts.Opencode.PluginServices

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
open Wanxiangshu.Hosts.Opencode.BacklogSession
open Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Hosts.Opencode.PtySpawn
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapter

type CoreServices =
    { ReviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore
      ChildAgentRegistry: ChildAgentRegistry
      SessionLifecycleObserver: SessionLifecycleObserver
      Directory: string
      RuntimeScope: RuntimeScope
      BacklogSession: BacklogSession
      Tools: obj
      McpMap: obj
      FallbackConfig: FallbackConfig option
      FallbackRuntime: FallbackRuntimeStore }

// ARCHITECTURE_EXEMPT: split this 99-line function later
let createCoreServices (host: Host) (ctx: obj) =
    let reviewStore = Wanxiangshu.Runtime.ReviewRuntime.createReviewStore ()
    let childAgentRegistry = ChildAgentRegistry.Create()
    let finderCache = FinderCache()

    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

    let directory = pluginDirectoryFromCtx ctx

    let fallbackConfigOpt =
        Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader.loadFallbackConfig directory

    let fallbackRuntime = FallbackRuntimeStore()

    let fallbackConfigLookup: Wanxiangshu.Runtime.Fallback.Ports.ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.emptyConfig)

    let fallbackHandler =
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

    let scope = create ()
    scope.Add("fallbackRuntime", box fallbackRuntime)
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
            promise {
                // Initialization barrier: reconcile unfinished subsession runs before anything else.
                do!
                    Wanxiangshu.Runtime.SubsessionReconcile.reconcileUnfinishedRuns
                        dir
                        (Some(fun _ ->
                            match getClientFromPluginCtx ctx with
                            | Ok client -> createHost client "" dir
                            | Error _ -> createHost (box null) "" dir))
                    |> FablePromise.map ignore

                return!
                    Wanxiangshu.Runtime.EventLogRuntime.syncAllSessionsFromEventLogDedicated host reviewStore scope dir
            })

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

let createReviewTestSurface (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore) : obj =
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
            Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader.buildAgentModelOverrides fbCfg

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

            match Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader.defaultPreferredModel fbCfg with
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
