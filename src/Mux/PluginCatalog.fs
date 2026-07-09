module Wanxiangshu.Mux.PluginCatalog

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Config
open Wanxiangshu.Kernel.ToolResult
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.MuxToolDefinition
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
open Wanxiangshu.Shell.ToolHookRuntime
open Wanxiangshu.Shell.MuxPluginCatalogShell

let muxToolNames =
    Array.append
        [| "coder"
           "investigator"
           "meditator"
           "browser"
           "continue"
           "executor"
           "submit_review"
           "websearch"
           "webfetch"
           "fuzzy_grep"
           "fuzzy_find"
           "write"
           "read" |]
        methodologyToolNames

let private canUseMuxTopLevel (agent: string) (toolName: string) : bool = canUseForHost mux agent toolName

let buildToolPolicy (toolNames: string array) (role: obj) : obj =
    let agent = if Dyn.isNullish role then "manager" else string role
    let remove = toolNames |> Array.filter (fun t -> not (canUseMuxTopLevel agent t))
    box {| add = [||]; remove = remove |}

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
       yield continueTool deps toolNames sessionScope
       yield injectWarnWarnTddIntoMuxSchema (executorTool deps toolNames sessionScope)
       yield submitReviewTool deps toolNames reviewStore sessionScope
       yield websearchTool deps toolNames
       yield webfetchTool
       yield fuzzyGrepTool finderCache iteratorStore
       yield fuzzyFindTool finderCache iteratorStore
       yield injectWarnWarnTddIntoMuxSchema (writeTool deps)
       yield readTool deps hostReadExec
       yield methodologyTool deps toolNames |]
    |> Array.map injectAmendIntoMuxSchema

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    ToolHookRuntime.muxToolExecuteBefore input output

let toolExecuteAfter (scope: RuntimeScope) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let decoded = decodeMuxToolExecuteAfterInput input (box null)
        let tool = decoded.Tool
        let sessionID = decoded.SessionID
        let originalOutput = hookOutputTextMux output

        let amendVal =
            let fromOutput = Dyn.get output "_amend"

            if not (Dyn.isNullish fromOutput) then
                fromOutput
            else
                let fromInput = Dyn.get input "_amend"

                if not (Dyn.isNullish fromInput) then
                    fromInput
                else
                    let fromArgs =
                        if not (Dyn.isNullish decoded.Args) then
                            Dyn.get decoded.Args "_amend"
                        else
                            null

                    if not (Dyn.isNullish fromArgs) then fromArgs else null

        if not (Dyn.isNullish amendVal) then
            restoreAmendToArgs decoded.Args amendVal
            let inputArgs = argsFromMuxToolExecuteInput input
            restoreAmendToArgs inputArgs amendVal
            let outputArgs = argsFromHookOutputMux output
            restoreAmendToArgs outputArgs amendVal

        let argsJson = JS.JSON.stringify decoded.Args

        if isNetworkErrorText originalOutput then
            setHookErrorMux output "network connection lost"

        if LivelockGuard.check scope sessionID tool argsJson originalOutput then
            setHookErrorMux output "livelock guard: repeated identical tool call with identical result"
    }

let systemTransform (directory: string) (_input: obj) (output: obj) : JS.Promise<unit> =
    promise { setSystemOutputToDirectory directory output }
