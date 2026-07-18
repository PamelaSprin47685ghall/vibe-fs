module Wanxiangshu.Hosts.Omp.NudgeHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.NudgeDispatchLogic
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.HookExecute
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ToolResultEvent
open Wanxiangshu.Hosts.Omp.MagicTodo
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Hosts.Omp.NudgeReminderDispatch

module Dyn = Wanxiangshu.Runtime.Dyn

let beforeAgentStartHandler
    (piObj: obj)
    (event: obj)
    (ctxObj: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    : JS.Promise<obj> =
    promise {
        let cwd = Dyn.str ctxObj "cwd"
        let sp = Dyn.get event "systemPrompt"

        match getSessionIdFromContext ctxObj with
        | Some sid -> fallbackRuntime.Update(sid, setMainContinuationAwaitingStart false)
        | None -> ()

        let! patch = beforeAgentStart cwd sp
        do! applyActiveToolFilterForMainSession piObj ctxObj
        return patch
    }

let toolCallHandler (_pi: obj) (_reviewStore: ReviewStore) (event: obj) (ctx: obj) : JS.Promise<obj> =
    promise {
        let toolName = Dyn.str event "toolName"
        let args = getToolInput event
        let sessionId = getSessionIdFromContext ctx |> Option.defaultValue ""
        let toolCallId = ToolResultEvent.getToolCallId event

        match Wanxiangshu.Hosts.Omp.HookExecute.applyToolCallHookWithIds toolName args sessionId toolCallId with
        | Some reason -> return createObj [ "block", box true; "reason", box reason ]
        | None ->
            match getSessionIdFromContext ctx with
            | Some sessionId when not (isChildSession ompScope sessionId) && isChildOnlyTool toolName ->
                return
                    createObj
                        [ "block", box true
                          "reason", box (sprintf "Tool '%s' is child-session-only; delegate via a subagent." toolName) ]
            | _ -> return None
    }

let turnStartHandler
    (piObj: obj)
    (event: obj)
    (ctxObj: obj)
    (fallbackRuntime: FallbackRuntimeStore)
    : JS.Promise<unit> =
    match getSessionIdFromContext ctxObj with
    | Some sid ->
        clearNudgeSession sid
        fallbackRuntime.Update(sid, setMainContinuationAwaitingStart false)
        Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sid
    | None -> ()

    applyActiveToolFilterForMainSession piObj ctxObj
