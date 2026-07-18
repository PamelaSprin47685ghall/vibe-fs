module Wanxiangshu.Hosts.Omp.NudgeDispatchLogic.SnapshotHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge.NudgeSnapshotSource
open Wanxiangshu.Kernel.Nudge.NudgeDerivation
open Wanxiangshu.Kernel.TreeSitterKernel
open Wanxiangshu.Hosts.Omp.ChildSession
open Wanxiangshu.Kernel.OmpSessionTools
open Wanxiangshu.Runtime.RunnerBackground
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Hosts.Omp.HookExecute
open Wanxiangshu.Hosts.Omp.MessageTransform
open Wanxiangshu.Hosts.Omp.ToolResultEvent
open Wanxiangshu.Hosts.Omp.MagicTodo
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Runtime.ToolOutputInfo

let buildNudgeSnapshot
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
