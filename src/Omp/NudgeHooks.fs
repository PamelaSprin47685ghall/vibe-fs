module Wanxiangshu.Omp.NudgeHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.NudgeDerivation
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
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Shell.EventLogRuntime

let applyActiveToolFilterForMainSession (pi: obj) (ctx: obj) : JS.Promise<unit> =
    promise {
        let getActive = Dyn.get pi "getActiveTools"
        let active =
            if Dyn.typeIs getActive "function" then
                let rawActive = Dyn.call0 getActive
                unbox<obj array> rawActive
                |> Array.map string
            else
                [||]
        let filtered = filterOmpMainSessionActiveTools active
        if filtered.Length <> active.Length then
            do! pi?setActiveTools(filtered) |> unbox<JS.Promise<unit>>
    }

let beforeAgentStartHandler (pi: obj) (event: obj) (ctx: obj) : JS.Promise<obj> =
    promise {
        let cwd = Dyn.str ctx "cwd"
        let sp = Dyn.get event "systemPrompt"
        let! patch = beforeAgentStart cwd sp
        do! applyActiveToolFilterForMainSession pi ctx
        return patch
    }

let toolCallHandler (_pi: obj) (_reviewStore: ReviewStore) (event: obj) (ctx: obj) : JS.Promise<obj> =
    promise {
        let toolName = Dyn.str event "toolName"
        let args = getToolInput event
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

let turnStartHandler (pi: obj) (_event: obj) (ctx: obj) : JS.Promise<unit> =
    match getSessionIdFromContext ctx with
    | Some sid -> clearNudgeSession sid
    | None -> ()
    applyActiveToolFilterForMainSession pi ctx

let agentEndHandler (pi: obj) (_reviewStore: ReviewStore) (ctx: obj) : JS.Promise<unit> =
    match getSessionIdFromContext ctx with
    | None -> Promise.lift ()
    | Some sessionId ->
        if isSessionForceStopped sessionId then Promise.lift ()
        else
            let sm = Dyn.get ctx "sessionManager"
            let hasPending =
                let fn = Dyn.get ctx "hasPendingMessages"
                Dyn.typeIs fn "function" && Dyn.truthy (Dyn.call0 fn)
            if Dyn.isNullish sm || hasPending then Promise.lift ()
            else
                promise {
                    let openTodos = openTodoStatuses sm
                    let last = lastAssistantMessage sm
                    let root = Dyn.str ctx "cwd"
                    let turnId = lastAssistantTurnId sm
                    do! appendAssistantCompletedOrFail root sessionId last None turnId openTodos
                    let! snap = getNudgeSnapshotFromEventLog root sessionId
                    let hasRunner = hasRunningRunnerJob sessionId
                    let key = nudgeAnchorKey snap.turnId snap.lastAssistantText
                    let dedupState = { BlockedAnchor = snap.nudgeDedupAnchor }
                    let blocked = isNudgeBlockedForAnchor dedupState key
                    let snapshot : Wanxiangshu.Kernel.Nudge.Types.SessionSnapshot = {
                        todos = snap.openTodos
                        lastAssistantMessage = snap.lastAssistantText
                        isLoopActive = snap.isLoopActive
                        nudgeBlockedForTurn = blocked
                        nudgeAnchorKey = key
                        agentFromMessage = snap.agentFromMessage
                        hasActiveRunner = hasRunner
                    }
                    match deriveAction snapshot with
                    | NudgeNone -> ()
                    | action ->
                        let! claimed = tryClaimNudgeDispatch root sessionId action snapshot.nudgeAnchorKey
                        if not claimed then ()
                        else
                            match action with
                            | NudgeRunner ->
                                pi?sendMessage(
                                    createObj [
                                        "customType", box "wanxiangshu-runner-reminder"
                                        "content", box (runnerReminderContent ())
                                        "display", box false
                                    ],
                                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
                            | NudgeLoop ->
                                pi?sendMessage(
                                    createObj [
                                        "customType", box "wanxiangshu-loop-reminder"
                                        "content", box (loopReminderContent snapshot.todos)
                                        "display", box false
                                    ],
                                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
                            | NudgeTodo ->
                                pi?sendMessage(
                                    createObj [
                                        "customType", box "wanxiangshu-todo-reminder"
                                        "content", box (todoReminderContent snapshot.todos)
                                        "display", box false
                                    ],
                                    createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
                            | NudgeNone -> ()
                }
