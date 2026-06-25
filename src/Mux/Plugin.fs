module VibeFs.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell
open VibeFs.Kernel.Config
open VibeFs.Kernel.HostTools
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.SubagentTools
open VibeFs.Mux.ReviewToolsMux
open VibeFs.Mux.BuiltinTools
open VibeFs.Mux.WebTools
open VibeFs.Mux.EventHook
open VibeFs.Mux.SlashCommands
open VibeFs.Mux.KnowledgeGraphTools
open VibeFs.Mux.KnowledgeGraphTestHooks
open VibeFs.Mux.KnowledgeGraphToolDefs
open VibeFs.Mux.ReadDedup
open VibeFs.Shell.ReadDedupMuxPlugin
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.WorkspaceFiles
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Mux.MessageTransform
open VibeFs.Mux.BacklogSession
open VibeFs.Shell.RuntimeScope
open VibeFs.Shell.Dyn
open VibeFs.Shell.MuxHookInputCodec

let muxToolNames =
    [| "coder"; "investigator"; "meditator"; "browser"; "executor"
       "submit_review"; "websearch"; "webfetch"; "fuzzy_grep"; "fuzzy_find"; "write"; "read"
       "knowledge_graph_fetch"; "return_bookkeeper" |]

let private canUseMuxTopLevel (agent: string) (toolName: string) : bool =
    canUseForHost mux agent toolName

let private buildToolPolicy (toolNames: string array) (role: obj) : obj =
    let agent = if Dyn.isNullish role then "manager" else string role
    let remove = toolNames |> Array.filter (fun t -> not (canUseMuxTopLevel agent t))
    box {| add = [||]; remove = remove |}

let getPluginToolPolicy (agentId: string) (role: obj) : obj =
    buildToolPolicy muxToolNames role

let collectReadOutputs (messages: obj array) : string[] =
    ReadDedupMuxPlugin.collectReadOutputs messages

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    ReadDedupMuxPlugin.deduplicateReadOutputsWithSeen seenOutputs messages

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
    (reviewStore: VibeFs.Shell.ReviewRuntime.ReviewStore)
    (hostReadExec: HostFunctionCapture)
    (finderCache: FinderCache)
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    (sessionScope: VibeFs.Shell.RuntimeScope.RuntimeScope)
    : ToolDefinition array =
    let iteratorStore = sessionScope.IteratorStore
    [| yield coderTool deps toolNames
       yield investigatorTool deps toolNames
       yield meditatorTool deps toolNames
       yield browserTool deps toolNames
       yield executorTool deps toolNames null sessionScope
       yield submitReviewTool deps toolNames reviewStore
       yield websearchTool deps toolNames
       yield webfetchTool
       yield fuzzyGrepTool finderCache iteratorStore
       yield fuzzyFindTool finderCache iteratorStore
       yield writeTool deps
       yield readTool deps hostReadExec
       yield knowledgeGraphFetchTool knowledgeGraphRuntime
       yield returnBookkeeperTool knowledgeGraphRuntime |]

let private recordsToBookkeeper (tool: string) : bool =
    let allowed =
        [| "coder"; "investigator"; "meditator"; "browser"; "executor"
           "submit_review"; "websearch"; "webfetch"; "write" |]
    Array.contains tool allowed

let private bookkeeperInput (args: obj) : string =
    if Dyn.isNullish args then "" else JS.JSON.stringify args

let private toolExecuteAfter
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    (deps: obj)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxToolExecuteAfterInput input deps
        let succeeded = hookOutputErrorMux output = ""
        let originalOutput = hookOutputTextMux output
        if succeeded
           && recordsToBookkeeper decoded.Tool
           && not (isReadOnlyExecutorMux decoded.Tool decoded.Args) then
            knowledgeGraphRuntime.StartBookkeeperAppend(
                bookkeeperInput decoded.Args,
                originalOutput,
                decoded.Tool,
                config = createObj
                    [ "sessionID", box decoded.SessionID
                      "directory", box decoded.Directory
                      "workspaceId", box decoded.WorkspaceId
                      "taskService", box (Dyn.get deps "taskService") ])
            setHookOutputStringMux output (VibeFs.Kernel.WorkBacklog.withTodoHint originalOutput)
    }

let createRegistration (deps: obj) : obj =
    let scope = create ()
    let backlogSession = BacklogSession(scope)
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostFunctionCapture()
    let finderCache = FinderCache()
    let knowledgeGraphRuntime = MuxKnowledgeGraphRuntime(deps)
    let tools = createToolCatalog deps muxToolNames reviewStore hostReadExec finderCache knowledgeGraphRuntime scope
    let toolsObj = toolsToObject tools
    let mcpServers = box {| ``stealth-browser-mcp`` = VibeFs.Kernel.Config.getStealthBrowserMcpCommand (envVar "STEALTH_BROWSER_MCP_REF") |}
    let wrappers = createAllWrappers toolsObj hostReadExec scope
    let eventHook = createEventHook deps reviewStore
    let slashCommands = createSlashCommands deps muxToolNames reviewStore
    let messagesTransformFn =
        System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
            messagesTransform deps scope backlogSession knowledgeGraphRuntime reviewStore input output)
    let getToolPolicy = System.Func<string, obj, obj>(fun (_agentId: string) (role: obj) -> buildToolPolicy muxToolNames role)
    let registration = createObj [
        "__runtimeScope", box scope
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
            box (
                let hooks = knowledgeGraphRuntime.TestHooks
                createObj
                    [ "rawInstance", box knowledgeGraphRuntime
                      "registerJobForTesting",
                      box (System.Func<string, string, string, obj, unit>(fun sessionID workspaceRoot kindTag payload ->
                          hooks.RegisterJob(sessionID, workspaceRoot, kindTag, payload)))
                      "startMaintenanceIfDue",
                      box (System.Func<string, JS.Promise<unit>>(fun workspaceRoot -> knowledgeGraphRuntime.StartMaintenanceIfDue(workspaceRoot)))
                      "takeBookkeeperLaunchesForTesting",
                      box (System.Func<obj array>(fun () -> hooks.TakeLaunches()))
                      "waitForBackgroundJobsForTesting",
                      box (System.Func<JS.Promise<unit>>(fun () -> hooks.WaitJobs())) ])
        "__reviewStore",
            box (createObj
                [ "activateReview",
                  box (System.Func<string, string, int64, unit>(fun sessionID task createdAt ->
                      reviewStore.activateReview(sessionID, task, createdAt)))
                  "deactivateReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.deactivateReview sessionID))
                  "isReviewActive", box (System.Func<string, bool>(fun sessionID -> reviewStore.isReviewActive sessionID))
                  "getReviewTask", box (System.Func<string, string option>(fun sessionID -> reviewStore.getReviewTask sessionID))
                  "tryLockReview", box (System.Func<string, bool>(fun sessionID -> reviewStore.tryLockReview sessionID))
                  "unlockReview", box (System.Func<string, unit>(fun sessionID -> reviewStore.unlockReview sessionID)) ]) ]
    setKey registration "tool.execute.after" (box (System.Func<obj, obj, JS.Promise<unit>>(fun input output ->
        toolExecuteAfter knowledgeGraphRuntime deps input output)))
    box registration
