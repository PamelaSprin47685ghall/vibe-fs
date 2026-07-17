module Wanxiangshu.Runtime.EventLogRuntimeSync

open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions

open Fable.Core
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.ReviewRuntime

open Wanxiangshu.Runtime.SessionProjectionStore
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.HumanTurnTransitions
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions

// ---- Helpers extracted from restoreFallbackRuntimeStore ----

/// Decode a SessionOwner from its string representation.
let private decodeOwner (owner: string) : SessionOwner =
    match owner with
    | "NoOwner"
    | "None" -> SessionOwner.NoOwner
    | "Human" -> SessionOwner.Human
    | "Fallback" -> SessionOwner.Fallback
    | "Nudge" -> SessionOwner.Nudge
    | "Compaction" -> SessionOwner.Compaction
    | "Title" -> SessionOwner.Title
    | _ -> SessionOwner.NoOwner

/// Decode a LeaseStatus from its string representation.
let private decodeLeaseStatus (status: string) : LeaseStatus =
    match status with
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

/// Decode the model stored in a ReplayLeaseState (a plain string) to a
/// FallbackModel via the shared codec, falling back to a minimal record on
/// failure.
let private decodeFallbackModel
    (lease: Wanxiangshu.Kernel.SessionControl.State.ReplayLeaseState)
    : Wanxiangshu.Kernel.FallbackKernel.Types.FallbackModel =
    match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.decodeModelFromObj (box lease.Model) with
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

/// Restore human-turn derived fields (LatestHumanModel, HumanTurnId,
/// AgentName) from SessionState into the FallbackRuntimeStore.
let private restoreHumanTurnInfo
    (state: Wanxiangshu.Kernel.EventSourcing.Fold.SessionState)
    (rt: FallbackRuntimeStore)
    (sid: string)
    : unit =
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

/// Restore all ordinal fields (HumanTurnOrdinal, ContinuationOrdinal,
/// NudgeOrdinal, CompactionOrdinal) from SessionState into the
/// FallbackRuntimeStore, using monotonic-max semantics.
let private restoreOrdinals
    (state: Wanxiangshu.Kernel.EventSourcing.Fold.SessionState)
    (rt: FallbackRuntimeStore)
    (sid: string)
    : unit =
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

/// Restore the pending continuation lease from SessionState into the
/// FallbackRuntimeStore.  Decodes Owner, Status, and Model via the shared
/// helpers above.
let private restorePendingLease
    (state: Wanxiangshu.Kernel.EventSourcing.Fold.SessionState)
    (rt: FallbackRuntimeStore)
    (sid: string)
    : unit =
    match state.PendingLease with
    | Some lease ->
        let modelObj = decodeFallbackModel lease

        let leaseOwner = decodeOwner lease.Owner

        let leaseStatus = decodeLeaseStatus lease.Status

        let pendingLease: Wanxiangshu.Runtime.Fallback.SessionRuntime.PendingLease =
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

/// Restore compaction fields (active id + ordinal, IsCompacted,
/// CompactionGeneration) from SessionState into the FallbackRuntimeStore.
let private restoreCompaction
    (state: Wanxiangshu.Kernel.EventSourcing.Fold.SessionState)
    (rt: FallbackRuntimeStore)
    (sid: string)
    : unit =
    match state.ActiveCompaction, state.ActiveCompactionId with
    | Some comp, _ -> rt.SetActiveCompactionId(sid, comp.CompactionID, comp.CompactionOrdinal)
    | None, Some cid -> rt.SetActiveCompactionId(sid, cid, state.CompactionOrdinal)
    | None, None -> ()

    rt.SetCompacted(sid, state.IsCompacted)
    rt.SetCompactionGeneration(sid, state.CompactionGeneration)

/// Restore the pending nudge lease from SessionState into the
/// FallbackRuntimeStore.  Decodes Status via decodeLeaseStatus.
let private restorePendingNudgeLease
    (state: Wanxiangshu.Kernel.EventSourcing.Fold.SessionState)
    (rt: FallbackRuntimeStore)
    (sid: string)
    : unit =
    match state.PendingNudgeLease with
    | Some nl ->
        let nudgeStatus = decodeLeaseStatus nl.Status

        let lease: Wanxiangshu.Runtime.Fallback.SessionRuntime.NudgeLease =
            { NudgeID = nl.NudgeID
              NudgeOrdinal = nl.NudgeOrdinal
              Nonce = nl.Nonce
              HumanTurnID = nl.HumanTurnID
              SessionGeneration = nl.SessionGeneration
              CancelGeneration = nl.CancelGeneration
              Owner = Wanxiangshu.Kernel.FallbackKernel.Types.SessionOwner.Nudge
              Status = nudgeStatus }

        rt.SetPendingNudgeLease(sid, lease)
    | None -> rt.ClearPendingNudgeLease sid

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
                    |> Option.defaultValue Wanxiangshu.Kernel.FallbackKernel.Types.FallbackLifecycle.Active
                Phase =
                    state.FallbackPhase
                    |> Option.defaultValue Wanxiangshu.Kernel.FallbackKernel.Types.FallbackPhase.Idle }

        rt.UpdateState sid updatedFallbackState

        match state.SessionOwner with
        | Some o -> rt.SetSessionOwner sid (decodeOwner o)
        | None -> ()

        restorePendingLease state rt sid

        restoreCompaction state rt sid

        restorePendingNudgeLease state rt sid
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
