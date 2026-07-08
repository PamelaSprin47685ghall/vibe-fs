module Wanxiangshu.Opencode.PluginCore

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Opencode.CommandHooks
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Opencode.Tools
open Wanxiangshu.Shell.TitleFetchGuardCommon
open Wanxiangshu.Opencode.PluginCoreServices
open Wanxiangshu.Opencode.PluginCoreHooks

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
