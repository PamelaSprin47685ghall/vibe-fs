module VibeFs.MuxPlugin.Registration

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.MuxPlugin.MuxTools
open VibeFs.MuxPlugin.MuxWrappers
open VibeFs.MuxPlugin.MuxEventHook
open VibeFs.MuxPlugin.MuxSlashCommands

let private toolsToObject (tools: VibeFs.Mux.Contract.ToolDefinition array) : obj =
    createObj [ for t in tools -> t.name, box t ]

/// The mux PluginRegistration: tools + mcp servers + wrappers + context injector
/// + event hook + slash commands + policy.
let createRegistration (_deps: obj) : obj =
    let reviewStore = VibeFs.Kernel.ReviewRuntime.createReviewStore ()
    let tools = createToolCatalog _deps reviewStore
    let toolNames = tools |> Array.map (fun t -> t.name)
    let toolsObj = toolsToObject tools
    let mcpServers = box {| ``stealth-browser-mcp`` = VibeFs.Kernel.McpConfig.getStealthBrowserMcpCommand () |}
    let wrappers = createAllWrappers toolsObj
    let eventHook = createEventHook reviewStore
    let slashCommands = createSlashCommands _deps reviewStore
    box {| toolNames = toolNames
           tools = tools
           wrappers = wrappers
           mcpServers = mcpServers
           contextInjector = box {| inject = (fun (projectPath: string) ->
                (async {
                    let! files = VibeFs.Shell.CapsShell.findCapsFiles projectPath |> Async.AwaitPromise
                    return if List.isEmpty files then box null else box (VibeFs.Kernel.CapsFormat.buildCapitalsContext files)
                } |> Async.StartAsPromise) :> obj) |}
           eventHook = eventHook
           slashCommands = slashCommands
           getToolPolicy = (fun (_agentId: string) (role: obj) ->
                let roleOpt = if Dyn.isNullish role then None else Some(string role)
                match VibeFs.Kernel.MuxPolicy.getPluginToolPolicy roleOpt with
                | Some policy -> box policy
                | None -> box null) |}
