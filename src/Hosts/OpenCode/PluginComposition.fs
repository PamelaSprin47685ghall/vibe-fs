module Wanxiangshu.Hosts.Opencode.PluginComposition

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Opencode.AgentConfig
open Wanxiangshu.Hosts.Opencode.CommandHooks
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.Tools
open Wanxiangshu.Runtime.TitleFetchGuard
open Wanxiangshu.Hosts.Opencode.PluginServices
open Wanxiangshu.Hosts.Opencode.PluginHooks
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Kernel.HostCapability

let pluginForWithSeams
    (host: Host)
    (ctx: obj)
    : JS.Promise<
          {| Plugin: obj
             ReviewStore: obj
             FallbackRuntime: FallbackRuntimeStore |}
       >
    =
    promise {
        Wanxiangshu.Runtime.E2eSandbox.applyFromProcessEnv ()
        installTitleFetchGuard ()
        let services = createCoreServices host ctx
        let result = emptyObj ()
        setKey result "id" (box "wanxiangshu")
        setKey result "name" (box "wanxiangshu")
        setKey result "mcp" services.McpMap
        setKey result "tool" services.Tools
        setKey result "capabilities" (box (toStringArray allFull))

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

        return
            {| Plugin = result
               ReviewStore = createReviewTestSurface services.ReviewStore
               FallbackRuntime = services.FallbackRuntime |}
    }

let pluginFor (host: Host) (ctx: obj) : JS.Promise<obj> =
    promise {
        let! seams = pluginForWithSeams host ctx
        return seams.Plugin
    }
