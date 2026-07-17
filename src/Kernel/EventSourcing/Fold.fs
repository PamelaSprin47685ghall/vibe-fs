module Wanxiangshu.Kernel.EventSourcing.Fold

/// SessionState is the composite view, but each projection axis is independently
/// maintained in its own module.
///
/// Projection modules:
///   - ReviewProjection  — review loop / task state
///   - BacklogProjection — todo backlog snapshot
///   - NudgeProjection   — nudge dedup / snapshot state
///   - SubsessionProjection — subagent registry
///   - HumanTurn   — human turn state
///   - Projection — owner/lease episode state machine

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.EventPayload
open Wanxiangshu.Kernel.EventSourcing.SessionState
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
open Wanxiangshu.Kernel.Review.ReviewVerdictWire
open Wanxiangshu.Kernel.FallbackKernel.Types

type SessionState = Wanxiangshu.Kernel.EventSourcing.SessionState.SessionState

let emptySessionState =
    Wanxiangshu.Kernel.EventSourcing.SessionState.emptySessionState

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
        let msgId = HumanTurn.messageId e

        newOrdinal <= currentOrdinal
        || (msgId.IsSome && lastMsgId.IsSome && msgId.Value = lastMsgId.Value)

let private computeNextHumanTurn (st: SessionState) (e: WanEvent) =
    if isDuplicateHumanTurn st.HumanTurnOrdinal st.LastHumanTurnMessageId e then
        st.LatestHumanTurn
    else
        HumanTurn.foldSingleEvent e |> Option.orElse st.LatestHumanTurn

let private computeNextBacklog (st: SessionState) (e: WanEvent) : BacklogEntry list =
    if e.Kind = eventKindWorkBacklogCommitted then
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

let private foldEpisode
    (st: SessionState)
    (nextHumanTurn: HumanTurnState option)
    (nextSessionGen: int)
    (nextCancelGen: int)
    (e: WanEvent)
    =
    let epState =
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

    Projection.foldOwnerAndLease epState e

let private createNextSessionState
    (st: SessionState)
    (nextEpisode: OwnerEpisodeState)
    (nextReviewLoop: ReviewLoopFold)
    (nextBacklog: BacklogEntry list)
    (nextBacklogSnapshot: WorkBacklogSnapshot)
    (nextNudgeDedup: NudgeDedupState)
    (nextNudgeSnapshot: NudgeSnapshotState)
    (nextSubagents: Map<string, SubagentState>)
    (nextFallbackInjection: FallbackInjectionState)
    (nextHumanTurn: HumanTurnState option)
    (nextSessionGen: int)
    (nextCancelGen: int)
    (nextActiveContGen: int)
    (nextActiveCancelGen: int)
    (e: WanEvent)
    : SessionState =
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

// ── Main fold orchestrator ──

let applyEvent (st: SessionState) (e: WanEvent) : SessionState =
    if isLateEvent st e then
        st
    else
        let nextReviewLoop = ReviewLoopFold.foldEvent st.ReviewLoop e
        let nextHumanTurn = computeNextHumanTurn st e

        let nextSessionGen, nextCancelGen, nextActiveContGen, nextActiveCancelGen =
            match Wanxiangshu.Kernel.SessionControl.Event.decode e with
            | Some ev ->
                Projection.foldGeneration
                    (st.SessionGeneration,
                     st.CancelGeneration,
                     st.ActiveContinuationGen,
                     st.ActiveContinuationCancelGen,
                     st.LatestHumanTurn)
                    ev
            | None ->
                st.SessionGeneration, st.CancelGeneration, st.ActiveContinuationGen, st.ActiveContinuationCancelGen

        let nextEpisode = foldEpisode st nextHumanTurn nextSessionGen nextCancelGen e

        let nextFallbackInjection =
            fallbackInjectionFolder st.ContinuationOrdinal st.ContinuationStage st.FallbackInjection e

        let shouldUpdateNudge = not (Projection.isEpisodeEvent e) || not (isLateEvent st e)
        let nextBacklog = computeNextBacklog st e
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

        createNextSessionState
            st
            nextEpisode
            nextReviewLoop
            nextBacklog
            nextBacklogSnapshot
            nextNudgeDedup
            nextNudgeSnapshot
            nextSubagents
            nextFallbackInjection
            nextHumanTurn
            nextSessionGen
            nextCancelGen
            nextActiveContGen
            nextActiveCancelGen
            e
