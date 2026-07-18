module Wanxiangshu.Hosts.Omp.NudgeDispatchLogic.ClaimHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Nudge.NudgeDispatchClaim
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.RuntimeScope

let tryClaimNudgeDispatch
    (root: string)
    (sessionId: string)
    (action: NudgeAction)
    (nudgeAnchorKey: string)
    (nudgeId: string)
    (nonce: string)
    (sessionGen: int)
    (cancelGen: int)
    (humanTurnId: string)
    (nudgeOrdinal: int)
    : JS.Promise<bool> =
    promise {
        let cache = ProjectionCache(root)
        use! _ = cache.OpenAsync()

        let! evt =
            tryClaim
                cache
                sessionId
                action
                nudgeAnchorKey
                nudgeId
                nonce
                sessionGen
                cancelGen
                humanTurnId
                nudgeOrdinal
                (fun isBlocked anchor ->
                    isBlocked
                        { PendingNudge = None
                          LastDispatchedAnchor = None }
                        anchor)
                (now ())

        return evt.IsSome
    }

let finalizeDispatchedLease
    (fallbackRuntime: FallbackRuntimeStore)
    (root: string)
    (sessionId: string)
    (lease: NudgeLease)
    (action: NudgeAction)
    (snapshot: SessionSnapshot)
    : JS.Promise<unit> =
    let dispatchedLease =
        { lease with
            Status = LeaseStatus.Dispatched }

    finishNudge
        fallbackRuntime
        root
        sessionId
        dispatchedLease
        NudgeOutcome.Dispatched
        ""
        (toString action)
        snapshot.nudgeAnchorKey

let handleTransitionFailure
    (fallbackRuntime: FallbackRuntimeStore)
    (root: string)
    (sessionId: string)
    (lease: NudgeLease)
    : JS.Promise<unit> =
    finishNudge fallbackRuntime root sessionId lease NudgeOutcome.Cancelled "Cancelled after dispatch" "" ""

let attemptTransitionThenFinalize
    (fallbackRuntime: FallbackRuntimeStore)
    (root: string)
    (sessionId: string)
    (lease: NudgeLease)
    (action: NudgeAction)
    (snapshot: SessionSnapshot)
    : JS.Promise<unit> =
    if
        not (
            fallbackRuntime.UpdateSessionReturning(
                sessionId,
                tryTransitionPendingNudgeLeaseReturning lease.NudgeID LeaseStatus.DispatchStarted LeaseStatus.Dispatched
            )
        )
    then
        finishNudge fallbackRuntime root sessionId lease NudgeOutcome.Cancelled "Cancelled after dispatch" "" ""
    else
        finalizeDispatchedLease fallbackRuntime root sessionId lease action snapshot
