module Wanxiangshu.Mux.PluginCatalog

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Mux.WrappersReview
open Wanxiangshu.Mux.KnowledgeGraphToolDefs
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.MuxWorkspaceCodec
open Wanxiangshu.Mux.SubagentTools
open Wanxiangshu.Mux.ReviewToolsMux
open Wanxiangshu.Mux.BuiltinTools
open Wanxiangshu.Mux.WebTools
open Wanxiangshu.Mux.KnowledgeGraphRuntimeMux
open Wanxiangshu.Mux.KnowledgeGraphRuntimeMuxSubmit
open Wanxiangshu.Methodology.MuxTools
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MuxHookInputCodec
open Wanxiangshu.Kernel.KnowledgeGraph.BookkeeperPolicy
open Wanxiangshu.Shell.ChatTransformOutputCodec

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
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (hostReadExec: HostFunctionCapture)
    (finderCache: FinderCache)
    (knowledgeGraphRuntime: MuxKnowledgeGraphRuntime)
    (sessionScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
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
           && Wanxiangshu.Kernel.KnowledgeGraph.BookkeeperPolicy.recordsToBookkeeper decoded.Tool
           && not (isReadOnlyExecutorMux decoded.Tool decoded.Args) && not (isChildWorkspace deps decoded.SessionID) then
            knowledgeGraphRuntime.StartBookkeeperAppend(
                bookkeeperInput decoded.Args,
                Wanxiangshu.Kernel.ToolOutputInfo.bodyForBookkeeper originalOutput,
                decoded.Tool,
                config = createObj
                    [ "sessionID", box decoded.SessionID
                      "directory", box decoded.Directory
                      "workspaceId", box decoded.WorkspaceId
                      "taskService", box (Dyn.get deps "taskService") ])
            setHookOutputStringMux output (Wanxiangshu.Kernel.ToolOutputInfo.withBookkeepingHints originalOutput)
    }

let toolExecuteBefore (input: obj) (_output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInputMux input
        let args = Dyn.get input "args"
        if not (Dyn.isNullish args) then
            let raw = Dyn.get args "intents"
            let labelResult =
                match tool with
                | "coder" -> Wanxiangshu.Shell.SubagentIntentsCodec.joinCoderUiLabel raw
                | "investigator" -> Wanxiangshu.Shell.SubagentIntentsCodec.joinInvestigatorUiLabel raw
                | _ -> Result.Error ""
            match labelResult with
            | Result.Ok label when label <> "" -> args?("_ui") <- box label
            | _ -> ()
    }

let systemTransform (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { clearSystemOutputLength output }