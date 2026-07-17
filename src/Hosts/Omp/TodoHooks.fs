module Wanxiangshu.Hosts.Omp.TodoHooks

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
open Wanxiangshu.Hosts.Omp.MagicTodo
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

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Kernel.Subsession.Types

/// Shared BacklogSession bound to the OMP host.
let private backlogSession = BacklogSession omp

/// Collect warn violations from the compliance envelope plus todo-report
/// violations decoded from args.  Control fields were removed in the
/// pre-execute gateway and must stay absent from the post-execute args
/// object (they will be restored in the finally block for LLM history
/// visibility).
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

/// Attempt to capture a backlog entry from a todo_write tool call when the
/// call carries meaningful structured fields and no error.
let tryCaptureBacklogEntry (isError: bool) (callId: string) (input: obj) (event: obj) : unit =
    if callId = "" then
        () // caller already checked; keep total lines low

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

/// Check whether the event carries an error: either the top-level isError
/// truthy flag or a non-empty error string.
let isToolError (event: obj) : bool =
    Dyn.truthy (Dyn.get event "isError")
    || (let err = Dyn.get event "error" in not (Dyn.isNullish err) && string err <> "")

/// Extract the select_methodology array from todo_write args, returning an
/// empty list when absent or not an array.
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

/// Determine the final ToolHookRuntime.ExecutionStatus from the compliance
/// envelope, error state, and livelock flag.
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

/// Restore warn fields to args so LLM history sees them, then delete
/// the compliance envelope.
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

/// Build the final (status, criticism) pair from the processed text,
/// violations list, and execution outcome, then write it back to the event.
let finalizeToolResult
    (businessProcessedText: string)
    (violations: string list)
    (status: ToolHookRuntime.ExecutionStatus)
    (event: obj)
    : unit =
    let criticism =
        ToolHookRuntime.appendCriticism businessProcessedText violations status

    setToolResultText event criticism

/// Tools whose every user-facing invocation is durable enough to feed the
/// backlog as an input/output black box. Direct write
/// tools join the set via `isFileEditTool`; subagent and IO tools are listed
/// explicitly. Pure lookups (fuzzy_find/fuzzy_grep), the review tools themselves,
/// and host read tools never record.
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

let sessionStartHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        do! applyActiveToolFilterForMainSession pi ctx
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let cwd = Dyn.str ctx "cwd"

        if sessionId <> "" && cwd <> "" then
            do! Wanxiangshu.Runtime.EventLogRuntime.syncReviewFromEventLogDedicated reviewStore cwd sessionId

            do!
                Wanxiangshu.Runtime.EventLogRuntime.syncBacklogFromEventLogDedicated
                    omp
                    backlogSession.Projection
                    cwd
                    sessionId
    }

/// session_prompt: lightweight re-sync before each prompt to catch cross-session durable state changes.
let sessionPromptHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let cwd = Dyn.str ctx "cwd"

        if sessionId <> "" && cwd <> "" then
            do! Wanxiangshu.Runtime.EventLogRuntime.syncReviewFromEventLogDedicated reviewStore cwd sessionId

            do!
                Wanxiangshu.Runtime.EventLogRuntime.syncBacklogFromEventLogDedicated
                    omp
                    backlogSession.Projection
                    cwd
                    sessionId
    }

let sessionShutdownHandler (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        match getSessionIdFromContext ctx with
        | None -> ()
        | Some sessionId ->
            clearNudgeSession sessionId
            clearTypedIteratorScope ompScope.IteratorStore sessionId
            let cwd = Dyn.str ctx "cwd"

            if cwd <> "" then
                do! appendLoopCancelledOrFail cwd sessionId
                do! syncReviewFromEventLogDedicated reviewStore cwd sessionId
                let sid = SessionId.create sessionId
                let eventStore = SubsessionEventStore.create cwd
                do! eventStore.Append(sid, [ PhysicalSessionClosed sid ])
                SubsessionActorRegistry.ClearPoison cwd sessionId
                SubsessionActorRegistry.Remove cwd sessionId

            do! cleanupRunnerJob ExecutorTools.ompScope sessionId
            Wanxiangshu.Runtime.LivelockGuard.cleanup ExecutorTools.ompScope sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sessionId
            Wanxiangshu.Runtime.ToolHookRuntime.closeSession sessionId
            ExecutorTools.ompScope.RemoveSessionQueue sessionId
            ExecutorTools.ompScope.RemoveTempFiles sessionId
    }
