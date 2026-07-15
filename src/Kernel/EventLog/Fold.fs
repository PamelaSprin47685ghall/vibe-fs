module Wanxiangshu.Kernel.EventLog.Fold

/// Phase 6: Composite projection that delegates to independent projection modules.
/// SessionState is the composite view, but each projection axis is independently
/// maintained in its own module.
///
/// Projection modules:
///   - ReviewProjection  — review loop / task state
///   - BacklogProjection — todo backlog snapshot
///   - NudgeProjection   — nudge dedup / snapshot state
///   - SubsessionProjection — subagent registry
///   - HumanTurnProjection   — human turn state
///   - ContinuationProjection — owner/lease episode state machine

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.FallbackInjectionFold
open Wanxiangshu.Kernel.EventLog.ReviewLoopFold
open Wanxiangshu.Kernel.EventLog.ReviewProjection
open Wanxiangshu.Kernel.EventLog.BacklogProjection
open Wanxiangshu.Kernel.EventLog.NudgeProjection
open Wanxiangshu.Kernel.EventLog.SubsessionProjection
open Wanxiangshu.Kernel.EventLog.HumanTurnProjection
open Wanxiangshu.Kernel.EventLog.ContinuationProjection
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.EventLog.ReviewVerdictWire
open Wanxiangshu.Kernel.FallbackKernel.Types

// ── Utilities ──

let private forSession (sessionId: string) (events: WanEvent list) : WanEvent list =
    events |> List.filter (fun e -> e.Session = sessionId)

