module Wanxiangshu.Runtime.EventLogRuntimeNudge

open Fable.Core
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Runtime.EventLogRuntimeStore

let isLoopActiveFromEventLog (workspaceRoot: string) (sessionID: string) : JS.Promise<bool> =
    promise {
        if sessionID = "" || workspaceRoot = "" then
            return false
        else
            let! state = getStore(workspaceRoot).GetSessionState(sessionID)
            return state.ReviewTask |> Option.isSome
    }

let nudgeBlockedForTurn (workspaceRoot: string) (sessionID: string) (assistantMessage: string) : JS.Promise<bool> =
    promise {
        if sessionID = "" || workspaceRoot = "" then
            return false
        else
            let! state = getStore(workspaceRoot).GetSessionState(sessionID)
            return Wanxiangshu.Kernel.Nudge.NudgeProjection.isBlocked state.NudgeDedup assistantMessage
    }

let tryClaimNudgeDispatch
    (workspaceRoot: string)
    (sessionID: string)
    (action: NudgeAction)
    (anchor: string)
    (nudgeId: string)
    (nonce: string)
    (sessionGen: int)
    (cancelGen: int)
    (humanTurnId: string)
    (nudgeOrdinal: int)
    : JS.Promise<bool> =
    getStore(workspaceRoot).TryClaimNudgeDispatch
        sessionID
        action
        anchor
        nudgeId
        nonce
        sessionGen
        cancelGen
        humanTurnId
        nudgeOrdinal
        Wanxiangshu.Kernel.Nudge.NudgeProjection.isBlocked

let getNudgeSnapshotFromEventLog (workspaceRoot: string) (sessionID: string) : JS.Promise<NudgeSnapshotState> =
    promise {
        if sessionID = "" || workspaceRoot = "" then
            return emptySnapshotState
        else
            let! state = getStore(workspaceRoot).GetSessionState(sessionID)
            return state.NudgeSnapshot
    }
