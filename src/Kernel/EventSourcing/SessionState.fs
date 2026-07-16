module Wanxiangshu.Kernel.EventSourcing.SessionState

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.Fallback
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Backlog
open Wanxiangshu.Kernel.SessionControl
open Wanxiangshu.Kernel.Subsession
open Wanxiangshu.Kernel.Fallback.FallbackInjectionFold
open Wanxiangshu.Kernel.Review.ReviewLoopFold
open Wanxiangshu.Kernel.Review.ReviewProjection
open Wanxiangshu.Kernel.Backlog.BacklogProjection
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Subsession.SubsessionProjection
open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.Projection
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.Backlog.BacklogTypes
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.FallbackKernel.Types

/// Composite projection state assembled from per-axis folds.
type SessionState =
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
      ActiveContinuationGen: int
      ActiveContinuationCancelGen: int
      FallbackLifecycle: FallbackLifecycle option
      FallbackPhase: FallbackPhase option
      SessionOwner: string option
      PendingLease: ReplayLeaseState option
      ContinuationOrdinal: int
      ContinuationStage: EpisodeStage
      PendingNudgeLease: ReplayNudgeLeaseState option
      NudgeOrdinal: int
      NudgeStage: EpisodeStage
      ActiveCompaction: ReplayCompactionState option
      ActiveCompactionId: string option
      CompactionOrdinal: int
      CompactionStage: EpisodeStage
      IsCompacted: bool
      CompactionGeneration: int
      HumanTurnOrdinal: int
      LastHumanTurnMessageId: string option
      EventCount: int }

let emptySessionState () : SessionState =
    { ReviewLoop = ReviewLoopFold.initial
      ReviewTask = None
      Backlog = []
      BacklogSnapshot = BacklogProjection.emptySnapshot
      NudgeDedup = NudgeProjection.emptyDedupState
      NudgeSnapshot = NudgeProjection.emptySnapshotState
      Subagents = Map.empty
      FallbackInjection = emptyFallbackInjectionState
      LatestHumanTurn = None
      SessionGeneration = 0
      CancelGeneration = 0
      ActiveContinuationGen = 0
      ActiveContinuationCancelGen = 0
      FallbackLifecycle = None
      FallbackPhase = None
      SessionOwner = None
      PendingLease = None
      ContinuationOrdinal = 0
      ContinuationStage = NoEpisode
      PendingNudgeLease = None
      NudgeOrdinal = 0
      NudgeStage = NoEpisode
      ActiveCompaction = None
      ActiveCompactionId = None
      CompactionOrdinal = 0
      CompactionStage = NoEpisode
      IsCompacted = false
      CompactionGeneration = 0
      HumanTurnOrdinal = 0
      LastHumanTurnMessageId = None
      EventCount = 0 }
