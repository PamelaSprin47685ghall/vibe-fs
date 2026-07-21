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
open Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.FallbackChainResolution
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Hosts.Opencode.PluginServiceLoader
open Wanxiangshu.Hosts.Opencode.PtySpawn

module Dyn = Wanxiangshu.Runtime.Dyn

type CoreServices =
    { ReviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore
      ChildAgentRegistry: ChildAgentRegistry
      SessionLifecycleObserver: SessionLifecycleObserver
      Directory: string
      RuntimeScope: RuntimeScope
      Tools: obj
      McpMap: obj
      FallbackConfig: FallbackConfig option
      FallbackRuntime: FallbackRuntimeStore }

let private registerPluginHooks (host: Host) (ctx: obj) (parts: PluginServiceParts) : CoreServices =
    let scope = parts.Scope
    let reviewStore = parts.ReviewStore

    scope.OnInit <- Some(buildScopeInit host ctx reviewStore scope)
    scope.TriggerInit(parts.Directory)

    let client =
        match getClientFromPluginCtx ctx with
        | Ok c -> c
        | Error _ -> box null

    if not (Dyn.isNullish client) then
        storePtyClient client

    { ReviewStore = parts.ReviewStore
      ChildAgentRegistry = parts.ChildAgentRegistry
      SessionLifecycleObserver = parts.LifecycleObserver
      Directory = parts.Directory
      RuntimeScope = parts.Scope
      Tools = parts.Tools
      McpMap = buildMcpMap ()
      FallbackConfig = parts.FallbackConfigOpt
      FallbackRuntime = parts.FallbackRuntime }

let createCoreServices (host: Host) (ctx: obj) =
    let parts = loadPluginServices host ctx
    registerPluginHooks host ctx parts

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
