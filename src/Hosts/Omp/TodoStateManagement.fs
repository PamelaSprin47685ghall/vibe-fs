module Wanxiangshu.Hosts.Omp.TodoStateManagement

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.HookExecute
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ToolResultEvent
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Kernel.Subsession.Types

let collectViolations (_envOpt: ToolHookRuntime.ControlEnvelope option) (_toolName: string) (_args: obj) : string list =
    []

let isToolError (event: obj) : bool =
    Dyn.truthy (Dyn.get event "isError")
    || (let err = Dyn.get event "error" in not (Dyn.isNullish err) && string err <> "")

let getTodoWriteMethodologies (args: obj) : string list =
    let raw =
        if Dyn.isNullish args then
            null
        else
            Dyn.get args "select_methodology"

    if Dyn.isNullish raw || not (Dyn.isArray raw) then
        []
    else
        let rawArr = unbox<obj array> raw
        rawArr |> Seq.map string |> List.ofSeq

let determineExecutionStatus
    (envOpt: ToolHookRuntime.ControlEnvelope option)
    (isError: bool)
    (isBusinessLivelock: bool)
    : ToolHookRuntime.ExecutionStatus =
    match envOpt with
    | Some env when env.Cancelled -> ToolHookRuntime.ExecutionStatus.Cancelled
    | _ ->
        if isError || isBusinessLivelock then
            ToolHookRuntime.ExecutionStatus.Failure
        else
            ToolHookRuntime.ExecutionStatus.Success

let cleanupCompliance
    (envOpt: ToolHookRuntime.ControlEnvelope option)
    (sessionId: string)
    (toolCallId: string)
    (args: obj)
    : unit =
    match envOpt with
    | Some env ->
        if not (Dyn.isNullish args) then
            ToolHookRuntime.restoreWarnToArgs args env

        ToolHookRuntime.removeCompliance sessionId toolCallId
    | None -> ()

let finalizeToolResult
    (businessProcessedText: string)
    (violations: string list)
    (status: ToolHookRuntime.ExecutionStatus)
    (event: obj)
    : unit =
    let criticism =
        ToolHookRuntime.appendCriticism businessProcessedText violations status

    setToolResultText event criticism

let toolResultHandler (_pi: obj) (_reviewStore: ReviewStore) (event: obj) (ctx: obj) : JS.Promise<unit> =
    promise {
        let toolName = Dyn.str event "toolName"
        let args = getToolInput event
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let toolCallId = getToolCallId event

        let envOpt = ToolHookRuntime.tryGetCompliance sessionId toolCallId

        try
            let violations = collectViolations envOpt toolName args
            let rawContent = getToolResultText event
            let isError = isToolError event

            let isLivelock =
                sessionId <> ""
                && check ExecutorTools.ompScope sessionId toolName (cleanArgsJson args) rawContent

            let mutable businessProcessedText = rawContent
            let mutable isBusinessLivelock = false

            if isLivelock then
                businessProcessedText <- "livelock guard: repeated identical tool call with identical result"
                isBusinessLivelock <- true
            else
                applyToolResultHook toolName args
                do! appendToolResultSyntax (Dyn.str ctx "cwd") event

                let mutable textAfterSyntax = getToolResultText event
                businessProcessedText <- textAfterSyntax

                if toolName = todoWriteToolName omp then
                    let methodologies = getTodoWriteMethodologies args

                    if textAfterSyntax <> "" && not isError then
                        businessProcessedText <- todoWriteOutput methodologies

            let status = determineExecutionStatus envOpt isError isBusinessLivelock
            finalizeToolResult businessProcessedText violations status event
        finally
            cleanupCompliance envOpt sessionId toolCallId args
    }
