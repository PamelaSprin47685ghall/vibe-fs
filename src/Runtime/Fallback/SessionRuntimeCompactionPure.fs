module Wanxiangshu.Runtime.Fallback.SessionRuntimeCompactionPure

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntime

let setActiveCompactionId id ordinal humanTurnId cancelGeneration (s: FallbackSessionRuntime) =
    { s with
        CompactionActiveId = id
        CompactionActiveOrdinal = ordinal
        CompactionHumanTurnId = humanTurnId
        CompactionCancelGeneration = cancelGeneration }

let startCompaction
    (compactionId: string)
    (compactionOrdinal: int)
    (humanTurnId: string)
    (cancelGeneration: int)
    (currentGen: int)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime =
    { s with
        CompactionActiveId = compactionId
        CompactionActiveOrdinal = compactionOrdinal
        CompactionHumanTurnId = humanTurnId
        CompactionCancelGeneration = cancelGeneration
        CompactionGeneration = currentGen
        CompactionCompacted = false
        CompactionContinuationObserved = false
        CompactionSummaryTransformPending = true
        Owner = SessionOwner.Compaction }

let tryGetSettleInfo expectedCompactionID (s: FallbackSessionRuntime) =
    if s.CompactionActiveId = expectedCompactionID then
        Some(s.CompactionActiveId, s.CompactionActiveOrdinal)
    else
        None

let applySettle expectedCompactionID (s: FallbackSessionRuntime) =
    if s.CompactionActiveId = expectedCompactionID then
        Some
            { s with
                CompactionActiveId = ""
                CompactionActiveOrdinal = 0
                CompactionHumanTurnId = ""
                CompactionCancelGeneration = 0
                CompactionGeneration = 0
                CompactionCompacted = false
                CompactionContinuationObserved = false
                CompactionSummaryTransformPending = false
                Owner =
                    if s.Owner = SessionOwner.Compaction then
                        SessionOwner.NoOwner
                    else
                        s.Owner }
    else
        None

let applySettleReturning expectedCompactionID (s: FallbackSessionRuntime) : FallbackSessionRuntime * bool =
    match applySettle expectedCompactionID s with
    | Some s' -> s', true
    | None -> s, false

let setCompacted value (s: FallbackSessionRuntime) = { s with CompactionCompacted = value }

let setCompactionContinuationObserved value (s: FallbackSessionRuntime) =
    { s with
        CompactionContinuationObserved = value }

let setCompactionGeneration gen (s: FallbackSessionRuntime) = { s with CompactionGeneration = gen }

let markForceStopped (s: FallbackSessionRuntime) =
    { s with CompactionForceStopped = true }

let removeForceStopped (s: FallbackSessionRuntime) =
    { s with
        CompactionForceStopped = false }

let setTaskComplete (s: FallbackSessionRuntime) =
    { s with
        Core =
            { s.Core with
                Lifecycle = FallbackLifecycle.TaskComplete } }

let tryConsumeCompactionSummaryTransform (s: FallbackSessionRuntime) =
    if s.CompactionSummaryTransformPending then
        Some
            { s with
                CompactionSummaryTransformPending = false }
    else
        None

let tryConsumeCompactionSummaryTransformReturning (s: FallbackSessionRuntime) : FallbackSessionRuntime * bool =
    match tryConsumeCompactionSummaryTransform s with
    | Some s' -> s', true
    | None -> s, false

let clearCompactionSummaryTransformPending (s: FallbackSessionRuntime) =
    { s with
        CompactionSummaryTransformPending = false }
