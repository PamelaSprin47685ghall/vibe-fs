module VibeFs.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.ToolPolicy
open VibeFs.Mux.CallStore
open VibeFs.Mux.ToolCatalog
open VibeFs.Mux.Wrappers
open VibeFs.Mux.EventHook
open VibeFs.Mux.SlashCommands
open VibeFs.Mux.CapsFileRead
open VibeFs.Mux.Dedup
open VibeFs.Shell.FuzzyFinderShell

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

let private toolsToObject (tools: VibeFs.Mux.Contract.ToolDefinition array) : obj =
    createObj [ for t in tools -> t.name, box t ]

let createRegistration (deps: obj) : obj =
    let callStore = createCallStore ()
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec : HostReadExec = { contents = None }
    let finderCache = FinderCache()
    let tools = createToolCatalog deps callStore reviewStore hostReadExec finderCache
    let toolNames = tools |> Array.map (fun t -> t.name)
    let toolsObj = toolsToObject tools
    let mcpServers = box {| ``stealth-browser-mcp`` = VibeFs.Kernel.McpConfig.getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}
    let wrappers = createAllWrappers toolsObj hostReadExec callStore
    let eventHook = createEventHook reviewStore
    let slashCommands = createSlashCommands deps callStore reviewStore
    box {| toolNames = toolNames
           tools = tools
           wrappers = wrappers
           mcpServers = mcpServers
           contextInjector =
               box {| inject = (fun (projectPath: string) ->
                   async {
                       let! files = VibeFs.Shell.Caps.findCapsFiles projectPath |> Async.AwaitPromise
                       return if List.isEmpty files then box null else box (VibeFs.Kernel.CapsFormat.buildCapitalsContext files)
                   } |> Async.StartAsPromise :> obj) |}
           eventHook = eventHook
           slashCommands = slashCommands
           getToolPolicy = (fun (_agentId: string) (role: obj) ->
               let agent = if Dyn.isNullish role then "manager" else string role
               let remove = toolNames |> Array.filter (fun t -> not (canUse agent t))
               box {| add = [||]; remove = remove |}) |}

let canUseTool (agent: string) (tool: string) : bool =
    canUse agent tool

let getPluginToolPolicy (_agentId: string) (role: string) : obj =
    let agent = if System.String.IsNullOrEmpty role then "manager" else role
    let remove = [| "coder"; "reader"; "meditator"; "browser"; "executor"; "submit_review"; "websearch"; "webfetch"; "fuzzy_find"; "fuzzy_grep"; "write"; "read" |]
                  |> Array.filter (fun t -> not (canUse agent t))
    box {| add = [||]; remove = remove |}

let buildCapsFileReadData (projectRoot: string) : JS.Promise<VibeFs.Mux.CapsFileRead.CapsFileReadEntry[]> =
    VibeFs.Mux.CapsFileRead.buildCapsFileReadData projectRoot

let deduplicateReadOutputs (messages: obj array) : obj array =
    VibeFs.Mux.Dedup.deduplicateReadOutputs messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj array =
    VibeFs.Mux.Dedup.deduplicateReadOutputsWithSeen (List.ofArray seenOutputs) messages |> snd

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj array =
    let seen, result = VibeFs.Mux.Dedup.deduplicateModelReadOutputsWithSeen (List.ofArray seenOutputs) messages
    Array.ofList seen, result

let deduplicateReadOutputsAgainstHistory (history: obj array) (messages: obj array) : obj array =
    let seenByPath = VibeFs.Mux.Dedup.collectReadOutputsByPath history
    VibeFs.Mux.Dedup.deduplicateReadOutputsWithSeenByPath seenByPath messages |> snd

let collectReadOutputs (messages: obj array) : string[] =
    VibeFs.Mux.Dedup.collectReadOutputs messages |> Array.ofList
