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
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.BacklogProjectionBuild

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Kernel.Subsession.Types

/// Shared BacklogSession bound to the OMP host.
let private backlogSession = BacklogSession(omp, ExecutorTools.ompScope)

let collectViolations (envOpt: ToolHookRuntime.ControlEnvelope option) (toolName: string) (args: obj) : string list =
    let warnViolations =
        match envOpt with
        | Some env -> env.Violations
        | None -> []

    let todoReportViolations =
        if toolName = todoWriteToolName omp then
            match decodeTodoWriteArgs false args with
            | Result.Ok(_, viols) -> viols
            | Result.Error _ -> []
        else
            []

    warnViolations @ todoReportViolations

let tryCaptureBacklogEntry (isError: bool) (callId: string) (input: obj) (event: obj) : unit =
    if callId = "" then
        ()

    let ahaMoments =
        if Dyn.isNullish input then
            ""
        else
            (Dyn.str input "ahaMoments").Trim()

    let changesAndReasons =
        if Dyn.isNullish input then
            ""
        else
            (Dyn.str input "changesAndReasons").Trim()

    let gotchas =
        if Dyn.isNullish input then
            ""
        else
            (Dyn.str input "gotchas").Trim()

    let lessonsAndConventions =
        if Dyn.isNullish input then
            ""
        else
            (Dyn.str input "lessonsAndConventions").Trim()

    let plan =
        if Dyn.isNullish input then
            ""
        else
            (Dyn.str input "plan").Trim()

    if
        not isError
        && (ahaMoments <> ""
            || changesAndReasons <> ""
            || gotchas <> ""
            || lessonsAndConventions <> ""
            || plan <> "")
    then
        let entry: BacklogEntry =
            { ahaMoments = ahaMoments
              changesAndReasons = changesAndReasons
              gotchas = gotchas
              lessonsAndConventions = lessonsAndConventions
              plan = plan }

        backlogSession.CaptureReport(callId, entry.plan)

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
                    let callId = getToolCallId event
                    let input = getToolInput event

                    tryCaptureBacklogEntry isError callId input event

                    let methodologies = getTodoWriteMethodologies args

                    if textAfterSyntax <> "" && not isError then
                        businessProcessedText <- todoWriteOutput methodologies

            let status = determineExecutionStatus envOpt isError isBusinessLivelock
            finalizeToolResult businessProcessedText violations status event
        finally
            cleanupCompliance envOpt sessionId toolCallId args
    }
