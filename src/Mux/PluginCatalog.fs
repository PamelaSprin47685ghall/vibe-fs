module Wanxiangshu.Mux.PluginCatalog

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Mux.Wrappers
open Wanxiangshu.Mux.WrappersReview
open Wanxiangshu.Shell.FuzzyFinderShell
open Wanxiangshu.Shell.MuxWorkspaceCodec
open Wanxiangshu.Mux.SubagentTools
open Wanxiangshu.Mux.ReviewToolsMux
open Wanxiangshu.Mux.BuiltinTools
open Wanxiangshu.Mux.WebTools
open Wanxiangshu.Methodology.MuxTools
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.MuxHookInputCodec
open Wanxiangshu.Shell.ChatTransformOutputCodec
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.ToolExecute

let muxToolNames =
    Array.append
        [| "coder"; "investigator"; "meditator"; "browser"; "executor"
           "submit_review"; "websearch"; "webfetch"; "fuzzy_grep"; "fuzzy_find"; "write"; "read" |]
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

let private addRequired (schema: obj) (key: string) : unit =
    let existing = Dyn.get schema "required"
    if Dyn.isArray existing then
        existing?("push")(box key) |> ignore
    else
        schema?("required") <- box [| box key |]

let private injectWarnTddIntoMuxSchema (tool: ToolDefinition) : ToolDefinition =
    if Wanxiangshu.Kernel.WarnTdd.isModificationTool tool.name then
        let props = Dyn.get tool.parameters "properties"
        if isNullish (Dyn.get props "warn_tdd") then
            props?("warn_tdd") <- box (createObj [| "type", box "string"; "enum", box [| box Wanxiangshu.Kernel.WarnTdd.canonicalValue |]; "description", box Wanxiangshu.Kernel.WarnTdd.warnTddDescription |])
        addRequired tool.parameters "warn_tdd"
    tool

let private injectWarnIntoMuxSchema (tool: ToolDefinition) : ToolDefinition =
    if Wanxiangshu.Kernel.WarnTdd.isWarnRequiredTool tool.name then
        let props = Dyn.get tool.parameters "properties"
        if isNullish (Dyn.get props "warn") then
            props?("warn") <- box (createObj [| "type", box "string"; "enum", box [| box Wanxiangshu.Kernel.WarnTdd.warnCanonicalValue |]; "description", box Wanxiangshu.Kernel.WarnTdd.warnDescription |])
        addRequired tool.parameters "warn"
    tool

let private injectWarnWarnTddIntoMuxSchema (tool: ToolDefinition) : ToolDefinition =
    injectWarnTddIntoMuxSchema (injectWarnIntoMuxSchema tool)

let createToolCatalog
    (deps: obj)
    (toolNames: string array)
    (reviewStore: Wanxiangshu.Shell.ReviewRuntime.ReviewStore)
    (hostReadExec: HostFunctionCapture)
    (finderCache: FinderCache)
    (sessionScope: Wanxiangshu.Shell.RuntimeScope.RuntimeScope)
    : ToolDefinition array =
     let iteratorStore = sessionScope.IteratorStore
     [| yield injectWarnWarnTddIntoMuxSchema (coderTool deps toolNames sessionScope)
        yield investigatorTool deps toolNames sessionScope
        yield meditatorTool deps toolNames sessionScope
        yield browserTool deps toolNames sessionScope
        yield injectWarnWarnTddIntoMuxSchema (executorTool deps toolNames sessionScope)
        yield submitReviewTool deps toolNames reviewStore
        yield websearchTool deps toolNames
        yield webfetchTool
        yield fuzzyGrepTool finderCache iteratorStore
        yield fuzzyFindTool finderCache iteratorStore
        yield injectWarnWarnTddIntoMuxSchema (writeTool deps)
        yield readTool deps hostReadExec
        yield methodologyTool deps toolNames |]

let private requireWarnTddMux (tool: string) (args: obj) (output: obj) : unit =
    if not (Wanxiangshu.Kernel.WarnTdd.isModificationTool tool) then ()
    else
        let raw = Dyn.str args "warn_tdd"
        match Wanxiangshu.Kernel.WarnTdd.parseWarnTdd raw with
        | Some _ -> Dyn.deleteKey args "warn_tdd"
        | None -> setHookErrorMux output (wireDomainFailure tool (InvalidIntent(tool, "warn_tdd", "required — acknowledge TDD + Kolmolgorov discipline")))

let private requireWarnMux (tool: string) (args: obj) (output: obj) : unit =
    if not (Wanxiangshu.Kernel.WarnTdd.isWarnRequiredTool tool) then ()
    else
        let raw = Dyn.str args "warn"
        if Wanxiangshu.Kernel.WarnTdd.parseWarn raw then
            Dyn.deleteKey args "warn"
        else
            setHookErrorMux output (wireDomainFailure tool (InvalidIntent(tool, "warn", "required — acknowledge this task cannot be done with other tools")))

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInputMux input
        let args = Dyn.get input "args"
        if not (Dyn.isNullish args) then
            requireWarnTddMux tool args output
            requireWarnMux tool args output
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

let toolExecuteAfter (scope: RuntimeScope) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInputMux input
        let sessionID = Dyn.str input "sessionID"
        let originalOutput = Dyn.str output "output"
        if isNetworkErrorText originalOutput then
            setHookErrorMux output "network connection lost"
        if LivelockGuard.check scope sessionID tool
            (JS.JSON.stringify (argsFromMuxToolExecuteInput input))
            originalOutput then
            setHookErrorMux output "livelock guard: repeated identical tool call with identical result"
    }

let systemTransform (directory: string) (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { setSystemOutputToDirectory directory output }