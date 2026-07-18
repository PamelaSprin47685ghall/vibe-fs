module Wanxiangshu.Runtime.Fallback.LeaseValidation

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Fallback.LeaseValidationRules

let verifyLeaseWithStatus = LeaseValidationRules.verifyLeaseWithStatus
let verifyLease = LeaseValidationRules.verifyLease
let ensureActiveAndOwner = LeaseValidationRules.ensureActiveAndOwner
let checkIsStale = LeaseValidationRules.checkIsStale
let checkContinuationMatches = LeaseValidationRules.checkContinuationMatches
let isTerminalOrSettled = LeaseValidationRules.isTerminalOrSettled

let setupContinuationLease
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (model: FallbackModel)
    (promptTextOpt: string option)
    : PendingLease =
    // Atomically set owner, gate, generations, and create the pending lease.
    runtime.Update(sessionID, startDispatch model promptTextOpt)
    let pending = runtime.GetSession sessionID

    match pending.PendingLease with
    | Some lease -> lease
    | None -> failwith "Invariant violated: startDispatch must set PendingLease"

let cancelPendingMainLease
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (reason: string)
    : JS.Promise<unit> =
    promise {
        match runtime.TryGetPendingLease sessionID with
        | Some lease ->
            do!
                appendContinuationCancelledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    reason
                    lease.ContinuationOrdinal

            let cleared = runtime.TryClearPendingLease(sessionID, lease.ContinuationID)

            if cleared then
                if runtime.GetSessionOwner sessionID = SessionOwner.Fallback then
                    runtime.SetSessionOwner sessionID SessionOwner.NoOwner

                runtime.Update(sessionID, setMainContinuationAwaitingStart false)
        | None -> ()
    }

let appendOutcomeIfNeeded
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (outcome: ContinuationOutcome)
    (errorOrReason: string)
    : JS.Promise<unit> =
    promise {
        match outcome with
        | ContinuationOutcome.Failed ->
            do!
                appendContinuationFailedOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    errorOrReason
                    lease.ContinuationOrdinal
        | ContinuationOutcome.Cancelled ->
            do!
                appendContinuationCancelledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    errorOrReason
                    lease.ContinuationOrdinal
        | ContinuationOutcome.Settled ->
            do!
                appendContinuationSettledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    lease.HumanTurnID
                    lease.SessionGeneration
                    errorOrReason
                    lease.ContinuationOrdinal
    }

let finishContinuation
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (outcome: ContinuationOutcome)
    (errorOrReason: string)
    : JS.Promise<unit> =
    promise {
        let isLeaseStillActive =
            match runtime.TryGetPendingLease sessionID with
            | Some pending when pending.ContinuationID = lease.ContinuationID -> true
            | _ -> false

        if isLeaseStillActive then
            do! appendOutcomeIfNeeded workspaceRoot sessionID lease outcome errorOrReason

        let cleared = runtime.TryClearPendingLease(sessionID, lease.ContinuationID)

        if cleared then
            if runtime.GetSessionOwner sessionID = SessionOwner.Fallback then
                runtime.SetSessionOwner sessionID SessionOwner.NoOwner

            runtime.Update(sessionID, setMainContinuationAwaitingStart false)

        runtime.UpdateState sessionID (runtime.GetOrCreateState sessionID)
    }
