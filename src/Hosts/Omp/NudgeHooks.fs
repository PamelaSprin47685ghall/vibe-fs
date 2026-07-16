module Wanxiangshu.Hosts.Omp.NudgeHooks

open Fable.Core
open Fable.Core.JsInterop
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
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.NudgeRuntimeTypes
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Hosts.Omp.NudgeHooksHelpers

let sendNudgeReminder = NudgeHooksHelpers.sendNudgeReminder
let resolveAgentLocal = NudgeHooksHelpers.resolveAgentLocal

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
        | Some sid -> fallbackRuntime.SetMainContinuationAwaitingStart sid false
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
        fallbackRuntime.SetMainContinuationAwaitingStart sid false
        Wanxiangshu.Runtime.ToolHookRuntime.clearSessionCompliance sid
    | None -> ()

    applyActiveToolFilterForMainSession piObj ctxObj

let performNudgeDispatch
    (pi: IPi)
    (fallbackRuntime: FallbackRuntimeStore)
    (root: string)
    (sessionId: string)
    (action: NudgeAction)
    (snapshot: SessionSnapshot)
    (lease: NudgeLease)
    : JS.Promise<unit> =
    promise {
        try
            do! sendNudgeReminder pi action snapshot

            if
                not (
                    fallbackRuntime.TryTransitionPendingNudgeLease(
                        sessionId,
                        lease.NudgeID,
                        LeaseStatus.DispatchStarted,
                        LeaseStatus.Dispatched
                    )
                )
            then
                do!
                    finishNudge
                        fallbackRuntime
                        root
                        sessionId
                        lease
                        NudgeOutcome.Cancelled
                        "Cancelled after dispatch"
                        ""
                        ""
            else
                let dispatchedLease =
                    { lease with
                        Status = LeaseStatus.Dispatched }

                do!
                    finishNudge
                        fallbackRuntime
                        root
                        sessionId
                        dispatchedLease
                        NudgeOutcome.Dispatched
                        ""
                        (Wanxiangshu.Kernel.Nudge.toString action)
                        snapshot.nudgeAnchorKey
        with _ ->
            do! finishNudge fallbackRuntime root sessionId lease NudgeOutcome.Failed "Send failed" "" ""
    }

let dispatchNudgeAction
    (pi: IPi)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionId: string)
    (root: string)
    (action: NudgeAction)
    (snapshot: SessionSnapshot)
    : JS.Promise<unit> =
    promise {
        let nudgeId = "nudge-" + System.Guid.NewGuid().ToString("N")
        let nonce = "nudge_" + System.Guid.NewGuid().ToString("N")
        let sessionGen = fallbackRuntime.GetSessionGeneration sessionId
        let cancelGen = fallbackRuntime.GetCancelGeneration sessionId
        let humanTurnId = fallbackRuntime.GetHumanTurnId sessionId
        let nudgeOrdinal = fallbackRuntime.IncrementNudgeOrdinal sessionId

        let! claimed =
            tryClaimNudgeDispatch
                root
                sessionId
                action
                snapshot.nudgeAnchorKey
                nudgeId
                nonce
                sessionGen
                cancelGen
                humanTurnId
                nudgeOrdinal

        if claimed then
            let lease: NudgeLease =
                { NudgeID = nudgeId
                  NudgeOrdinal = nudgeOrdinal
                  Nonce = nonce
                  HumanTurnID = humanTurnId
                  SessionGeneration = sessionGen
                  CancelGeneration = cancelGen
                  Owner = SessionOwner.Nudge
                  Status = LeaseStatus.DispatchStarted }

            fallbackRuntime.SetPendingNudgeLease(sessionId, lease)
            fallbackRuntime.SetSessionOwner sessionId SessionOwner.Nudge
            fallbackRuntime.SetActiveNudgeNonce sessionId nonce
            fallbackRuntime.SetMainContinuationAwaitingStart sessionId true

            if isSessionForceStopped sessionId then
                do! finishNudge fallbackRuntime root sessionId lease NudgeOutcome.Cancelled "Force stopped" "" ""
            else
                do! performNudgeDispatch pi fallbackRuntime root sessionId action snapshot lease
    }

let handleAgentEndNudge
    (pi: IPi)
    (fallbackRuntime: FallbackRuntimeStore)
    (ctx: INudgeHooksContext)
    (sessionId: string)
    (sm: ISessionManager)
    : JS.Promise<unit> =
    let hasPending =
        match ctx.hasPendingMessages with
        | Some fn -> Dyn.truthy (fn ())
        | None -> false

    if hasPending then
        Promise.lift ()
    else
        promise {
            if not (isSessionForceStopped sessionId) then
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

                let anchor =
                    Wanxiangshu.Kernel.Nudge.NudgeProjection.nudgeAnchorKey snap.turnId snap.lastAssistantText

                let blockStatus =
                    if
                        Wanxiangshu.Kernel.Nudge.NudgeProjection.isBlocked
                            { PendingNudge = snap.pendingNudge
                              LastDispatchedAnchor = snap.lastDispatchedAnchor }
                            anchor
                    then
                        NudgeBlockStatus.Blocked
                    else
                        NudgeBlockStatus.Allowed

                let snapshot = sessionSnapshotFromFold snap runner blockStatus

                match deriveAction snapshot with
                | NudgeNone -> ()
                | action -> do! dispatchNudgeAction pi fallbackRuntime sessionId root action snapshot
        }

let agentEndHandler
    (piObj: obj)
    (_reviewStore: ReviewStore)
    (fallbackRuntime: FallbackRuntimeStore)
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
        elif owner = SessionOwner.Nudge then
            let root = ctx.cwd |> Option.defaultValue ""

            promise {
                match fallbackRuntime.TryGetPendingNudgeLease sessionId with
                | Some lease ->
                    if root <> "" then
                        do! finishNudge fallbackRuntime root sessionId lease NudgeOutcome.Settled "completed" "" ""
                | None -> fallbackRuntime.SetSessionOwner sessionId SessionOwner.NoOwner
            }
        elif owner <> SessionOwner.NoOwner && owner <> SessionOwner.Human then
            Promise.lift ()
        elif isSessionForceStopped sessionId then
            Promise.lift ()
        else
            match ctx.sessionManager with
            | None -> Promise.lift ()
            | Some sm -> handleAgentEndNudge pi fallbackRuntime ctx sessionId sm
