module VibeFs.Mux.PluginCatalog

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Shell
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Config
open VibeFs.Mux.Wrappers
open VibeFs.Mux.WrappersReview
open VibeFs.Mux.KnowledgeGraphToolDefs
open VibeFs.Shell.FuzzyFinderShell
open VibeFs.Shell.MuxWorkspaceCodec
open VibeFs.Mux.SubagentTools
open VibeFs.Mux.ReviewToolsMux
open VibeFs.Mux.BuiltinTools
open VibeFs.Mux.WebTools
open VibeFs.Mux.KnowledgeGraphRuntimeMux
open VibeFs.Mux.KnowledgeGraphRuntimeMuxSubmit
open VibeFs.Methodology.MuxTools
open VibeFs.Shell.RuntimeScope
open VibeFs.Shell.Dyn
open VibeFs.Shell.MuxHookInputCodec
open VibeFs.Kernel.KnowledgeGraph.BookkeeperPolicy
open VibeFs.Shell.ChatTransformOutputCodec

let muxToolNames =
    Array.append
        [| "coder"; "investigator"; "meditator"; "browser"; "executor"
           "submit_review"; "websearch"; "webfetch"; "fuzzy_grep"; "fuzzy_find"; "write"; "read"
           "knowledge_graph_fetch"; "return_bookkeeper" |]
        methodologyToolNames

let private canUseMuxTopLevel (agent: string) (toolName: string) : bool =
    canUseForHost mux agent toolName

let buildToolPolicy (toolNames: string array) (role: obj) : obj =
    let agent = if Dyn.isNullish role then "manager" else string role
    let remove = toolNames |> Array.filter (fun t -> not (canUseMuxTopLevel agent t))
    box {| add = [||]; remove = remove |}

[<Global("process")>]
let private nodeProcess : obj = jsNative

let envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if isNull v then "" else string v

let setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

let toolsToObject (tools: ToolDefinition array) : obj =
    createObj [ for t in tools -> t.name, box t ]

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
       yield returnBookkeeperTool knowledgeGraphRuntime
       yield! allMethodologyTools deps toolNames |]

let bookkeeperInput (args: obj) : string =
    if Dyn.isNullish args then "" else JS.JSON.stringify args

let toolExecuteAfter
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
           && VibeFs.Kernel.KnowledgeGraph.BookkeeperPolicy.recordsToBookkeeper decoded.Tool
           && not (isReadOnlyExecutorMux decoded.Tool decoded.Args) && not (isChildWorkspace deps decoded.SessionID) then
            knowledgeGraphRuntime.StartBookkeeperAppend(
                bookkeeperInput decoded.Args,
                VibeFs.Kernel.ToolOutputInfo.bodyForBookkeeper originalOutput,
                decoded.Tool,
                config = createObj
                    [ "sessionID", box decoded.SessionID
                      "directory", box decoded.Directory
                      "workspaceId", box decoded.WorkspaceId
                      "taskService", box (Dyn.get deps "taskService") ])
            setHookOutputStringMux output (VibeFs.Kernel.ToolOutputInfo.withBookkeepingHints originalOutput)
    }

let toolExecuteBefore (input: obj) (_output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInputMux input
        let args = Dyn.get input "args"
        if not (Dyn.isNullish args) then
            let raw = Dyn.get args "intents"
            let labelResult =
                match tool with
                | "coder" -> VibeFs.Shell.SubagentIntentsCodec.joinCoderUiLabel raw
                | "investigator" -> VibeFs.Shell.SubagentIntentsCodec.joinInvestigatorUiLabel raw
                | _ -> Result.Error ""
            match labelResult with
            | Result.Ok label when label <> "" -> args?("_ui") <- box label
            | _ -> ()
    }

let systemTransform (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { clearSystemOutputLength output }