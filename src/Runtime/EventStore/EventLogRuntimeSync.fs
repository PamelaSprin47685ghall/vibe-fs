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
                        let! state = getStore(workspaceRoot).GetSessionState(sid)
                        syncReviewProjection store sid state.ReviewTask
                        restoreFallbackRuntimeStore scope sid state
        with _ ->
            ()
    }
