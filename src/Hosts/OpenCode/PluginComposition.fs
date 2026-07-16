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

let pluginFor (host: Host) (ctx: obj) : JS.Promise<obj> =
    promise {
        Wanxiangshu.Runtime.E2eSandbox.applyFromProcessEnv ()
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
        setKey result "__fallbackRuntime" (box services.FallbackRuntime)
        return result
    }
