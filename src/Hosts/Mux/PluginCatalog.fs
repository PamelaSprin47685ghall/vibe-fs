module Wanxiangshu.Hosts.Mux.PluginCatalog

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.MuxToolDefinition
open Wanxiangshu.Hosts.Mux.WrappersReview
open Wanxiangshu.Runtime.FuzzyFinderShell
open Wanxiangshu.Runtime.MuxWorkspaceCodec
open Wanxiangshu.Hosts.Mux.SubagentTools
open Wanxiangshu.Hosts.Mux.ReviewToolsMux
open Wanxiangshu.Hosts.Mux.BuiltinTools
open Wanxiangshu.Hosts.Mux.WebTools
open Wanxiangshu.Hosts.Mux.MuxTools
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.MuxHookInputCodec
open Wanxiangshu.Runtime.ChatTransformOutputCodec
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.ToolHookRuntime
open Wanxiangshu.Runtime.MuxPluginCatalogShell
open Wanxiangshu.Runtime.SubagentIntentsCodec

let muxToolNames =
    Array.append
        [| "coder"
           "investigator"
           "browser"
           "continue"
           "executor"
           "submit_review"
           "websearch"
           "webfetch"
           "fuzzy_grep"
           "fuzzy_find"
           "fuzzy_continue"
           "write"
           "read" |]
        meditatorToolNames

let private canUseMuxTopLevel (agent: string) (toolName: string) : bool = canUseForHost mux agent toolName

let buildToolPolicy (toolNames: string array) (role: obj) : obj =
    let agent = if Dyn.isNullish role then "manager" else string role
    let remove = toolNames |> Array.filter (fun t -> not (canUseMuxTopLevel agent t))
    box {| add = [||]; remove = remove |}

let createToolCatalog
    (deps: obj)
    (toolNames: string array)
    (reviewStore: Wanxiangshu.Runtime.ReviewRuntime.ReviewStore)
    (hostReadExec: HostFunctionCapture)
    (finderCache: FinderCache)
    (sessionScope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    : ToolDefinition array =
    let iteratorStore = sessionScope.IteratorStore

    let catalog =
        [| yield injectWarnWarnTddIntoMuxSchema (coderTool deps toolNames sessionScope)
           yield investigatorTool deps toolNames sessionScope
           yield browserTool deps toolNames sessionScope
           yield continueTool deps toolNames sessionScope
           yield injectWarnWarnTddIntoMuxSchema (executorTool deps toolNames sessionScope)
           yield submitReviewTool deps toolNames reviewStore sessionScope
           yield websearchTool deps toolNames
           yield webfetchTool
           yield fuzzyGrepTool finderCache iteratorStore
           yield fuzzyFindTool finderCache iteratorStore
           yield fuzzyContinueTool finderCache iteratorStore
           yield injectWarnWarnTddIntoMuxSchema (writeTool deps)
           yield readTool deps hostReadExec
           yield meditatorTool deps toolNames |]
        |> Array.map injectWarnReuseIntoMuxSchema

    for t in catalog do
        ToolHookRuntime.registerSchemaTypes t.name (box t.parameters)

    catalog

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInputMux input
        let args = argsFromMuxToolExecuteInput input

        if not (Dyn.isNullish args) then
            match ToolHookRuntime.executeBeforeGateway tool args with
            | Result.Error e -> setHookErrorMux output e
            | Result.Ok(nextArgs, env) ->
                setHookArgsMux output nextArgs

                let sessionID = ToolHookRuntime.tryExtractSessionId input |> Option.defaultValue ""

                let toolCallID =
                    ToolHookRuntime.tryExtractToolCallId input |> Option.defaultValue ""

                ToolHookRuntime.saveCompliance sessionID toolCallID env


                let rawOpt: obj option = nextArgs?intents

                let labelResult =
                    match tool with
                    | "coder" ->
                        match rawOpt with
                        | Some r -> joinCoderUiLabel r
                        | None -> Result.Error ""
                    | "investigator" ->
                        match rawOpt with
                        | Some r -> joinInvestigatorUiLabel r
                        | None -> Result.Error ""
                    | _ -> Result.Error ""

                match labelResult with
                | Result.Ok label when label <> "" ->
                    args?("ui_") <- box label
                    nextArgs?("ui_") <- box label
                | _ -> ()
    }

let toolExecuteAfter (scope: RuntimeScope) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxToolExecuteAfterInput input (box null)
        let tool = decoded.Tool
        let sessionID = decoded.SessionID
        let originalOutput = hookOutputTextMux output


        let todoViolations =
            if tool = todoWriteToolName Mux && not (Dyn.isNullish decoded.Args) then
                match Wanxiangshu.Runtime.WorkBacklogToolsCodec.decodeTodoWriteArgs false decoded.Args with
                | Ok(_, viols) -> viols
                | Error _ -> []
            else
                []

        let currentOutput = hookOutputTextMux output
        let isError = hookOutputErrorMux output <> "" || isNetworkErrorText currentOutput

        let toolCallID =
            ToolHookRuntime.tryExtractToolCallId input |> Option.defaultValue ""

        match ToolHookRuntime.tryGetCompliance sessionID toolCallID with
        | Some env ->
            let status =
                if env.Cancelled then
                    ToolHookRuntime.ExecutionStatus.Cancelled
                elif isError then
                    ToolHookRuntime.ExecutionStatus.Failure
                else
                    ToolHookRuntime.ExecutionStatus.Success

            let allViolations = env.Violations @ todoViolations |> List.distinct

            if not allViolations.IsEmpty then
                let criticism = ToolHookRuntime.appendCriticism currentOutput allViolations status
                setHookOutputStringMux output criticism

            // Restore warn fields to decoded args so LLM history sees them.
            if not (Dyn.isNullish decoded.Args) then
                ToolHookRuntime.restoreWarnToArgs decoded.Args env

            ToolHookRuntime.removeCompliance sessionID toolCallID
        | None ->
            let status =
                if isError then
                    ToolHookRuntime.ExecutionStatus.Failure
                else
                    ToolHookRuntime.ExecutionStatus.Success

            if not todoViolations.IsEmpty then
                let criticism = ToolHookRuntime.appendCriticism currentOutput todoViolations status
                setHookOutputStringMux output criticism

        let argsJson = LivelockGuard.cleanArgsJson decoded.Args

        let finalOutput = hookOutputTextMux output

        if isNetworkErrorText finalOutput then
            setHookErrorMux output "network connection lost"

        if LivelockGuard.check scope sessionID tool argsJson currentOutput then
            setHookErrorMux output "livelock guard: repeated identical tool call with identical result"
    }

let systemTransform (directory: string) (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { setSystemOutputToDirectory directory output }
