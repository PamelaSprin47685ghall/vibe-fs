module Wanxiangshu.Omp.NudgeHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Kernel.PromptFragments
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.ExecutorTools
open Wanxiangshu.Omp.HookExecute
open Wanxiangshu.Omp.MessageTransform
open Wanxiangshu.Omp.ToolResultEvent
open Wanxiangshu.Omp.MagicTodo
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.NudgeRuntime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Shell.RunnerBackground
open Wanxiangshu.Shell.LivelockGuard
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.FallbackRuntimeState

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Shell.FuzzyIteratorStore
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Kernel.EventLog.Fold
open Wanxiangshu.Shell.EventLogRuntime

let applyActiveToolFilterForMainSession (piObj: obj) (ctxObj: obj) : JS.Promise<unit> =
    promise {
        let active =
            if Dyn.typeIs (Dyn.get piObj "getActiveTools") "function" then
                unbox<obj array> (piObj?getActiveTools ()) |> Array.map string
            else
                [||]

        let filtered = filterOmpMainSessionActiveTools active

        if filtered.Length <> active.Length then
            if Dyn.typeIs (Dyn.get piObj "setActiveTools") "function" then
                do! piObj?setActiveTools (filtered)
    }

let beforeAgentStartHandler
    (piObj: obj)
    (event: obj)
    (ctxObj: obj)
    (fallbackRuntime: FallbackRuntimeState)
    : JS.Promise<obj> =
    promise {
        let cwd = Dyn.str ctxObj "cwd"
        let sp = Dyn.get event "systemPrompt"

        match getSessionIdFromContext ctxObj with
        | Some sid -> fallbackRuntime.SetAwaitingBusy sid false
        | None -> ()

        let! patch = beforeAgentStart cwd sp
        do! applyActiveToolFilterForMainSession piObj ctxObj
        return patch
    }

let toolCallHandler (_pi: obj) (_reviewStore: ReviewStore) (event: obj) (ctx: obj) : JS.Promise<obj> =
    promise {
        let toolName = Dyn.str event "toolName"
        let args = getToolInput event

        match applyToolCallHook toolName args with
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
    (fallbackRuntime: FallbackRuntimeState)
    : JS.Promise<unit> =
    match getSessionIdFromContext ctxObj with
    | Some sid ->
        clearNudgeSession sid
        fallbackRuntime.SetAwaitingBusy sid false
    | None -> ()

    applyActiveToolFilterForMainSession piObj ctxObj

let private sendNudgeReminder (pi: IPi) (action: NudgeAction) (snapshot: SessionSnapshot) : JS.Promise<unit> =
    promise {
        if pi.sendMessage.IsSome then
            let call (msg: obj) (opts: obj) =
                let r = pi?sendMessage (msg, opts)
                if Dyn.isNullish r then Promise.lift () else unbox r

            match action with
            | NudgeRunner ->
                do!
                    call
                        (createObj
                            [ "customType", box "wanxiangshu-runner-reminder"
                              "content", box (runnerReminderContent ())
                              "display", box false ])
                        (createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | NudgeLoop ->
                do!
                    call
                        (createObj
                            [ "customType", box "wanxiangshu-loop-reminder"
                              "content", box (loopReminderContent snapshot.todos)
                              "display", box false ])
                        (createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | NudgeTodo ->
                do!
                    call
                        (createObj
                            [ "customType", box "wanxiangshu-todo-reminder"
                              "content", box (todoReminderContent snapshot.todos)
                              "display", box false ])
                        (createObj [ "triggerTurn", box true; "deliverAs", box "nextTurn" ])
            | NudgeNone -> ()
    }

let private resolveAgentLocal (ctx: obj) : string =
    let sm = Dyn.get ctx "sessionManager"

    if Dyn.isNullish sm then
        "manager"
    else
        let name = Dyn.str sm "agentName"
        if name <> "" then name else "manager"

let agentEndHandler
    (piObj: obj)
    (_reviewStore: ReviewStore)
    (fallbackRuntime: FallbackRuntimeState)
    (ctxObj: obj)
    : JS.Promise<unit> =
    let pi = unbox<IPi> piObj
    let ctx = unbox<INudgeHooksContext> ctxObj

    match getSessionIdFromContext ctxObj with
    | None -> Promise.lift ()
    | Some sessionId ->
        let owner = fallbackRuntime.GetSessionOwner sessionId
        let currentAgent = resolveAgentLocal ctxObj

        if isSyntheticAssistantAgent currentAgent then
            Promise.lift ()
        elif owner <> "None" && owner <> "Human" then
            Promise.lift ()
        elif isSessionForceStopped sessionId then
            Promise.lift ()
        else
            match ctx.sessionManager with
            | None -> Promise.lift ()
            | Some sm ->
                let hasPending =
                    match ctx.hasPendingMessages with
                    | Some fn -> Dyn.truthy (fn ())
                    | None -> false

                if hasPending then
                    Promise.lift ()
                else
                    promise {
                        if isSessionForceStopped sessionId then
                            return ()
                        else
                            let openTodos = openTodoStatuses sm
                            let last = lastAssistantMessage sm
                            let root = ctx.cwd |> Option.defaultValue ""
                            let turnId = lastAssistantTurnId sm
                            let model = lastAssistantModel sm
                            do! appendAssistantCompletedOrFail root sessionId last None model turnId openTodos
                            let! snap = getNudgeSnapshotFromEventLog root sessionId

                            let runner =
                                if hasRunningRunnerJob ompScope sessionId then
                                    RunnerPresence.Active
                                else
                                    RunnerPresence.Absent

                            let anchor = nudgeAnchorKey snap.turnId snap.lastAssistantText

                            let blockStatus =
                                if isNudgeBlockedForAnchor { DispatchedAnchors = snap.dispatchedAnchors } anchor then
                                    NudgeBlockStatus.Blocked
                                else
                                    NudgeBlockStatus.Allowed

                            let snapshot = sessionSnapshotFromFold snap runner blockStatus

                            match deriveAction snapshot with
                            | NudgeNone -> ()
                            | action ->
                                let! claimed = tryClaimNudgeDispatch root sessionId action snapshot.nudgeAnchorKey

                                if not claimed then
                                    ()
                                elif isSessionForceStopped sessionId then
                                    ()
                                else
                                    fallbackRuntime.SetAwaitingBusy sessionId true
                                    do! sendNudgeReminder pi action snapshot
                    }
