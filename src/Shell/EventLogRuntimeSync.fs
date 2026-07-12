module Wanxiangshu.Shell.EventLogRuntimeSync

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.EventLogRuntimeStore
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.ReviewReplaySync
open Wanxiangshu.Shell.SessionProjectionStore
open Wanxiangshu.Shell.RuntimeScope

let restoreFallbackRuntimeState
    (scope: RuntimeScope)
    (sid: string)
    (state: Wanxiangshu.Kernel.EventLog.Fold.SessionState)
    : unit =
    match scope.TryFindKey("fallbackRuntime") with
    | Some obj ->
        let rt = unbox<Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState> obj

        match state.LatestHumanTurn with
        | Some turn ->
            let modelStr =
                turn.Provider
                + "/"
                + turn.Model
                + (if turn.Variant <> "" then ":" + turn.Variant else "")

            rt.SetLatestHumanModel sid modelStr
            rt.SetHumanTurnId sid turn.TurnId

            if turn.Agent <> "" then
                rt.SetAgentName sid turn.Agent
        | None -> ()

        rt.SetSessionGeneration sid state.SessionGeneration
        rt.SetCancelGeneration sid state.CancelGeneration
        rt.SetActiveContinuationGeneration sid state.ActiveContinuationGen
        rt.SetActiveContinuationCancelGeneration sid state.ActiveContinuationCancelGen

        let fallbackState = rt.GetOrCreateState sid

        let updatedFallbackState =
            { fallbackState with
                Lifecycle =
                    state.FallbackLifecycle
                    |> Option.defaultValue Wanxiangshu.Kernel.FallbackKernel.Types.FallbackLifecycle.Active
                Phase =
                    state.FallbackPhase
                    |> Option.defaultValue Wanxiangshu.Kernel.FallbackKernel.Types.FallbackPhase.Idle }

        rt.UpdateState sid updatedFallbackState

        match state.SessionOwner with
        | Some o -> rt.SetSessionOwner sid o
        | None -> ()

        match state.PendingLease with
        | Some lease ->
            let modelObj =
                match Wanxiangshu.Shell.FallbackMessageCodec.decodeModelFromObj (box lease.Model) with
                | Some m -> m
                | None ->
                    { ProviderID = ""
                      ModelID = lease.Model
                      Variant = None
                      Temperature = None
                      TopP = None
                      MaxTokens = None
                      ReasoningEffort = None
                      Thinking = false }

            let pendingLease: Wanxiangshu.Shell.FallbackRuntimeState.PendingLease =
                { ContinuationID = lease.ContinuationID
                  SessionGeneration = lease.SessionGeneration
                  HumanTurnID = lease.HumanTurnID
                  CancelGeneration = lease.CancelGeneration
                  Owner = lease.Owner
                  Model = modelObj
                  PromptText = lease.PromptText
                  Status = lease.Status }

            rt.SetPendingLease(sid, pendingLease)
        | None -> rt.ClearPendingLease sid

        match state.ActiveCompactionId with
        | Some cid -> rt.SetActiveCompactionId(sid, cid)
        | None -> ()

        rt.SetCompacted sid state.IsCompacted
    | None -> ()

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
                        restoreFallbackRuntimeState scope sid state
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
