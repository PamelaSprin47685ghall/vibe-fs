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
open VibeFs.Mux.KnowledgeGraphTools
open VibeFs.Kernel.Dyn
open VibeFs.Mux.ReadDedup
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.WorkspaceFiles
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Mux.MessageTransform

let muxToolNames =
    [| "coder"; "investigator"; "meditator"; "browser"; "executor"
       "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "fuzzy_grep"; "fuzzy_find"; "write"; "read"
       "knowledge_graph_fetch"; "return_bookkeeper" |]

let private canUseMuxTopLevel (agent: string) (toolName: string) : bool =
    canUseCanonical agent toolName

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

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

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
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    (knowledgeGraphEnabled: bool)
    : ToolDefinition array =
    [| yield coderTool deps toolNames
       yield investigatorTool deps toolNames
       yield meditatorTool deps toolNames
       yield browserTool deps toolNames
       yield executorTool deps toolNames null
       yield submitReviewTool deps toolNames callStore reviewStore
       yield returnReviewerTool deps callStore reviewStore
       yield websearchTool deps toolNames
       yield webfetchTool
       yield fuzzyGrepTool finderCache
       yield fuzzyFindTool finderCache
       yield writeTool deps
       yield readTool deps hostReadExec
       if knowledgeGraphEnabled then
           yield knowledgeGraphFetchTool knowledgeGraphRuntime
           yield returnBookkeeperTool knowledgeGraphRuntime |]

let private recordsToBookkeeper (tool: string) : bool =
    let allowed =
        [| "coder"; "investigator"; "meditator"; "browser"; "executor"
           "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "write" |]
    Array.contains tool allowed

let private isReadOnlyExecutor (tool: string) (input: obj) : bool =
    tool = "executor" && Dyn.str (Dyn.get input "args") "mode" = "ro"

let private bookkeeperInput (input: obj) : string =
    let args = Dyn.get input "args"
    if Dyn.isNullish args then "" else JS.JSON.stringify args

let private toolExecuteAfter
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    (knowledgeGraphEnabled: bool)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        if not knowledgeGraphEnabled then ()
        else
            let tool = Dyn.str input "tool"
            let sessionID = Dyn.str input "sessionID"
            let succeeded = Dyn.str output "error" = ""
            if succeeded
               && recordsToBookkeeper tool
               && not (isReadOnlyExecutor tool input) then
                knowledgeGraphRuntime.StartBookkeeperAppend(
                    bookkeeperInput input,
                    Dyn.str output "output",
                    tool,
                    config = createObj
                        [ "sessionID", box sessionID
                          "directory", box (Dyn.str input "directory") ])
    }

let createRegistration (deps: obj) : obj =
    let callStore = createCallStore ()
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostReadExec()
    let finderCache = FinderCache()
    let knowledgeGraphRuntime = MuxKnowledgeGraphRuntime(deps)
    let directory =
        let dir = Dyn.str deps "directory"
        if dir <> "" then dir else Dyn.str deps "cwd"
    let knowledgeGraphEnabled = knowledgeGraphDirExists directory
    let tools = createToolCatalog deps muxToolNames callStore reviewStore hostReadExec finderCache knowledgeGraphRuntime knowledgeGraphEnabled
    let toolsObj = toolsToObject tools
    let mcpServers = box {| ``stealth-browser-mcp`` = VibeFs.Kernel.Config.getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}
    let wrappers = createAllWrappers toolsObj hostReadExec callStore
    let eventHook = createEventHook deps reviewStore
    let slashCommands = createSlashCommands deps muxToolNames callStore reviewStore
    let messagesTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            messagesTransform deps knowledgeGraphRuntime reviewStore input output)
    let getToolPolicy = System.Func<string, obj, obj>(fun (_agentId: string) (role: obj) -> buildToolPolicy muxToolNames role)
    let registration = createObj [
        "toolNames", box muxToolNames
        "tools", box tools
        "wrappers", box wrappers
        "mcpServers", box mcpServers
        "contextInjector",
            box {| inject = (fun (projectPath: string) ->
                promise {
                    let! files = findCapsFiles projectPath
                    return if List.isEmpty files then box null else box (VibeFs.Kernel.CapsFormat.buildCapitalsContext files)
                } :> obj) |}
        "eventHook", box eventHook
        "slashCommands", box slashCommands
        "messagesTransform", box messagesTransformFn
        "getToolPolicy", box getToolPolicy
        "__knowledgeGraphRuntime",
            box (createObj
                [ "rawInstance", box knowledgeGraphRuntime
                  "registerJobForTesting",
                  box (System.Func<string, string, string, obj, unit>(fun sessionID workspaceRoot kindTag payload ->
                      knowledgeGraphRuntime.RegisterJobForTesting(sessionID, workspaceRoot, kindTag, payload)))
                  "startMaintenanceIfDue",
                  box (System.Func<string, JS.Promise<unit>>(fun workspaceRoot -> knowledgeGraphRuntime.StartMaintenanceIfDue(workspaceRoot)))
                  "takeBookkeeperLaunchesForTesting",
                  box (System.Func<obj array>(fun () -> knowledgeGraphRuntime.TakeBookkeeperLaunchesForTesting()))
                  "waitForBackgroundJobsForTesting",
                  box (System.Func<JS.Promise<unit>>(fun () -> knowledgeGraphRuntime.WaitForBackgroundJobsForTesting())) ])
        "__reviewStore",
            box (createObj
                [ "activateReview",
                  box (System.Func<string, string, int64, unit>(fun sessionID task createdAt ->
                      reviewStore.activateReview(sessionID, task, createdAt)))
                  "deactivateReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.deactivateReview sessionID))
                  "isReviewActive", box (System.Func<string, bool>(fun sessionID -> reviewStore.isReviewActive sessionID))
                  "getReviewTask", box (System.Func<string, string option>(fun sessionID -> reviewStore.getReviewTask sessionID))
                  "tryLockReview", box (System.Func<string, bool>(fun sessionID -> reviewStore.tryLockReview sessionID))
                  "unlockReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.unlockReview sessionID)) ])
        "__callStore",
            box (createObj
                [ "resolveCall", box (System.Func<string, obj, bool>(fun callId args -> resolveCall callStore callId args))
                  "pendingCallIds", box (System.Func<string array>(fun () -> callStore.PendingCalls.Keys |> Seq.toArray))
                  "hasCall", box (System.Func<string, bool>(fun callId -> hasCall callStore callId))
                  "resolveFirstMatching",
                  box (System.Func<string, obj, bool>(fun prefix args ->
                      callStore.PendingCalls.Keys
                      |> Seq.tryFind (fun k -> k.StartsWith(prefix))
                      |> Option.map (fun k -> resolveCall callStore k args)
                      |> Option.defaultValue false)) ]) ]
    setKey registration "tool.execute.after" (box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
        toolExecuteAfter knowledgeGraphRuntime knowledgeGraphEnabled input output)))
    box registration
