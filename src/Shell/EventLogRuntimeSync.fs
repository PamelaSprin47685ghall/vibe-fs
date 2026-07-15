module Wanxiangshu.Shell.EventLogRuntimeSync

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.EventLogRuntimeStore
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.ReviewReplaySync
open Wanxiangshu.Shell.SessionProjectionStore
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Kernel.FallbackKernel.Types

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
        let curHuman = rt.GetHumanTurnOrdinal sid

        if state.HumanTurnOrdinal > curHuman then
            rt.SetHumanTurnOrdinal sid state.HumanTurnOrdinal

        let curCont = rt.GetContinuationOrdinal sid

        if state.ContinuationOrdinal > curCont then
            rt.SetContinuationOrdinal sid state.ContinuationOrdinal

        let curNudge = rt.GetNudgeOrdinal sid

        if state.NudgeOrdinal > curNudge then
            rt.SetNudgeOrdinal sid state.NudgeOrdinal

        let curComp = rt.GetCompactionOrdinal sid

        if state.CompactionOrdinal > curComp then
            rt.SetCompactionOrdinal sid state.CompactionOrdinal

        match state.LastHumanTurnMessageId with
        | Some id -> rt.SetLastHumanMessageId sid id
        | None -> rt.ClearLastHumanMessageId sid

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
        | Some o ->
            let ownerObj =
                match o with
                | "NoOwner"
                | "None" -> SessionOwner.NoOwner
                | "Human" -> SessionOwner.Human
                | "Fallback" -> SessionOwner.Fallback
                | "Nudge" -> SessionOwner.Nudge
                | "Compaction" -> SessionOwner.Compaction
                | "Title" -> SessionOwner.Title
                | _ -> SessionOwner.NoOwner

            rt.SetSessionOwner sid ownerObj
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

            let leaseOwner =
                match lease.Owner with
                | "NoOwner"
                | "None" -> SessionOwner.NoOwner
                | "Human" -> SessionOwner.Human
                | "Fallback" -> SessionOwner.Fallback
                | "Nudge" -> SessionOwner.Nudge
                | "Compaction" -> SessionOwner.Compaction
                | "Title" -> SessionOwner.Title
                | _ -> SessionOwner.NoOwner

            let leaseStatus =
                match lease.Status with
                | "requested"
                | "Requested" -> LeaseStatus.Requested
                | "dispatch_started"
                | "DispatchStarted" -> LeaseStatus.DispatchStarted
                | "dispatched"
                | "Dispatched" -> LeaseStatus.Dispatched
                | "running"
                | "Running" -> LeaseStatus.Running
                | "cancelled"
                | "Cancelled" -> LeaseStatus.Cancelled
                | _ -> LeaseStatus.Requested

            let pendingLease: Wanxiangshu.Shell.FallbackRuntimeState.PendingLease =
                { ContinuationID = lease.ContinuationID
                  ContinuationOrdinal = lease.ContinuationOrdinal
                  SessionGeneration = lease.SessionGeneration
                  HumanTurnID = lease.HumanTurnID
                  CancelGeneration = lease.CancelGeneration
                  Owner = leaseOwner
                  Model = modelObj
                  PromptText = lease.PromptText
                  Status = leaseStatus }

            rt.SetPendingLease(sid, pendingLease)
        | None -> rt.ClearPendingLease sid

        match state.ActiveCompaction, state.ActiveCompactionId with
        | Some comp, _ -> rt.SetActiveCompactionId(sid, comp.CompactionID, comp.CompactionOrdinal)
        | None, Some cid -> rt.SetActiveCompactionId(sid, cid, state.CompactionOrdinal)
        | None, None -> ()

        rt.SetCompacted sid state.IsCompacted
        rt.SetCompactionGeneration sid state.CompactionGeneration

        match state.PendingNudgeLease with
        | Some nl ->
            let nudgeStatus =
                match nl.Status with
                | "requested"
                | "Requested" -> LeaseStatus.Requested
                | "dispatch_started"
                | "DispatchStarted" -> LeaseStatus.DispatchStarted
                | "dispatched"
                | "Dispatched" -> LeaseStatus.Dispatched
                | "running"
                | "Running" -> LeaseStatus.Running
                | "cancelled"
                | "Cancelled" -> LeaseStatus.Cancelled
                | _ -> LeaseStatus.Requested

            let lease: Wanxiangshu.Shell.FallbackRuntimeState.NudgeLease =
                { NudgeID = nl.NudgeID
                  NudgeOrdinal = nl.NudgeOrdinal
                  Nonce = nl.Nonce
                  HumanTurnID = nl.HumanTurnID
                  SessionGeneration = nl.SessionGeneration
                  CancelGeneration = nl.CancelGeneration
                  Owner = SessionOwner.Nudge
                  Status = nudgeStatus }

            rt.SetPendingNudgeLease(sid, lease)
        | None -> rt.ClearPendingNudgeLease sid
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
                    let! allStates = getStore(workspaceRoot).GetAllSessionStates()
                    let sessionIds = allStates |> Map.keys |> Seq.toList

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
