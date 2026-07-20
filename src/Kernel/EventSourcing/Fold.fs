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
open Wanxiangshu.Kernel.EventSourcing.FoldApply
open Wanxiangshu.Kernel.Fallback
open Wanxiangshu.Kernel.Review
open Wanxiangshu.Kernel.Backlog
open Wanxiangshu.Kernel.SessionControl
open Wanxiangshu.Kernel.Subsession
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

// ── Main fold orchestrator ──

let private getNextGeneration (st: SessionState) (e: WanEvent) =
    match Wanxiangshu.Kernel.SessionControl.Event.decode e with
    | Some ev ->
        Projection.foldGeneration
            (st.SessionGeneration,
             st.CancelGeneration,
             st.ActiveContinuationGen,
             st.ActiveContinuationCancelGen,
             st.LatestHumanTurn)
            ev
    | None -> st.SessionGeneration, st.CancelGeneration, st.ActiveContinuationGen, st.ActiveContinuationCancelGen

let private applyEventInner (st: SessionState) (e: WanEvent) : SessionState =
    let nextReviewLoop = ReviewLoopFold.foldEvent st.ReviewLoop e
    let nextHumanTurn = computeNextHumanTurn st e

    let nextSessionGen, nextCancelGen, nextActiveContGen, nextActiveCancelGen =
        getNextGeneration st e

    let nextEpisode = foldEpisode st nextHumanTurn nextSessionGen nextCancelGen e
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
        nextHumanTurn
        nextSessionGen
        nextCancelGen
        nextActiveContGen
        nextActiveCancelGen
        e

let applyEvent (st: SessionState) (e: WanEvent) : SessionState =
    match e.EventId with
    | Some eid when st.ProcessedEventIds.Contains(eid) -> st
    | _ -> if isLateEvent st e then st else applyEventInner st e
