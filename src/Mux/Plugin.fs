module VibeFs.Mux.Plugin

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Config
open VibeFs.Shell.CallStore
open VibeFs.Mux.Delegate
open VibeFs.Mux.Wrappers
open VibeFs.Mux.SubagentTools
open VibeFs.Mux.BuiltinTools
open VibeFs.Mux.EventHook
open VibeFs.Mux.SlashCommands
open VibeFs.Mux.WikiTools
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.MessageDedup
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.WorkspaceFiles

let muxToolNames =
    [| "coder"; "investigator"; "meditator"; "browser"; "executor"
       "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "fuzzy_grep"; "fuzzy_find"; "write"; "read"
       "fetch_wiki"; "return_bookkeeper" |]

let private buildToolPolicy (toolNames: string array) (role: obj) : obj =
    let agent = if Dyn.isNullish role then "manager" else string role
    let remove = toolNames |> Array.filter (fun t -> not (canUse agent t))
    box {| add = [||]; remove = remove |}

let getPluginToolPolicy (agentId: string) (role: obj) : obj =
    buildToolPolicy muxToolNames role

let collectReadOutputs (messages: obj array) : string[] =
    VibeFs.Kernel.MessageDedup.collectReadOutputs messages |> Array.ofList

let private muxReadToolNames = Set [ "read"; "file_read" ]

let private wrapDedupedMuxReadOutput (originalPart: obj) (dedupedPart: obj) : obj =
    let originalOutput = Dyn.get originalPart "output"
    let dedupedOutput = Dyn.get dedupedPart "output"
    let isReadPart =
        Dyn.str dedupedPart "type" = "dynamic-tool"
        && Set.contains (Dyn.str dedupedPart "toolName") muxReadToolNames
        && Dyn.str dedupedPart "state" = "output-available"
    if isReadPart
       && not (Dyn.isNullish originalOutput)
       && not (Dyn.typeIs originalOutput "string")
       && Dyn.typeIs dedupedOutput "string"
       && string dedupedOutput = VibeFs.Kernel.Dedup.dedupMarker then
        Dyn.withKey dedupedPart "output" (box (Dyn.withKey originalOutput "content" (box VibeFs.Kernel.Dedup.dedupMarker)))
    else
        dedupedPart

let private wrapDedupedMuxReadParts (originalMessage: obj) (dedupedMessage: obj) : obj =
    let originalParts = Dyn.get originalMessage "parts"
    let dedupedParts = Dyn.get dedupedMessage "parts"
    if Dyn.isNullish originalParts || Dyn.isNullish dedupedParts || not (Dyn.isArray originalParts) || not (Dyn.isArray dedupedParts) then
        dedupedMessage
    else
        let originalArray = originalParts :?> obj array
        let dedupedArray = dedupedParts :?> obj array
        if originalArray.Length <> dedupedArray.Length then
            dedupedMessage
        else
            let wrappedParts = Array.map2 wrapDedupedMuxReadOutput originalArray dedupedArray
            Dyn.withKey dedupedMessage "parts" (box wrappedParts)

let deduplicateReadOutputsWithSeen (seenOutputs: string[]) (messages: obj array) : obj[] =
    let deduped = VibeFs.Kernel.MessageDedup.deduplicateReadOutputsWithSeen (List.ofArray seenOutputs) messages |> snd
    if messages.Length <> deduped.Length then deduped
    else Array.map2 wrapDedupedMuxReadParts messages deduped

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
    : ToolDefinition array =
    let tools =
        [| coderTool deps toolNames
           investigatorTool deps toolNames
           meditatorTool deps toolNames
           browserTool deps toolNames
           executorTool deps (box wikiRuntime)
           submitReviewTool deps toolNames callStore reviewStore
           returnReviewerTool deps callStore reviewStore
           websearchTool deps
           webfetchTool
           fuzzyGrepTool finderCache
           fuzzyFindTool finderCache
           writeTool deps
           readTool deps hostReadExec
           fetchWikiTool wikiRuntime
           returnBookkeeperTool wikiRuntime |]
    tools

let createRegistration (deps: obj) : obj =
    let callStore = createCallStore ()
    let reviewStore = VibeFs.Shell.ReviewRuntime.createReviewStore ()
    let hostReadExec = HostReadExec()
    let finderCache = FinderCache()
    let wikiRuntime = MuxWikiRuntime(deps)
    let toolNames = muxToolNames
    let tools = createToolCatalog deps toolNames callStore reviewStore hostReadExec finderCache wikiRuntime
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
                   promise {
                       let! files = findCapsFiles projectPath
                       return if List.isEmpty files then box null else box (VibeFs.Kernel.CapsFormat.buildCapitalsContext files)
                   } :> obj) |}
           eventHook = eventHook
           slashCommands = slashCommands
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
