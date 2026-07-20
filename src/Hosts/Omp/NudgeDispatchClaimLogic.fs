module Wanxiangshu.Hosts.Omp.NudgeDispatchClaimLogic

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Runtime.EventLogRuntimeNudge
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Hosts.Omp.NudgeReminderDispatch
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

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
    tryClaimNudgeDispatch
        root
        sessionId
        action
        nudgeAnchorKey
        nudgeId
        nonce
        sessionGen
        cancelGen
        humanTurnId
        nudgeOrdinal

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
      HostUserMessageId = ""
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
            fallbackRuntime.UpdateSessionReturning(
                sessionId,
                tryTransitionPendingNudgeLeaseReturning lease.NudgeID LeaseStatus.DispatchStarted LeaseStatus.Dispatched
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
    fallbackRuntime.UpdateSession(sessionId, setPendingNudgeLease lease)
    fallbackRuntime.UpdateSession(sessionId, transferOwnership SessionOwner.Nudge)
    fallbackRuntime.UpdateSession(sessionId, armNudgeNonce nonce)
    fallbackRuntime.Update(sessionId, setMainContinuationAwaitingStart true)

    if isSessionForceStopped fallbackRuntime sessionId then
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
