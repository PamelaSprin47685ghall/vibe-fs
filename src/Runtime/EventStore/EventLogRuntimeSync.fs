module Wanxiangshu.Runtime.EventLogRuntimeSync

open Fable.Core
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.FallbackKernel.Types
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
        rt.Update(sid, SessionStateRestore.restoreFromEventLogState state)
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
                        try
                            let! state = getStore(workspaceRoot).GetSessionState(sid)
                            syncReviewProjection store sid state.ReviewTask
                            scope.Projection.StoreBacklog(host, sid, List.rev state.Backlog)
                            restoreFallbackRuntimeStore scope sid state
                        with ex ->
                            JS.console.error (
                                box
                                    {| event = "session_restore_failed"
                                       directory = workspaceRoot
                                       session = sid
                                       message = ex.Message |}
                            )

                            match scope.TryFindKey("fallbackRuntime") with
                            | Some obj ->
                                let rt = unbox<FallbackRuntimeStore> obj

                                rt.Update(
                                    sid,
                                    fun s ->
                                        { s with
                                            Core =
                                                { s.Core with
                                                    Lifecycle = FallbackLifecycle.RecoveryRequired } }
                                )
                            | None -> ()
        with ex ->
            JS.console.error (
                box
                    {| event = "session_restore_failed"
                       directory = workspaceRoot
                       message = ex.Message |}
            )

            match scope.TryFindKey("fallbackRuntime") with
            | Some obj ->
                let rt = unbox<FallbackRuntimeStore> obj

                for sid in rt.GetAllSessionIds() do
                    rt.Update(
                        sid,
                        fun s ->
                            { s with
                                Core =
                                    { s.Core with
                                        Lifecycle = FallbackLifecycle.RecoveryRequired } }
                    )
            | None -> ()
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
        with ex ->
            JS.console.error (
                box
                    {| event = "session_restore_failed"
                       directory = workspaceRoot
                       session = sessionID
                       message = ex.Message |}
            )
    }
