module Wanxiangshu.Runtime.SessionStateRestore

open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.SessionControl.State

// ── Decoders ──────────────────────────────────────────────────────────

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
let private decodeFallbackModel (lease: ReplayLeaseState) : FallbackModel =
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

// ── Local restore helpers ─────────────────────────────────────────────

let private restoreHumanTurnDerived
    (state: SessionState)
    (s: FallbackSessionRuntime)
    : string * string option * string =
    match state.LatestHumanTurn with
    | Some turn ->
        let modelStr =
            turn.Provider
            + "/"
            + turn.Model
            + (if turn.Variant <> "" then ":" + turn.Variant else "")

        let agent = if turn.Agent <> "" then turn.Agent else s.AgentName
        agent, Some modelStr, turn.TurnId
    | None -> s.AgentName, s.LatestHumanModel, s.HumanTurnId

let private restoreOrdinals (state: SessionState) (s: FallbackSessionRuntime) : int * int * int * int =
    max s.HumanTurnOrdinal state.HumanTurnOrdinal,
    max s.ContinuationOrdinal state.ContinuationOrdinal,
    max s.NudgeOrdinal state.NudgeOrdinal,
    max s.CompactionOrdinal state.CompactionOrdinal

let private decodePendingLease (lease: ReplayLeaseState) : PendingLease =
    { ContinuationID = lease.ContinuationID
      ContinuationOrdinal = lease.ContinuationOrdinal
      SessionGeneration = lease.SessionGeneration
      HumanTurnID = lease.HumanTurnID
      CancelGeneration = lease.CancelGeneration
      Owner = decodeOwner lease.Owner
      Model = decodeFallbackModel lease
      PromptText = lease.PromptText
      Status = decodeLeaseStatus lease.Status }

let private decodeNudgeLease (nl: ReplayNudgeLeaseState) : NudgeLease =
    { NudgeID = nl.NudgeID
      NudgeOrdinal = nl.NudgeOrdinal
      Nonce = nl.Nonce
      HumanTurnID = nl.HumanTurnID
      SessionGeneration = nl.SessionGeneration
      CancelGeneration = nl.CancelGeneration
      Owner = SessionOwner.Nudge
      Status = decodeLeaseStatus nl.Status }

let private restoreCompactionIdentity (state: SessionState) : string * int * string * int =
    match state.ActiveCompaction, state.ActiveCompactionId with
    | Some comp, _ -> comp.CompactionID, comp.CompactionOrdinal, comp.HumanTurnID, comp.CancelGeneration
    | None, Some cid -> cid, state.CompactionOrdinal, "", 0
    | None, None -> "", 0, "", 0

let private nextCoreFromState (state: SessionState) (core: SessionFallbackState) : SessionFallbackState =
    { core with
        Lifecycle = state.FallbackLifecycle |> Option.defaultValue FallbackLifecycle.Active
        Phase = state.FallbackPhase |> Option.defaultValue FallbackPhase.Idle }

let private applyOwner (state: SessionState) (afterLifecycle: FallbackSessionRuntime) : SessionOwner =
    state.SessionOwner
    |> Option.map decodeOwner
    |> Option.defaultValue afterLifecycle.Owner

let private buildBaseState
    (s: FallbackSessionRuntime)
    (agentName: string, latestHumanModel: string option, humanTurnId: string)
    (ordinals: int * int * int * int)
    (state: SessionState)
    (nextCore: SessionFallbackState)
    : FallbackSessionRuntime =
    let h, c, n, co = ordinals

    { s with
        AgentName = agentName
        LatestHumanModel = latestHumanModel
        HumanTurnId = humanTurnId
        LastHumanMessageId = state.LastHumanTurnMessageId |> Option.defaultValue ""
        SessionGeneration = state.SessionGeneration
        CancelGeneration = state.CancelGeneration
        ActiveContinuationGen = state.ActiveContinuationGen
        ActiveContinuationCancelGen = state.ActiveContinuationCancelGen
        HumanTurnOrdinal = h
        ContinuationOrdinal = c
        NudgeOrdinal = n
        CompactionOrdinal = co
        Core = nextCore }

let private applyLifecycleReset (baseState: FallbackSessionRuntime) (nextCore: SessionFallbackState) =
    if nextCore.Lifecycle = FallbackLifecycle.Cancelled then
        let reset = cancelEpisode baseState
        { reset with Core = nextCore }
    else
        baseState

// ── Pure restoration transition ───────────────────────────────────────

/// Restore the in-memory FallbackSessionRuntime from a durable SessionState.
/// This is a single pure transition: the runtime store is responsible for
/// atomically reading the current snapshot and committing the returned state.
let restoreFromEventLogState (state: SessionState) (s: FallbackSessionRuntime) : FallbackSessionRuntime =
    let human = restoreHumanTurnDerived state s
    let ordinals = restoreOrdinals state s
    let nextCore = nextCoreFromState state s.Core

    let pendingLease = state.PendingLease |> Option.map decodePendingLease
    let pendingNudgeLease = state.PendingNudgeLease |> Option.map decodeNudgeLease

    let compactionId, compactionOrdinal, compactionHumanTurnId, compactionCancelGeneration =
        restoreCompactionIdentity state

    let baseState = buildBaseState s human ordinals state nextCore
    let afterLifecycle = applyLifecycleReset baseState nextCore

    { afterLifecycle with
        Owner = applyOwner state afterLifecycle
        PendingLease = pendingLease
        PendingNudgeLease = pendingNudgeLease
        CompactionActiveId = compactionId
        CompactionActiveOrdinal = compactionOrdinal
        CompactionHumanTurnId = compactionHumanTurnId
        CompactionCancelGeneration = compactionCancelGeneration
        CompactionGeneration = state.CompactionGeneration
        CompactionCompacted = state.IsCompacted }
