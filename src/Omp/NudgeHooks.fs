module Wanxiangshu.Omp.NudgeHooks

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
open Wanxiangshu.Omp.OmpTestHooks
open Wanxiangshu.Omp.ToolResultEvent
open Wanxiangshu.Omp.MagicTodo
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.ReviewRuntime

/// Shared helper: filter the active-tool list for the main (non-child) OMP
/// session. Used by both `session_start` and `before_agent_start` handlers so
/// the filtering logic lives in one place.
let applyActiveToolFilterForMainSession (pi: obj) (ctx: obj) : JS.Promise<unit> =
    promise {
        let getActive = Dyn.get pi "getActiveTools"
        let active =
            if Dyn.typeIs getActive "function" then
                let rawActive = Dyn.call0 getActive
                unbox<obj array> rawActive
                |> Microsoft.FSharp.Collections.Array.map string
            else
                [||]
        let filtered = filterOmpMainSessionActiveTools active
        if filtered.Length <> active.Length then
            do! pi?setActiveTools(filtered) |> unbox<JS.Promise<unit>>
    }

/// before_agent_start handler: runs the existing cwd + system-prompt patch,
/// then applies the same active-tool filtering as session_start so that any
/// tool-list mutation between session start and a new agent turn is caught.
let beforeAgentStartHandler (pi: obj) (event: obj) (ctx: obj) : JS.Promise<obj> =
    promise {
        let cwd = Dyn.str ctx "cwd"
        let sp = Dyn.get event "systemPrompt"
        let! patch = beforeAgentStart cwd sp
        do! applyActiveToolFilterForMainSession pi ctx
        return patch
    }

/// tool_call handler: pre-execute hook on Pi. Normalises the tool arguments
/// (patch unification + `_ui` label injection) before the tool runs.
/// Also blocks child-session-only tools when invoked from the main session.
let toolCallHandler (_pi: obj) (_reviewStore: ReviewStore) (event: obj) (ctx: obj) : JS.Promise<obj> =
    promise {
        let toolName = Dyn.str event "toolName"
        let args = getToolInput event
        match getSessionIdFromContext ctx with
        | Some sessionId when isChildSession sessionId ->
            return None  // skip applyToolCallHook — child sessions bypass warn/warn_tdd validation
        | _ ->
            match applyToolCallHook toolName args with
            | Some reason ->
                return createObj [
                    "block", box true
                    "reason", box reason
                ]
            | None ->
                match getSessionIdFromContext ctx with
                | Some sessionId when not (isChildSession sessionId) && isChildOnlyTool toolName ->
                    return createObj [
                        "block", box true
                        "reason", box (sprintf "Tool '%s' is child-session-only; delegate via a subagent." toolName)
                    ]
                | _ -> return None
    }

/// turn_start handler: re-applies active-tool filtering at the start of each
/// turn so that tool visibility changes mid-session are caught immediately.
let turnStartHandler (pi: obj) (_event: obj) (ctx: obj) : JS.Promise<unit> =
    applyActiveToolFilterForMainSession pi ctx

let agentEndHandler (pi: obj) (reviewStore: ReviewStore) (ctx: obj) : unit =
    match getSessionIdFromContext ctx with
    | None -> ()
    | Some sessionId ->
        let sm = Dyn.get ctx "sessionManager"
        let hasPending =
            let fn = Dyn.get ctx "hasPendingMessages"
            Dyn.typeIs fn "function" && Dyn.truthy (Dyn.call0 fn)
        if reviewStore.isReviewActive sessionId && not (Dyn.isNullish sm) && not hasPending then
            let last = lastAssistantMessage sm
            match tryLoopNudge sessionId last with
            | Some NudgeRunner ->
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-runner-reminder"
                        "content", box (runnerReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | Some NudgeLoop ->
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-loop-reminder"
                        "content", box (loopReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | _ -> ()
        elif not (Dyn.isNullish sm) && not hasPending then
            let last = lastAssistantMessage sm
            match tryTodoNudge sessionId sm last with
            | Some NudgeRunner ->
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-runner-reminder"
                        "content", box (runnerReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | Some NudgeTodo ->
                pi?sendMessage(
                    createObj [
                        "customType", box "wanxiangshu-todo-reminder"
                        "content", box (todoReminderContent ())
                        "display", box false
                    ],
                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | _ -> ()
