module VibeFs.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Config
open VibeFs.Kernel.HostTools
open VibeFs.Shell.CallStore
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.SubagentTools
open VibeFs.Mux.BuiltinTools
open VibeFs.Mux.EventHook
open VibeFs.Mux.SlashCommands
open VibeFs.Mux.WikiTools
open VibeFs.Kernel.Dyn
open VibeFs.Mux.ReadDedup
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.WorkspaceFiles
open VibeFs.Shell.WikiFiles
open VibeFs.Mux.MessageTransform

let muxToolNames =
    [| "coder"; "investigator"; "meditator"; "browser"; "executor"
       "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "fuzzy_grep"; "fuzzy_find"; "write"; "read"
       "fetch_wiki"; "return_bookkeeper" |]

let private canUseMuxTopLevel (agent: string) (toolName: string) : bool =
    match agent, toolName with
    | "manager", "write" -> true
    | _ -> canUse agent toolName

let private buildToolPolicy (toolNames: string array) (role: obj) : obj =
    let agent = if Dyn.isNullish role then "manager" else string role
    let remove = toolNames |> Array.filter (fun t -> not (canUseMuxTopLevel agent t))
    box {| add = [||]; remove = remove |}

let getPluginToolPolicy (agentId: string) (role: obj) : obj =
    buildToolPolicy muxToolNames role

let collectReadOutputs (messages: obj array) : string[] =
    ReadDedup.collectReadOutputs messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    ReadDedup.deduplicateReadOutputsWithSeen seenOutputs messages

let deduplicateModelReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : string[] * obj[] =
    ReadDedup.deduplicateModelReadOutputsWithSeen seenOutputs messages

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
    promise {
        let! files = findCapsFiles projectRoot
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

let createToolCatalog
    (deps: obj)
    (toolNames: string array)
    (callStore: CallStore)
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (hostReadExec: HostReadExec)
    (finderCache: FinderCache)
    (wikiRuntime: MuxWikiRuntime)
    (wikiEnabled: bool)
    : ToolDefinition array =
    let executorWikiRuntime =
        if wikiEnabled then
            box (createObj [
                "startBookkeeperAppend",
                box (System.Func<string, string, string, obj, unit>(fun prompt result title config ->
                    wikiRuntime.StartBookkeeperAppend(prompt, result, title, config)))
            ])
        else null
    [| coderTool deps toolNames
       investigatorTool deps toolNames
       meditatorTool deps toolNames
       browserTool deps toolNames
       executorTool deps executorWikiRuntime
       submitReviewTool deps toolNames callStore reviewStore
       returnReviewerTool deps callStore reviewStore
       websearchTool deps
       webfetchTool
       fuzzyGrepTool finderCache
       fuzzyFindTool finderCache
       writeTool deps
       readTool deps hostReadExec
       if wikiEnabled then
           fetchWikiTool wikiRuntime
           returnBookkeeperTool wikiRuntime |]

let createRegistration (deps: obj) : obj =
    let callStore = createCallStore ()
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostReadExec()
    let finderCache = FinderCache()
    let wikiRuntime = MuxWikiRuntime(deps)
    let toolNames = muxToolNames
    let directory =
        let dir = Dyn.str deps "directory"
        if dir <> "" then dir else Dyn.str deps "cwd"
    let wikiEnabled = wikiDirExists directory
    let tools = createToolCatalog deps toolNames callStore reviewStore hostReadExec finderCache wikiRuntime wikiEnabled
    let toolsObj = toolsToObject tools
    let mcpServers = box {| ``stealth-browser-mcp`` = VibeFs.Kernel.Config.getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}
    let wrappers = createAllWrappers toolsObj hostReadExec callStore
    let eventHook = createEventHook deps reviewStore
    let slashCommands = createSlashCommands deps toolNames callStore reviewStore
    let messagesTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            messagesTransform deps wikiRuntime reviewStore input output)
    box {| toolNames = toolNames
           tools = tools
           wrappers = wrappers
           mcpServers = mcpServers
           contextInjector =
               box {| inject = (fun (projectPath: string) ->
                   promise {
                       let! files = findCapsFiles projectPath
                       return if List.isEmpty files then box null else box (VibeFs.Kernel.CapsFormat.buildCapitalsContext files)
                   } :> obj) |}
           eventHook = eventHook
           slashCommands = slashCommands
           messagesTransform = box messagesTransformFn
           __wikiRuntime =
                box (createObj
                    [ "rawInstance", box wikiRuntime
                      "registerJobForTesting",
                      box (System.Func<string, string, string, obj, unit>(fun sessionID workspaceRoot kindTag payload ->
                          wikiRuntime.RegisterJobForTesting(sessionID, workspaceRoot, kindTag, payload)))
                      "startMaintenanceIfDue",
                      box (System.Func<string, JS.Promise<unit>>(fun workspaceRoot -> wikiRuntime.StartMaintenanceIfDue(workspaceRoot)))
                      "takeBookkeeperLaunchesForTesting",
                      box (System.Func<obj array>(fun () -> wikiRuntime.TakeBookkeeperLaunchesForTesting()))
                      "waitForBackgroundJobsForTesting",
                      box (System.Func<JS.Promise<unit>>(fun () -> wikiRuntime.WaitForBackgroundJobsForTesting())) ])
           __reviewStore =
               box (createObj
                   [ "activateReview",
                     box (System.Func<string, string, int64, unit>(fun sessionID task createdAt ->
                         reviewStore.activateReview(sessionID, task, createdAt)))
                     "deactivateReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.deactivateReview sessionID))
                     "isReviewActive", box (System.Func<string, bool>(fun sessionID -> reviewStore.isReviewActive sessionID))
                     "getReviewTask", box (System.Func<string, string option>(fun sessionID -> reviewStore.getReviewTask sessionID))
                     "tryLockReview", box (System.Func<string, bool>(fun sessionID -> reviewStore.tryLockReview sessionID))
                     "unlockReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.unlockReview sessionID)) ])
           __callStore =
               box (createObj
                   [ "resolveCall", box (System.Func<string, obj, bool>(fun callId args -> resolveCall callStore callId args))
                     "pendingCallIds", box (System.Func<string array>(fun () -> callStore.PendingCalls.Keys |> Seq.toArray))
                     "hasCall", box (System.Func<string, bool>(fun callId -> hasCall callStore callId))
                     "resolveFirstMatching",
                     box (System.Func<string, obj, bool>(fun prefix args ->
                         callStore.PendingCalls.Keys
                         |> Seq.tryFind (fun k -> k.StartsWith(prefix))
                         |> Option.map (fun k -> resolveCall callStore k args)
                         |> Option.defaultValue false)) ])
           getToolPolicy = (fun (_agentId: string) (role: obj) -> buildToolPolicy toolNames role) |}
