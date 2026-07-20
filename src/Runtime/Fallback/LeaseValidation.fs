module Wanxiangshu.Runtime.Fallback.LeaseValidation

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Fallback.LeaseValidationRules

let verifyLeaseWithStatus = LeaseValidationRules.verifyLeaseWithStatus
let verifyLease = LeaseValidationRules.verifyLease
let ensureActiveAndOwner = LeaseValidationRules.ensureActiveAndOwner
let checkIsStale = LeaseValidationRules.checkIsStale
let checkContinuationMatches = LeaseValidationRules.checkContinuationMatches
let checkContinuationMatchesWithEvidence = LeaseValidationRules.checkContinuationMatchesWithEvidence
let isTerminalOrSettled = LeaseValidationRules.isTerminalOrSettled

let isContinuationLeaseActive (state: FallbackSessionRuntime) : bool =
    match state.PendingLease with
    | Some lease ->
        match lease.Status with
        | LeaseStatus.Requested
        | LeaseStatus.DispatchStarted
        | LeaseStatus.Dispatched
        | LeaseStatus.Running -> true
        | LeaseStatus.Cancelled
        | LeaseStatus.Settled -> false
    | None -> false

let tryReserveContinuationLease
    (state: FallbackSessionRuntime)
    (model: FallbackModel)
    (promptTextOpt: string option)
    : (FallbackSessionRuntime * PendingLease) option =
    if isContinuationLeaseActive state then
        None
    else
        let next = startDispatch model promptTextOpt state
        match next.PendingLease with
        | Some lease -> Some(next, lease)
        | None -> failwith "Invariant violated: startDispatch must set PendingLease"

let commitContinuationLease
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (expectedState: FallbackSessionRuntime)
    (nextState: FallbackSessionRuntime)
    (customCoreOpt: SessionFallbackState option)
    : bool =
    runtime.UpdateSessionReturning(sessionID, fun current ->
        let matches =
            current.PendingLease = expectedState.PendingLease
            && current.Owner = expectedState.Owner
            && current.SessionGeneration = expectedState.SessionGeneration
            && current.CancelGeneration = expectedState.CancelGeneration
            && current.HumanTurnId = expectedState.HumanTurnId
            && current.ContinuationOrdinal = expectedState.ContinuationOrdinal
            && current.ActiveContinuationGen = expectedState.ActiveContinuationGen
            && current.ActiveContinuationCancelGen = expectedState.ActiveContinuationCancelGen
            && current.ActiveGates = expectedState.ActiveGates
        if matches then
            let committed =
                match customCoreOpt with
                | Some core -> { nextState with Core = core }
                | None -> nextState
            committed, true
        else
            current, false)

let trySetupContinuationLease
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (model: FallbackModel)
    (promptTextOpt: string option)
    : PendingLease option =
    let current = runtime.GetSession sessionID
    match tryReserveContinuationLease current model promptTextOpt with
    | Some (next, lease) ->
        commitContinuationLease runtime sessionID current next None |> ignore
        Some lease
    | None -> None

let setupContinuationLease
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (model: FallbackModel)
    (promptTextOpt: string option)
    : PendingLease =
    match trySetupContinuationLease runtime sessionID model promptTextOpt with
    | Some lease -> lease
    | None -> failwith "Continuation lease already active"

let cancelPendingMainLease
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (reason: string)
    : JS.Promise<unit> =
    promise {
        match (runtime.GetSession sessionID).PendingLease with
        | Some lease ->
            do!
                appendContinuationCancelledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    reason
                    lease.ContinuationOrdinal

            let cleared =
                runtime.UpdateSessionReturning(sessionID, tryClearPendingLeaseReturning lease.ContinuationID)

            if cleared then
                if (runtime.GetSession sessionID).Owner = SessionOwner.Fallback then
                    runtime.UpdateSession(sessionID, transferOwnership SessionOwner.NoOwner)

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
            match (runtime.GetSession sessionID).PendingLease with
            | Some pending when
                pending.ContinuationID = lease.ContinuationID
                && pending.Status <> LeaseStatus.Settled
                && pending.Status <> LeaseStatus.Cancelled
                ->
                true
            | _ -> false

        if isLeaseStillActive then
            do! appendOutcomeIfNeeded workspaceRoot sessionID lease outcome errorOrReason

        let cleared =
            runtime.UpdateSessionReturning(sessionID, tryClearPendingLeaseReturning lease.ContinuationID)

        if cleared then
            if (runtime.GetSession sessionID).Owner = SessionOwner.Fallback then
                runtime.UpdateSession(sessionID, transferOwnership SessionOwner.NoOwner)

            runtime.Update(sessionID, setMainContinuationAwaitingStart false)

        runtime.Update(sessionID, setCore (runtime.GetOrCreateState sessionID))
    }
