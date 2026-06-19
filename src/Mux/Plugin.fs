module VibeFs.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Config
open VibeFs.Mux.CallStore
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
    (callStore: CallStore)
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (hostReadExec: HostReadExec)
    (finderCache: FinderCache)
    : ToolDefinition array =
    let tools =
        [| coderTool deps
           investigatorTool deps
           meditatorTool deps
           browserTool deps
           executorTool deps
           submitReviewTool deps callStore reviewStore
           websearchTool deps
           webfetchTool
           fuzzyGrepTool finderCache
           fuzzyFindTool finderCache
           writeTool deps
           readTool deps hostReadExec |]
    registeredToolNames <- tools |> Array.map (fun t -> t.name)
    tools

let createRegistration (deps: obj) : obj =
    let callStore = createCallStore ()
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec : HostReadExec = { contents = None }
    let finderCache = FinderCache()
    let tools = createToolCatalog deps callStore reviewStore hostReadExec finderCache
    let toolNames = tools |> Array.map (fun t -> t.name)
    let toolsObj = toolsToObject tools
    let mcpServers = box {| ``stealth-browser-mcp`` = VibeFs.Kernel.Config.getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}
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
                       let! files = findCapsFiles projectPath |> Async.AwaitPromise
                       return if List.isEmpty files then box null else box (VibeFs.Kernel.CapsFormat.buildCapitalsContext files)
                   } |> Async.StartAsPromise :> obj) |}
           eventHook = eventHook
           slashCommands = slashCommands
           getToolPolicy = (fun (_agentId: string) (role: obj) ->
               let agent = if Dyn.isNullish role then "manager" else string role
               let remove = toolNames |> Array.filter (fun t -> not (canUse agent t))
               box {| add = [||]; remove = remove |}) |}

let getPluginToolPolicy (_agentId: string) (role: string) : obj =
    let agent = if System.String.IsNullOrEmpty role then "manager" else role
    let remove = [| "coder"; "investigator"; "meditator"; "browser"; "executor"; "submit_review"; "websearch"; "webfetch"; "fuzzy_find"; "fuzzy_grep"; "write"; "read" |]
                  |> Array.filter (fun t -> not (canUse agent t))
    box {| add = [||]; remove = remove |}

let deduplicateReadOutputs (messages: obj array) : obj array =
    VibeFs.Kernel.MessageDedup.deduplicateReadOutputs messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj array =
    VibeFs.Kernel.MessageDedup.deduplicateReadOutputsWithSeen (List.ofArray seenOutputs) messages |> snd

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj array =
    let seen, result = VibeFs.Kernel.MessageDedup.deduplicateModelReadOutputsWithSeen (List.ofArray seenOutputs) messages
    Array.ofList seen, result

let deduplicateReadOutputsAgainstHistory (history: obj array) (messages: obj array) : obj array =
    let seenByPath = VibeFs.Kernel.MessageDedup.collectReadOutputsByPath history
    VibeFs.Kernel.MessageDedup.deduplicateReadOutputsWithSeenByPath seenByPath messages |> snd

let collectReadOutputs (messages: obj array) : string[] =
    VibeFs.Kernel.MessageDedup.collectReadOutputs messages |> Array.ofList