let foldEventStream
    (sessionId: string)
    (zero: 'State)
    (folder: 'State -> WanEvent -> 'State)
    (events: WanEvent list)
    : 'State =
    forSession sessionId events |> List.fold folder zero

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let private parseIntOpt (raw: string) : int option =
    if raw = "" then
        None
    else
        try
            Some(int raw)
        with _ ->
            None

// ── Backward-compatible thin wrappers ──

/// Delegate to ReviewProjection.
let foldReviewLoop (sessionId: string) (events: WanEvent list) : ReviewLoopFold =
    ReviewProjection.foldReviewLoopStream sessionId events

/// Delegate to ReviewProjection.
let foldReviewTask (sessionId: string) (events: WanEvent list) : string option =
    ReviewProjection.foldReviewTask sessionId events

/// Delegate to BacklogProjection.
let foldWorkBacklogSnapshot (sessionId: string) (events: WanEvent list) : WorkBacklogSnapshot =
    BacklogProjection.foldBacklogStream sessionId events

/// Delegate to BacklogProjection.
let backlogEntryFromPayload (payload: Map<string, string>) : BacklogEntry option =
    BacklogProjection.backlogEntryFromPayload payload

/// Delegate to NudgeProjection.
let foldNudgeDedup (sessionId: string) (events: WanEvent list) : NudgeDedupState =
    NudgeProjection.foldDedupStream sessionId events

/// Delegate to NudgeProjection.
let foldNudgeSnapshot (sessionId: string) (events: WanEvent list) : NudgeSnapshotState =
    NudgeProjection.foldSnapshotStream sessionId events

/// Delegate to NudgeProjection.
let nudgeAnchorKey (turnId: string) (assistantMessage: string) : string =
    NudgeProjection.nudgeAnchorKey turnId assistantMessage

/// Delegate to NudgeProjection.
let isNudgeBlockedForAnchor (st: NudgeDedupState) (anchorKey: string) : bool = NudgeProjection.isBlocked st anchorKey

/// Delegate to SubsessionProjection.
let foldSubagents (sessionId: string) (events: WanEvent list) : Map<string, SubagentState> =
    SubsessionProjection.foldSubagents sessionId events

/// Delegate to ContinuationProjection.
let isEpisodeEvent (e: WanEvent) : bool = ContinuationProjection.isEpisodeEvent e

// ── Local ordinal helpers (kept private for isLateEvent) ──

let private continuationStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "continuationOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private continuationStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "continuationOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private nudgeStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "nudgeOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private nudgeStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "nudgeOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private compactionStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "compactionOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private compactionStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "compactionOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private humanTurnOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "humanTurnOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

// ── Composite SessionState ──

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

// ── Local fallback lifecycle helpers ──

let private fallbackLifecycleFolder (st: FallbackLifecycle option) (e: WanEvent) : FallbackLifecycle option =
    match e.Kind with
    | k when k = eventKindUserAbortObserved -> Some FallbackLifecycle.Cancelled
    | k when k = eventKindHumanTurnStarted -> Some FallbackLifecycle.Active
    | _ -> st

let private fallbackPhaseFolder (st: FallbackPhase option) (e: WanEvent) : FallbackPhase option =
    match e.Kind with
    | k when k = eventKindHumanTurnStarted -> Some FallbackPhase.Idle
    | _ -> st

// ── Late event detection ──

let private isLateEvent (st: SessionState) (e: WanEvent) : bool =
    match e.Kind with
    | k when k = eventKindContinuationRequested ->
        continuationStartOrdinal st.ContinuationOrdinal e <= st.ContinuationOrdinal
    | k when
        (k = eventKindContinuationDispatchStarted
         || k = eventKindContinuationDispatched
         || k = eventKindContinuationFailed
         || k = eventKindContinuationCancelled
         || k = eventKindContinuationSettled)
        ->
        continuationStageOrdinal st.ContinuationOrdinal e < st.ContinuationOrdinal
    | k when k = eventKindNudgeRequested -> nudgeStartOrdinal st.NudgeOrdinal e <= st.NudgeOrdinal
    | k when
        (k = eventKindNudgeDispatched
         || k = eventKindNudgeFailed
         || k = eventKindNudgeCancelled
         || k = eventKindNudgeSettled)
        ->
        nudgeStageOrdinal st.NudgeOrdinal e < st.NudgeOrdinal
    | k when k = eventKindCompactionStarted -> compactionStartOrdinal st.CompactionOrdinal e <= st.CompactionOrdinal
    | k when k = eventKindCompactionSettled -> compactionStageOrdinal st.CompactionOrdinal e < st.CompactionOrdinal
    | k when k = eventKindHumanTurnStarted -> humanTurnOrdinal st.HumanTurnOrdinal e <= st.HumanTurnOrdinal
    | _ -> false

let private isDuplicateHumanTurn (currentOrdinal: int) (lastMsgId: string option) (e: WanEvent) : bool =
    if e.Kind <> eventKindHumanTurnStarted then
        false
    else
        let newOrdinal = humanTurnOrdinal currentOrdinal e
        let msgId = HumanTurnProjection.messageId e

        newOrdinal <= currentOrdinal
        || (msgId.IsSome && lastMsgId.IsSome && msgId.Value = lastMsgId.Value)

// ── Main fold orchestrator ──

let applyEvent (st: SessionState) (e: WanEvent) : SessionState =
    if isLateEvent st e then
        st
    else
        // 1. Review projection
        let nextReviewLoop = ReviewLoopFold.foldEvent st.ReviewLoop e

        // 2. Human turn projection (with duplicate detection)
        let nextHumanTurn =
            if isDuplicateHumanTurn st.HumanTurnOrdinal st.LastHumanTurnMessageId e then
                st.LatestHumanTurn
            else
                HumanTurnProjection.foldSingleEvent e |> Option.orElse st.LatestHumanTurn

        // 3. Generation tracking
        let nextSessionGen, nextCancelGen, nextActiveContGen, nextActiveCancelGen =
            ContinuationProjection.foldGeneration
                (st.SessionGeneration,
                 st.CancelGeneration,
                 st.ActiveContinuationGen,
                 st.ActiveContinuationCancelGen,
                 st.LatestHumanTurn)
                e

        // 4. Owner/lease episode state
        let episodeState: OwnerEpisodeState =
            { Owner = st.SessionOwner
              ContinuationLease = st.PendingLease
              ContinuationOrdinal = st.ContinuationOrdinal
              ContinuationStage = st.ContinuationStage
              NudgeLease = st.PendingNudgeLease
              NudgeOrdinal = st.NudgeOrdinal
              NudgeStage = st.NudgeStage
              Compaction = st.ActiveCompaction
              CompactionOrdinal = st.CompactionOrdinal
              CompactionStage = st.CompactionStage
              IsCompacted = st.IsCompacted
              CompactionGeneration = st.CompactionGeneration
              SessionGeneration = nextSessionGen
              CancelGeneration = nextCancelGen
              HumanTurn = nextHumanTurn
              HumanTurnOrdinal = st.HumanTurnOrdinal
              LastHumanTurnMessageId = st.LastHumanTurnMessageId }

        let nextEpisode = ContinuationProjection.foldOwnerAndLease episodeState e

        // 5. Fallback injection
        let nextFallbackInjection =
            fallbackInjectionFolder st.ContinuationOrdinal st.ContinuationStage st.FallbackInjection e

        // 6. Nudge state (conditional on not being a late episode event)
        let isBacklog = e.Kind = eventKindWorkBacklogCommitted
        let shouldUpdateNudge = not (isEpisodeEvent e) || not (isLateEvent st e)

        // 7. Backlog (handle + truncate)
        let nextBacklog =
            if isBacklog then
                match BacklogProjection.backlogEntryFromPayload e.Payload with
                | Some entry ->
                    let nextList = entry :: st.Backlog

                    if nextList.Length > 5 then
                        List.truncate 5 nextList
                    else
                        nextList
                | None -> st.Backlog
            else
                st.Backlog

        let nextBacklogSnapshot = BacklogProjection.foldSingleEvent st.BacklogSnapshot e

        let nextNudgeDedup =
            if shouldUpdateNudge then
                NudgeProjection.foldSingleDedupEvent st.NudgeDedup e
            else
                st.NudgeDedup

        let nextNudgeSnapshot =
            if shouldUpdateNudge then
                NudgeProjection.foldSingleSnapshotEvent st.NudgeSnapshot e
            else
                st.NudgeSnapshot

        let nextSubagents = SubsessionProjection.foldSingleSubagentEvent st.Subagents e

        { ReviewLoop = nextReviewLoop
          ReviewTask = ReviewLoopFold.activeTask nextReviewLoop
          Backlog = nextBacklog
          BacklogSnapshot = nextBacklogSnapshot
          NudgeDedup = nextNudgeDedup
          NudgeSnapshot = nextNudgeSnapshot
          Subagents = nextSubagents
          FallbackInjection = nextFallbackInjection
          LatestHumanTurn = nextHumanTurn
          SessionGeneration = nextSessionGen
          CancelGeneration = nextCancelGen
          ActiveContinuationGen = nextActiveContGen
          ActiveContinuationCancelGen = nextActiveCancelGen
          FallbackLifecycle = fallbackLifecycleFolder st.FallbackLifecycle e
          FallbackPhase = fallbackPhaseFolder st.FallbackPhase e
          SessionOwner = nextEpisode.Owner
          PendingLease = nextEpisode.ContinuationLease
          ContinuationOrdinal = nextEpisode.ContinuationOrdinal
          ContinuationStage = nextEpisode.ContinuationStage
          PendingNudgeLease = nextEpisode.NudgeLease
          NudgeOrdinal = nextEpisode.NudgeOrdinal
          NudgeStage = nextEpisode.NudgeStage
          ActiveCompaction = nextEpisode.Compaction
          ActiveCompactionId = nextEpisode.Compaction |> Option.map (fun c -> c.CompactionID)
          CompactionOrdinal = nextEpisode.CompactionOrdinal
          CompactionStage = nextEpisode.CompactionStage
          IsCompacted = nextEpisode.IsCompacted
          CompactionGeneration = nextEpisode.CompactionGeneration
          HumanTurnOrdinal = nextEpisode.HumanTurnOrdinal
          LastHumanTurnMessageId = nextEpisode.LastHumanTurnMessageId
          EventCount = st.EventCount + 1 }
