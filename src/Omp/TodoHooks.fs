module Wanxiangshu.Omp.TodoHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.HookExecute
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Omp.ToolResultEvent
open Wanxiangshu.Omp.MagicTodo
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.WorkBacklogToolsCodec
open Wanxiangshu.Shell.SubsessionActorRegistry
open Wanxiangshu.Kernel.Subsession.Types

/// Shared BacklogSession bound to the OMP host.
let private backlogSession = BacklogSession omp

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
            // 1. 恢复控制字段
            match envOpt with
            | Some env -> ToolHookRuntime.restoreWarnToArgs args env
            | None -> ()

            // 2. 收集 warn violations & 3. 收集 todo report violations
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

            let violations = warnViolations @ todoReportViolations

            // 4. 执行 syntax、livelock、todo 标准化等业务处理
            let rawContent = getToolResultText event

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

                    let isError =
                        Dyn.truthy (Dyn.get event "isError")
                        || (let err = Dyn.get event "error" in not (Dyn.isNullish err) && string err <> "")

                    if
                        not isError
                        && (ahaMoments <> ""
                            || changesAndReasons <> ""
                            || gotchas <> ""
                            || lessonsAndConventions <> ""
                            || plan <> "")
                        && callId <> ""
                    then
                        let entry: BacklogEntry =
                            { ahaMoments = ahaMoments
                              changesAndReasons = changesAndReasons
                              gotchas = gotchas
                              lessonsAndConventions = lessonsAndConventions
                              plan = plan }

                        backlogSession.CaptureReport(callId, entry.plan)

                    let methodologies =
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

                    if textAfterSyntax <> "" && not isError then
                        businessProcessedText <- todoWriteOutput methodologies

            // 5. 确定最终 Success / Failure / Cancelled 状态
            let isError =
                Dyn.truthy (Dyn.get event "isError")
                || (let err = Dyn.get event "error" in not (Dyn.isNullish err) && string err <> "")

            let status =
                match envOpt with
                | Some env when env.Cancelled -> ToolHookRuntime.ExecutionStatus.Cancelled
                | _ ->
                    if isError || isBusinessLivelock then
                        ToolHookRuntime.ExecutionStatus.Failure
                    else
                        ToolHookRuntime.ExecutionStatus.Success

            // 6. 在最终结果上追加合并后的批评
            let criticism =
                ToolHookRuntime.appendCriticism businessProcessedText violations status

            setToolResultText event criticism
        finally
            // 7. finally 删除 compliance envelope
            if envOpt.IsSome then
                ToolHookRuntime.removeCompliance sessionId toolCallId
    }

let sessionStartHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    promise {
        do! NudgeHooks.applyActiveToolFilterForMainSession pi ctx
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let cwd = Dyn.str ctx "cwd"

        if sessionId <> "" && cwd <> "" then
            do! Wanxiangshu.Shell.EventLogRuntime.syncReviewFromEventLogDedicated reviewStore cwd sessionId

            do!
                Wanxiangshu.Shell.EventLogRuntime.syncBacklogFromEventLogDedicated
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
            do! Wanxiangshu.Shell.EventLogRuntime.syncReviewFromEventLogDedicated reviewStore cwd sessionId

            do!
                Wanxiangshu.Shell.EventLogRuntime.syncBacklogFromEventLogDedicated
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
                SubsessionActorRegistry.ClearPoison sessionId
                SubsessionActorRegistry.Remove sessionId

            do! cleanupRunnerJob ExecutorTools.ompScope sessionId
            Wanxiangshu.Shell.LivelockGuard.cleanup ExecutorTools.ompScope sessionId
            Wanxiangshu.Shell.ToolHookRuntime.clearSessionCompliance sessionId
            Wanxiangshu.Shell.ToolHookRuntime.closeSession sessionId
    }
