module Wanxiangshu.Kernel.SessionControl.State

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.SessionControl.HumanTurn

// ── Lease state types ──

type ReplayLeaseState =
    { ContinuationID: string
      ContinuationOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      HostUserMessageId: string
      CancelGeneration: int
      Owner: string
      Model: string
      PromptText: string option
      Status: string }

type ReplayNudgeLeaseState =
    { NudgeID: string
      NudgeOrdinal: int
      Nonce: string
      Anchor: string
      HumanTurnID: string
      HostUserMessageId: string
      SessionGeneration: int
      CancelGeneration: int
      Status: string }

type ReplayCompactionState =
    { CompactionID: string
      CompactionOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      CancelGeneration: int
      Status: string }

// ── Episode state ──

type OwnerEpisodeState =
    { Owner: string option
      ContinuationLease: ReplayLeaseState option
      ContinuationOrdinal: int
      ContinuationStage: EpisodeStage
      NudgeLease: ReplayNudgeLeaseState option
      NudgeOrdinal: int
      NudgeStage: EpisodeStage
      Compaction: ReplayCompactionState option
      CompactionOrdinal: int
      CompactionStage: EpisodeStage
      IsCompacted: bool
      CompactionGeneration: int
      SessionGeneration: int
      CancelGeneration: int
      HumanTurn: HumanTurnState option
      HumanTurnOrdinal: int
      LastHumanTurnMessageId: string option }

let emptyEpisodeState: OwnerEpisodeState =
    { Owner = None
      ContinuationLease = None
      ContinuationOrdinal = 0
      ContinuationStage = NoEpisode
      NudgeLease = None
      NudgeOrdinal = 0
      NudgeStage = NoEpisode
      Compaction = None
      CompactionOrdinal = 0
      CompactionStage = NoEpisode
      IsCompacted = false
      CompactionGeneration = 0
      SessionGeneration = 0
      CancelGeneration = 0
      HumanTurn = None
      HumanTurnOrdinal = 0
      LastHumanTurnMessageId = None }
