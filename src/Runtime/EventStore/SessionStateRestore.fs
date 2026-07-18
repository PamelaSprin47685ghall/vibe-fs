module Wanxiangshu.Runtime.SessionStateRestore

open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.HumanTurnTransitions
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.EventSourcing.Fold

// ── Decoders ──────────────────────────────────────────────────────────

/// Decode a SessionOwner from its string representation.
let decodeOwner (owner: string) : SessionOwner =
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
let decodeLeaseStatus (status: string) : LeaseStatus =
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
let decodeFallbackModel
    (lease: Wanxiangshu.Kernel.SessionControl.State.ReplayLeaseState)
    : FallbackModel =
    match decodeModelFromObj (box lease.Model) with
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

// ── Restore helpers ────────────────────────────────────────────────────

/// Restore human-turn derived fields (LatestHumanModel, HumanTurnId,
/// AgentName) from SessionState into the FallbackRuntimeStore.
let restoreHumanTurnInfo
    (state: SessionState)
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
let restoreOrdinals
    (state: SessionState)
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
let restorePendingLease
    (state: SessionState)
    (rt: FallbackRuntimeStore)
    (sid: string)
    : unit =
    match state.PendingLease with
    | Some lease ->
        let modelObj = decodeFallbackModel lease

        let leaseOwner = decodeOwner lease.Owner

        let leaseStatus = decodeLeaseStatus lease.Status

        let pendingLease: PendingLease =
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
let restoreCompaction
    (state: SessionState)
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
let restorePendingNudgeLease
    (state: SessionState)
    (rt: FallbackRuntimeStore)
    (sid: string)
    : unit =
    match state.PendingNudgeLease with
    | Some nl ->
        let nudgeStatus = decodeLeaseStatus nl.Status

        let lease: NudgeLease =
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
