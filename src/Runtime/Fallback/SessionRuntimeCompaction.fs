module Wanxiangshu.Runtime.Fallback.SessionRuntimeCompaction

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntime

/// A compaction run is starting — claim ownership, track its identity, and arm the summary-transform bypass.
let beginCompaction
    (compactionId: string)
    (compactionOrdinal: int)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime =
    { s with
        CompactionActiveId = compactionId
        CompactionActiveOrdinal = compactionOrdinal
        CompactionHumanTurnId = s.HumanTurnId
        CompactionCancelGeneration = s.CancelGeneration
        CompactionGeneration = s.CompactionGeneration + 1
        CompactionSummaryTransformPending = true
        Owner = SessionOwner.Compaction }

/// A compaction run has settled — release ownership if compaction owned the session.
let settleCompaction (s: FallbackSessionRuntime) : FallbackSessionRuntime =
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
