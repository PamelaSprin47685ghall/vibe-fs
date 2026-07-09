module Wanxiangshu.Shell.EventLogRuntimeSync

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.EventLogRuntimeStore
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.ReviewReplaySync
open Wanxiangshu.Shell.SessionProjectionStore
open Wanxiangshu.Shell.RuntimeScope

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
                    let! allEvents = getStore(workspaceRoot).ReadAllEvents()

                    let sessionIds =
                        allEvents |> Seq.map (fun e -> e.Session) |> Seq.distinct |> Seq.toList

                    for sid in sessionIds do
                        let! state = getStore(workspaceRoot).GetSessionState(sid)
                        syncReviewProjection store sid state.ReviewTask
                        scope.Projection.StoreBacklog(host, sid, List.rev state.Backlog)
        with _ ->
            ()
    }

let syncReviewFromEventLogDedicated
    (store: ReviewStore)
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
                    syncReviewProjection store sessionID state.ReviewTask
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
