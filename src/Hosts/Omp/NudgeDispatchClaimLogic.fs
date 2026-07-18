module Wanxiangshu.Hosts.Omp.NudgeDispatchClaimLogic

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.NudgeDispatchClaim
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Hosts.Omp.NudgeReminderDispatch
open Wanxiangshu.Runtime.ProjectionCache
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions

let now () = System.DateTime.UtcNow

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

let private makeNudgeLease
    (nudgeId: string)
    (nudgeOrdinal: int)
    (nonce: string)
    (humanTurnId: string)
    (sessionGen: int)
    (cancelGen: int)
    : NudgeLease =
    { NudgeID = nudgeId
      NudgeOrdinal = nudgeOrdinal
      Nonce = nonce
      HumanTurnID = humanTurnId
      SessionGeneration = sessionGen
      CancelGeneration = cancelGen
      Owner = SessionOwner.Nudge
      Status = LeaseStatus.DispatchStarted }

let private finalizeDispatchedLease
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

let private attemptTransitionThenFinalize
    (fallbackRuntime: FallbackRuntimeStore)
    (root: string)
    (sessionId: string)
    (lease: NudgeLease)
    (action: NudgeAction)
    (snapshot: SessionSnapshot)
    : JS.Promise<unit> =
    if
        not (
            fallbackRuntime.TryTransitionPendingNudgeLease(
                sessionId,
                lease.NudgeID,
                LeaseStatus.DispatchStarted,
                LeaseStatus.Dispatched
            )
        )
    then
        finishNudge fallbackRuntime root sessionId lease NudgeOutcome.Cancelled "Cancelled after dispatch" "" ""
    else
        finalizeDispatchedLease fallbackRuntime root sessionId lease action snapshot

let performNudgeDispatch
    (pi: IPi)
    (fallbackRuntime: FallbackRuntimeStore)
    (root: string)
    (sessionId: string)
    (action: NudgeAction)
    (snapshot: SessionSnapshot)
    (lease: NudgeLease)
    : JS.Promise<unit> =
    promise {
        try
            do! sendNudgeReminder pi action snapshot
            do! attemptTransitionThenFinalize fallbackRuntime root sessionId lease action snapshot
        with _ ->
            do! finishNudge fallbackRuntime root sessionId lease NudgeOutcome.Failed "Send failed" "" ""
    }

let private registerLeaseAndMaybeDispatch
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionId: string)
    (lease: NudgeLease)
    (nonce: string)
    (pi: IPi)
    (root: string)
    (action: NudgeAction)
    (snapshot: SessionSnapshot)
    : JS.Promise<unit> =
    fallbackRuntime.SetPendingNudgeLease(sessionId, lease)
    fallbackRuntime.SetSessionOwner sessionId SessionOwner.Nudge
    fallbackRuntime.SetActiveNudgeNonce sessionId nonce
    fallbackRuntime.SetMainContinuationAwaitingStart sessionId true

    if isSessionForceStopped sessionId then
        finishNudge fallbackRuntime root sessionId lease NudgeOutcome.Cancelled "Force stopped" "" ""
    else
        performNudgeDispatch pi fallbackRuntime root sessionId action snapshot lease

let claimLeaseAndDispatch
    (pi: IPi)
    (fallbackRuntime: FallbackRuntimeStore)
    (sessionId: string)
    (root: string)
    (action: NudgeAction)
    (snapshot: SessionSnapshot)
    (nudgeId: string)
    (nonce: string)
    (sessionGen: int)
    (cancelGen: int)
    (humanTurnId: string)
    (nudgeOrdinal: int)
    : JS.Promise<unit> =
    let lease =
        makeNudgeLease nudgeId nudgeOrdinal nonce humanTurnId sessionGen cancelGen

    registerLeaseAndMaybeDispatch fallbackRuntime sessionId lease nonce pi root action snapshot
