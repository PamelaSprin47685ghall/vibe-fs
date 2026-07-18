module Wanxiangshu.Hosts.Omp.NudgeDispatchLogic.LeaseHelper

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Runtime.NudgeDispatchClaim
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Hosts.Omp.NudgeRuntime
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Runtime.Fallback.GateFlagTransitions
open Wanxiangshu.Runtime.Fallback.HumanTurnTransitions
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.RuntimeScope

let makeNudgeLease
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

let registerLeaseAndMaybeDispatch
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
