module Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeTransitions

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
    | Some lease when lease.NudgeID = expectedID ->
        if lease.Status = nextStatus then
            Some s
        elif
            lease.Status = expectedStatus
            && lease.SessionGeneration = s.SessionGeneration
            && lease.HumanTurnID = s.HumanTurnId
            && lease.CancelGeneration = s.CancelGeneration
            && lease.Owner = SessionOwner.Nudge
            && s.Owner = SessionOwner.Nudge
            && s.Core.Lifecycle = FallbackLifecycle.Active
        then
            Some
                { s with
                    PendingNudgeLease = Some { lease with Status = nextStatus } }
        else
            None
    | _ -> None

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

let setLastHumanMessageId messageId (s: FallbackSessionRuntime) =
    { s with
        LastHumanMessageId = messageId }

let clearLastHumanMessageId (s: FallbackSessionRuntime) = { s with LastHumanMessageId = "" }

let setActiveContinuationGeneration gen (s: FallbackSessionRuntime) = { s with ActiveContinuationGen = gen }

let setActiveContinuationCancelGeneration gen (s: FallbackSessionRuntime) =
    { s with
        ActiveContinuationCancelGen = gen }
