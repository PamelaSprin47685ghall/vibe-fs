module Wanxiangshu.Runtime.EventLogRuntimeSync

open Fable.Core
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.EventStore.EventLogRuntimeStore
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.SessionProjectionStore
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.SessionStateRestore

// ── Aggregate restore ──────────────────────────────────────────────────

let restoreFallbackRuntimeStore
    (scope: RuntimeScope)
    (sid: string)
    (state: Wanxiangshu.Kernel.EventSourcing.Fold.SessionState)
    : unit =
    match scope.TryFindKey("fallbackRuntime") with
    | Some obj ->
        let rt = unbox<FallbackRuntimeStore> obj

        restoreHumanTurnInfo state rt sid

        rt.SetSessionGeneration sid state.SessionGeneration
        rt.SetCancelGeneration sid state.CancelGeneration
        rt.SetActiveContinuationGeneration sid state.ActiveContinuationGen
        rt.SetActiveContinuationCancelGeneration sid state.ActiveContinuationCancelGen

        restoreOrdinals state rt sid

        match state.LastHumanTurnMessageId with
        | Some id -> rt.SetLastHumanMessageId sid id
        | None -> rt.ClearLastHumanMessageId sid

        let fallbackState = rt.GetOrCreateState sid

        let updatedFallbackState =
            { fallbackState with
                Lifecycle =
                    state.FallbackLifecycle
                    |> Option.defaultValue FallbackLifecycle.Active
                Phase =
                    state.FallbackPhase
                    |> Option.defaultValue FallbackPhase.Idle }

        rt.UpdateState sid updatedFallbackState

        match state.SessionOwner with
        | Some o -> rt.SetSessionOwner sid (decodeOwner o)
        | None -> ()

        restorePendingLease state rt sid

        restoreCompaction state rt sid

        restorePendingNudgeLease state rt sid
    | None -> ()

// ── Sync loop entry points ─────────────────────────────────────────────

let syncAllSessionsFromEventLogDedicated
    (host: Host)
    (store: ReviewStore)
    (scope: RuntimeScope)
    (workspaceRoot: string)
    : JS.Promise<unit> =
    promise {
        try
            if workspaceRoot = "" then
                ()
            else
                let! exists = directoryExists workspaceRoot

                if exists then
                    let! allStates = getStore(workspaceRoot).GetAllSessionStates()
                    let sessionIds = allStates |> Map.keys |> Seq.toList

                    for sid in sessionIds do
                        let! state = getStore(workspaceRoot).GetSessionState(sid)
                        syncReviewProjection store sid state.ReviewTask
                        scope.Projection.StoreBacklog(host, sid, List.rev state.Backlog)
                        restoreFallbackRuntimeStore scope sid state
        with _ ->
            ()
    }

let syncBacklogFromEventLogDedicated
    (host: Host)
    (projection: ProjectionStore)
    (workspaceRoot: string)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        try
            if sessionID = "" || workspaceRoot = "" then
                ()
            else
                let! exists = directoryExists workspaceRoot

                if exists then
                    let! state = getStore(workspaceRoot).GetSessionState(sessionID)
                    projection.StoreBacklog(host, sessionID, List.rev state.Backlog)
        with _ ->
            ()
    }
