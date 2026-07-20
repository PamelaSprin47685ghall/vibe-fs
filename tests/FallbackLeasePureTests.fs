module Wanxiangshu.Tests.FallbackLeasePureTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeCompactionPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseValidation

let private dummyModel =
    { ProviderID = "p"
      ModelID = "m"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private mkNudgeLease id status =
    { NudgeID = id
      NudgeOrdinal = 1
      Nonce = "n"
      HumanTurnID = ""
      HostUserMessageId = ""
      SessionGeneration = 0
      CancelGeneration = 0
      Owner = SessionOwner.Nudge
      Status = status }

let private withNudgeLease id status s =
    s
    |> transferOwnership SessionOwner.Nudge
    |> setPendingNudgeLease (mkNudgeLease id status)

let private cidOf s =
    (Option.get s.PendingLease).ContinuationID

// --- tryTransitionPendingLease ---

let tryTransitionPendingLeaseAllMatch () =
    let s = freshSessionState |> startDispatch dummyModel None

    tryTransitionPendingLease (cidOf s) LeaseStatus.Requested LeaseStatus.DispatchStarted s
    |> isSome

let tryTransitionPendingLeaseGenerationMismatch () =
    let s =
        freshSessionState |> startDispatch dummyModel None |> setSessionGeneration 99

    tryTransitionPendingLease (cidOf s) LeaseStatus.Requested LeaseStatus.DispatchStarted s
    |> isNone

let tryTransitionPendingLeaseStatusMismatch () =
    let s = freshSessionState |> startDispatch dummyModel None

    tryTransitionPendingLease (cidOf s) LeaseStatus.DispatchStarted LeaseStatus.Dispatched s
    |> isNone

let tryTransitionPendingLeaseOwnerNotFallback () =
    let s =
        freshSessionState
        |> startDispatch dummyModel None
        |> transferOwnership SessionOwner.NoOwner

    tryTransitionPendingLease (cidOf s) LeaseStatus.Requested LeaseStatus.DispatchStarted s
    |> isNone

let tryTransitionPendingLeaseNoLease () =
    tryTransitionPendingLease "x" LeaseStatus.Requested LeaseStatus.DispatchStarted freshSessionState
    |> isNone

let tryTransitionPendingLeaseReturningBool () =
    let s = freshSessionState |> startDispatch dummyModel None

    let _, flag =
        tryTransitionPendingLeaseReturning (cidOf s) LeaseStatus.Requested LeaseStatus.DispatchStarted s

    check "returning true on match" flag

let trySetupContinuationLeasePreservesActiveLease () =
    let runtime = FallbackRuntimeStore()
    let sessionID = "active-continuation"
    let first = trySetupContinuationLease runtime sessionID dummyModel None |> Option.get

    let replacement =
        { dummyModel with
            ProviderID = "replacement"
            ModelID = "other" }

    let second = trySetupContinuationLease runtime sessionID replacement (Some "must-not-replace")
    let active = (runtime.GetSession sessionID).PendingLease |> Option.get

    equal "second continuation is rejected" None second
    equal "active continuation id is preserved" first.ContinuationID active.ContinuationID
    equal "active continuation model is preserved" dummyModel active.Model
    equal "active continuation prompt is preserved" None active.PromptText

// --- tryTransitionPendingNudgeLease ---

let tryTransitionPendingNudgeLeaseIdempotent () =
    let s = freshSessionState |> withNudgeLease "n1" LeaseStatus.Dispatched

    tryTransitionPendingNudgeLease "n1" LeaseStatus.Requested LeaseStatus.Dispatched s
    |> isSome

let tryTransitionPendingNudgeLeaseGenerationMismatch () =
    let s =
        freshSessionState
        |> withNudgeLease "n1" LeaseStatus.Requested
        |> setSessionGeneration 99

    tryTransitionPendingNudgeLease "n1" LeaseStatus.Requested LeaseStatus.DispatchStarted s
    |> isNone

let tryTransitionPendingNudgeLeaseNoLease () =
    tryTransitionPendingNudgeLease "x" LeaseStatus.Requested LeaseStatus.DispatchStarted freshSessionState
    |> isNone

// --- applyCancelNudgeLease ---

let applyCancelNudgeLeaseMatch () =
    let s =
        freshSessionState
        |> withNudgeLease "n1" LeaseStatus.Requested
        |> armNudgeNonce "n"
        |> setNudgeActive true

    let result = applyCancelNudgeLease "n1" s
    result |> isSome
    let s' = Option.get result
    equal "lease cleared" None s'.PendingNudgeLease
    equal "nonce cleared" "" s'.ActiveNudgeNonce
    equal "owner reset" SessionOwner.NoOwner s'.Owner
    check "gate removed" (not (Set.contains FallbackSessionGateFlag.NudgeActive s'.ActiveGates))

let applyCancelNudgeLeaseNoMatch () =
    let s = freshSessionState |> withNudgeLease "a" LeaseStatus.Requested
    applyCancelNudgeLease "b" s |> isNone

let applyCancelNudgeLeaseReturningBool () =
    let s = freshSessionState |> withNudgeLease "n1" LeaseStatus.Requested
    let _, okMatch = applyCancelNudgeLeaseReturning "n1" s
    let _, okMiss = applyCancelNudgeLeaseReturning "wrong" s
    check "match true" okMatch
    check "miss false" (not okMiss)

// --- applySettle / tryGetSettleInfo ---

let applySettleMatch () =
    let s = freshSessionState |> startCompaction "comp-1" 1 "" 0 1
    let result = applySettle "comp-1" s
    result |> isSome
    let s' = Option.get result
    equal "active id cleared" "" s'.CompactionActiveId
    equal "owner reset" SessionOwner.NoOwner s'.Owner

let applySettleNoMatch () =
    let s = freshSessionState |> startCompaction "comp-1" 1 "" 0 1
    applySettle "comp-2" s |> isNone

let tryGetSettleInfoMatch () =
    let s = freshSessionState |> startCompaction "comp-1" 1 "" 0 1
    let info = tryGetSettleInfo "comp-1" s
    isSome info
    equal "ordinal" (Some("comp-1", 1)) info

let tryGetSettleInfoNoMatch () =
    let s = freshSessionState |> startCompaction "comp-1" 1 "" 0 1
    tryGetSettleInfo "wrong" s |> isNone

// --- tryClearPendingLease ---

let tryClearPendingLeaseMatch () =
    let s = freshSessionState |> startDispatch dummyModel None
    let result = tryClearPendingLease (cidOf s) s
    result |> isSome
    equal "lease cleared" None (Option.get result).PendingLease

let tryClearPendingLeaseNoMatch () =
    let s = freshSessionState |> startDispatch dummyModel None
    tryClearPendingLease "wrong" s |> isNone

// --- compaction summary transform ---

let tryConsumeCompactionSummaryTransformPending () =
    let s = freshSessionState |> startCompaction "c" 1 "" 0 1
    let result = tryConsumeCompactionSummaryTransform s
    result |> isSome
    equal "cleared" false (Option.get result).CompactionSummaryTransformPending

let tryConsumeCompactionSummaryTransformNotPending () =
    tryConsumeCompactionSummaryTransform freshSessionState |> isNone

// --- lifecycle / force-stopped ---

let setTaskCompleteSetsLifecycle () =
    let s = freshSessionState |> setTaskComplete
    equal "lifecycle" FallbackLifecycle.TaskComplete s.Core.Lifecycle

let markForceStoppedThenRemove () =
    let s1 = freshSessionState |> markForceStopped
    check "stopped" s1.CompactionForceStopped
    let s2 = s1 |> removeForceStopped
    check "cleared" (not s2.CompactionForceStopped)

let run () =
    tryTransitionPendingLeaseAllMatch ()
    tryTransitionPendingLeaseGenerationMismatch ()
    tryTransitionPendingLeaseStatusMismatch ()
    tryTransitionPendingLeaseOwnerNotFallback ()
    tryTransitionPendingLeaseNoLease ()
    tryTransitionPendingLeaseReturningBool ()
    trySetupContinuationLeasePreservesActiveLease ()
    tryTransitionPendingNudgeLeaseIdempotent ()
    tryTransitionPendingNudgeLeaseGenerationMismatch ()
    tryTransitionPendingNudgeLeaseNoLease ()
    applyCancelNudgeLeaseMatch ()
    applyCancelNudgeLeaseNoMatch ()
    applyCancelNudgeLeaseReturningBool ()
    applySettleMatch ()
    applySettleNoMatch ()
    tryGetSettleInfoMatch ()
    tryGetSettleInfoNoMatch ()
    tryClearPendingLeaseMatch ()
    tryClearPendingLeaseNoMatch ()
    tryConsumeCompactionSummaryTransformPending ()
    tryConsumeCompactionSummaryTransformNotPending ()
    setTaskCompleteSetsLifecycle ()
    markForceStoppedThenRemove ()
