module Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.SessionRuntime

// ----- Core and lease transitions -----

let setCore state (s: FallbackSessionRuntime) =
    if state.Lifecycle = FallbackLifecycle.Cancelled then
        { cancelEpisode s with Core = state }
    else
        { s with Core = state }

let setPendingLease lease (s: FallbackSessionRuntime) = { s with PendingLease = Some lease }

let tryClearPendingLease expectedContinuationID (s: FallbackSessionRuntime) =
    match s.PendingLease with
    | Some lease when lease.ContinuationID = expectedContinuationID -> Some { s with PendingLease = None }
    | _ -> None

let clearPendingLease (s: FallbackSessionRuntime) = { s with PendingLease = None }

let tryTransitionPendingLease expectedID expectedStatus nextStatus (s: FallbackSessionRuntime) =
    match s.PendingLease with
    | Some lease ->
        let isCurrent =
            lease.ContinuationID = expectedID
            && lease.Status = expectedStatus
            && lease.SessionGeneration = s.SessionGeneration
            && lease.HumanTurnID = s.HumanTurnId
            && lease.CancelGeneration = s.CancelGeneration
            && lease.Owner = SessionOwner.Fallback
            && s.Owner = SessionOwner.Fallback
            && s.Core.Lifecycle = FallbackLifecycle.Active

        if isCurrent then
            Some
                { s with
                    PendingLease = Some { lease with Status = nextStatus } }
        else
            None
    | None -> None

let setPendingNudgeLease lease (s: FallbackSessionRuntime) =
    { s with
        PendingNudgeLease = Some lease }

let clearPendingNudgeLease (s: FallbackSessionRuntime) = { s with PendingNudgeLease = None }

let tryClearPendingNudgeLease expectedNudgeID (s: FallbackSessionRuntime) =
    match s.PendingNudgeLease with
    | Some lease when lease.NudgeID = expectedNudgeID -> Some { s with PendingNudgeLease = None }
    | _ -> None

let tryTransitionPendingNudgeLease expectedID expectedStatus nextStatus (s: FallbackSessionRuntime) =
    match s.PendingNudgeLease with
    | Some lease ->
        let isCurrent =
            lease.NudgeID = expectedID
            && lease.Status = expectedStatus
            && lease.SessionGeneration = s.SessionGeneration
            && lease.HumanTurnID = s.HumanTurnId
            && lease.CancelGeneration = s.CancelGeneration
            && lease.Owner = SessionOwner.Nudge
            && s.Owner = SessionOwner.Nudge
            && s.Core.Lifecycle = FallbackLifecycle.Active

        if isCurrent then
            Some
                { s with
                    PendingNudgeLease = Some { lease with Status = nextStatus } }
        else
            None
    | None -> None

let applyCancelNudgeLease expectedNudgeID (s: FallbackSessionRuntime) =
    match s.PendingNudgeLease with
    | Some lease when lease.NudgeID = expectedNudgeID ->
        Some
            { s with
                PendingNudgeLease = None
                ActiveNudgeNonce = ""
                ActiveGates = Set.remove FallbackSessionGateFlag.NudgeActive s.ActiveGates
                Owner =
                    if s.Owner = SessionOwner.Nudge then
                        SessionOwner.NoOwner
                    else
                        s.Owner }
    | _ -> None

let applyCancelNudgeLeaseReturning expectedNudgeID (s: FallbackSessionRuntime) : FallbackSessionRuntime * bool =
    match applyCancelNudgeLease expectedNudgeID s with
    | Some s' -> s', true
    | None -> s, false

let tryClearPendingLeaseReturning expectedContinuationID (s: FallbackSessionRuntime) : FallbackSessionRuntime * bool =
    match tryClearPendingLease expectedContinuationID s with
    | Some s' -> s', true
    | None -> s, false

let tryTransitionPendingLeaseReturning
    expectedID
    expectedStatus
    nextStatus
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime * bool =
    match tryTransitionPendingLease expectedID expectedStatus nextStatus s with
    | Some s' -> s', true
    | None -> s, false

let tryClearPendingNudgeLeaseReturning expectedNudgeID (s: FallbackSessionRuntime) : FallbackSessionRuntime * bool =
    match tryClearPendingNudgeLease expectedNudgeID s with
    | Some s' -> s', true
    | None -> s, false

let tryTransitionPendingNudgeLeaseReturning
    expectedID
    expectedStatus
    nextStatus
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime * bool =
    match tryTransitionPendingNudgeLease expectedID expectedStatus nextStatus s with
    | Some s' -> s', true
    | None -> s, false

// ----- Compaction transitions -----

let setLastHumanMessageId messageId (s: FallbackSessionRuntime) =
    { s with
        LastHumanMessageId = messageId }

let clearLastHumanMessageId (s: FallbackSessionRuntime) = { s with LastHumanMessageId = "" }

let setActiveContinuationGeneration gen (s: FallbackSessionRuntime) = { s with ActiveContinuationGen = gen }

let setActiveContinuationCancelGeneration gen (s: FallbackSessionRuntime) =
    { s with
        ActiveContinuationCancelGen = gen }

let setActiveCompactionId id ordinal humanTurnId cancelGeneration (s: FallbackSessionRuntime) =
    { s with
        CompactionActiveId = id
        CompactionActiveOrdinal = ordinal
        CompactionHumanTurnId = humanTurnId
        CompactionCancelGeneration = cancelGeneration }

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
