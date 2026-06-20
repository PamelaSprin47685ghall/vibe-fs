module VibeFs.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Config
open VibeFs.Shell.CallStore
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.SubagentTools
open VibeFs.Mux.HostTools
open VibeFs.Mux.EventHook
open VibeFs.Mux.SlashCommands
open VibeFs.Kernel.Dyn
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.WorkspaceFiles

[<Global("process")>]
let private nodeProcess : obj = jsNative

let private envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

type CapsFileReadEntry =
    { path: string
      callId: string
      input: {| path: string |}
      output: {| success: bool; file_size: int; modifiedTime: string; lines_read: int; content: string |} }

let private toolsToObject (tools: ToolDefinition array) : obj =
    createObj [ for t in tools -> t.name, box t ]

let buildCapsFileReadData (projectRoot: string) : JS.Promise<CapsFileReadEntry[]> =
    async {
        let! files = findCapsFiles projectRoot |> Async.AwaitPromise
        if List.isEmpty files then return [||]
        else
            let timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            let token = string timestamp
            let modified = System.DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime.ToString("O")
            return
                files
                |> Array.ofList
                |> Array.mapi (fun index f ->
                    { path = f.label
                      callId = $"caps-fr-{token}-{index}"
                      input = {| path = f.label |}
                      output = {| success = true
                                  file_size = f.content.Length
                                  modifiedTime = modified
                                  lines_read = f.content.Split('\n').Length
                                  content = f.content.Split('\n') |> Array.mapi (fun i line -> $"{i + 1}\t{line}") |> String.concat "\n" |} })
    }
    |> Async.StartAsPromise

let createToolCatalog
    (deps: obj)
    (toolNames: string array)
    (callStore: CallStore)
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (hostReadExec: HostReadExec)
    (finderCache: FinderCache)
    : ToolDefinition array =
    let tools =
        [| coderTool deps toolNames
           investigatorTool deps toolNames
           meditatorTool deps toolNames
           browserTool deps toolNames
           executorTool deps
           submitReviewTool deps toolNames callStore reviewStore
           websearchTool deps
           webfetchTool
           fuzzyGrepTool finderCache
           fuzzyFindTool finderCache
           writeTool deps
           readTool deps hostReadExec |]
    tools

let createRegistration (deps: obj) : obj =
    let callStore = createCallStore ()
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostReadExec()
    let finderCache = FinderCache()
    let toolNames =
        [| "coder"; "investigator"; "meditator"; "browser"; "executor"
           "submit_review"; "websearch"; "webfetch"; "fuzzy_grep"; "fuzzy_find"; "write"; "read" |]
    let tools = createToolCatalog deps toolNames callStore reviewStore hostReadExec finderCache
    let toolsObj = toolsToObject tools
    let mcpServers = box {| ``stealth-browser-mcp`` = VibeFs.Kernel.Config.getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}
    let wrappers = createAllWrappers toolsObj hostReadExec callStore
    let eventHook = createEventHook reviewStore
    let slashCommands = createSlashCommands deps toolNames callStore reviewStore
    box {| toolNames = toolNames
           tools = tools
           wrappers = wrappers
           mcpServers = mcpServers
           contextInjector =
               box {| inject = (fun (projectPath: string) ->
                   async {
                       let! files = findCapsFiles projectPath |> Async.AwaitPromise
                       return if List.isEmpty files then box null else box (VibeFs.Kernel.CapsFormat.buildCapitalsContext files)
                   } |> Async.StartAsPromise :> obj) |}
           eventHook = eventHook
           slashCommands = slashCommands
           getToolPolicy = (fun (_agentId: string) (role: obj) ->
               let agent = if Dyn.isNullish role then "manager" else string role
               let remove = toolNames |> Array.filter (fun t -> not (canUse agent t))
               box {| add = [||]; remove = remove |}) |}
