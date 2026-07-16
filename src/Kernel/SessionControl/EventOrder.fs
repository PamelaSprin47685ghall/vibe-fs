module Wanxiangshu.Kernel.SessionControl.EventOrder

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.EventPayload

let payloadField = tryField
let parseIntOpt = Wanxiangshu.Kernel.EventSourcing.EventPayload.parseIntOpt

let humanTurnOrdinal =
    Wanxiangshu.Kernel.EventSourcing.EventPayload.humanTurnOrdinal

let continuationStartOrdinal =
    Wanxiangshu.Kernel.EventSourcing.EventPayload.continuationStartOrdinal

let continuationStageOrdinal =
    Wanxiangshu.Kernel.EventSourcing.EventPayload.continuationStageOrdinal

let nudgeStartOrdinal =
    Wanxiangshu.Kernel.EventSourcing.EventPayload.nudgeStartOrdinal

let nudgeStageOrdinal =
    Wanxiangshu.Kernel.EventSourcing.EventPayload.nudgeStageOrdinal

let compactionStartOrdinal =
    Wanxiangshu.Kernel.EventSourcing.EventPayload.compactionStartOrdinal

let compactionStageOrdinal =
    Wanxiangshu.Kernel.EventSourcing.EventPayload.compactionStageOrdinal

let isEpisodeEvent (e: WanEvent) : bool =
    e.Kind = eventKindContinuationRequested
    || e.Kind = eventKindContinuationDispatchStarted
    || e.Kind = eventKindContinuationDispatched
    || e.Kind = eventKindContinuationFailed
    || e.Kind = eventKindContinuationCancelled
    || e.Kind = eventKindContinuationSettled
    || e.Kind = eventKindNudgeRequested
    || e.Kind = eventKindNudgeDispatched
    || e.Kind = eventKindNudgeFailed
    || e.Kind = eventKindNudgeCancelled
    || e.Kind = eventKindNudgeSettled
    || e.Kind = eventKindCompactionStarted
    || e.Kind = eventKindCompactionSettled
    || e.Kind = eventKindHumanTurnStarted
