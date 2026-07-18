module Wanxiangshu.Hosts.Omp.NudgeDispatchLogic

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.NudgeDispatchClaimLogic
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Runtime.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Hosts.Omp.HookExecute
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ToolResultEvent
open Wanxiangshu.Hosts.Omp.MagicTodo
open Wanxiangshu.Hosts.Omp.MessagingCodec
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
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Hosts.Omp.NudgeReminderDispatch
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Hosts.Omp.ExecutorTools
open Wanxiangshu.Hosts.Omp.PruneGuard

module Dyn = Wanxiangshu.Runtime.Dyn

let sendNudgeReminder = NudgeReminderDispatch.sendNudgeReminder

let resolveAgentLocal = NudgeReminderDispatch.resolveAgentLocal

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
        let humanTurnId = (fallbackRuntime.GetSession sessionId).HumanTurnId
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
            do!
                claimLeaseAndDispatch
                    pi
                    fallbackRuntime
                    sessionId
                    root
                    action
                    snapshot
                    nudgeId
                    nonce
                    sessionGen
                    cancelGen
                    humanTurnId
                    nudgeOrdinal
    }

let private buildNudgeSnapshot
    (ctx: INudgeHooksContext)
    (sm: ISessionManager)
    (sessionId: string)
    (root: string)
    : JS.Promise<SessionSnapshot> =
    promise {
        let openTodos = openTodoStatuses sm
        let last = lastAssistantMessage sm
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

        return sessionSnapshotFromFold snap runner blockStatus
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
                let root = ctx.cwd |> Option.defaultValue ""
                let! snapshot = buildNudgeSnapshot ctx sm sessionId root

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
