module Wanxiangshu.Kernel.SessionOverview

/// Combines all independent projections into a clean query interface.
/// Process Managers should read only the projections they need.

open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.Backlog.BacklogTypes
open Wanxiangshu.Kernel.Backlog.BacklogProjection
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Subsession.SubsessionProjection
open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.Projection
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.Fallback.FallbackInjectionFold
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.FallbackKernel.Types

type SessionOverview =
    { ReviewLoop: ReviewLoopFold
      ReviewTask: string option
      Backlog: BacklogEntry list
      BacklogSnapshot: WorkBacklogSnapshot
      NudgeDedup: NudgeDedupState
      NudgeSnapshot: NudgeSnapshotState
      Subagents: Map<string, SubagentState>
      FallbackInjection: FallbackInjectionState
      LatestHumanTurn: HumanTurnState option
      SessionGeneration: int
      CancelGeneration: int
      FallbackLifecycle: FallbackLifecycle option
      FallbackPhase: FallbackPhase option
      SessionOwner: string option
      PendingLease: ReplayLeaseState option
      PendingNudgeLease: ReplayNudgeLeaseState option
      ActiveCompaction: ReplayCompactionState option
      HumanTurnOrdinal: int
      EventCount: int }

/// Build a SessionOverview from a SessionState by extracting each projection field.
/// This is the canonical mapping function — Process Managers should use this
/// instead of accessing SessionState fields directly.
let fromSessionState (st: SessionState) : SessionOverview =
    { ReviewLoop = st.ReviewLoop
      ReviewTask = st.ReviewTask
      Backlog = st.Backlog
      BacklogSnapshot = st.BacklogSnapshot
      NudgeDedup = st.NudgeDedup
      NudgeSnapshot = st.NudgeSnapshot
      Subagents = st.Subagents
      FallbackInjection = st.FallbackInjection
      LatestHumanTurn = st.LatestHumanTurn
      SessionGeneration = st.SessionGeneration
      CancelGeneration = st.CancelGeneration
      FallbackLifecycle = st.FallbackLifecycle
      FallbackPhase = st.FallbackPhase
      SessionOwner = st.SessionOwner
      PendingLease = st.PendingLease
      PendingNudgeLease = st.PendingNudgeLease
      ActiveCompaction = st.ActiveCompaction
      HumanTurnOrdinal = st.HumanTurnOrdinal
      EventCount = st.EventCount }

let emptyOverview: SessionOverview = fromSessionState (emptySessionState ())

/// Direct record construction for testing — no Fold dependency required.
let emptyOverviewDirect: SessionOverview =
    { ReviewLoop = initial
      ReviewTask = None
      Backlog = []
      BacklogSnapshot = emptySnapshot
      NudgeDedup = emptyDedupState
      NudgeSnapshot = emptySnapshotState
      Subagents = Map.empty
      FallbackInjection = emptyFallbackInjectionState
      LatestHumanTurn = None
      SessionGeneration = 0
      CancelGeneration = 0
      FallbackLifecycle = None
      FallbackPhase = None
      SessionOwner = None
      PendingLease = None
      PendingNudgeLease = None
      ActiveCompaction = None
      HumanTurnOrdinal = 0
      EventCount = 0 }
