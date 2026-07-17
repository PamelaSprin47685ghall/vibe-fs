module Wanxiangshu.Runtime.Fallback.LeaseValidation

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.Fallback.HumanTurnTransitions
open Wanxiangshu.Runtime.Fallback.OrdinalTransitions
open Wanxiangshu.Runtime.Fallback.CompactionTransitions
open Wanxiangshu.Runtime.Fallback.GateFlagTransitions
open Wanxiangshu.Runtime.ContinuationEventWriter

let verifyLeaseWithStatus
    (expectedStatus: LeaseStatus)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (lease: PendingLease)
    : bool =
    let stateOpt = runtime.TryGetState sessionID
    let pending = runtime.TryGetPendingLease sessionID

    lease.SessionGeneration = runtime.GetSessionGeneration sessionID
    && lease.HumanTurnID = runtime.GetHumanTurnId sessionID
    && lease.CancelGeneration = runtime.GetCancelGeneration sessionID
    && lease.Owner = SessionOwner.Fallback
    && runtime.GetSessionOwner sessionID = SessionOwner.Fallback
    && not (runtime.IsForceStopped sessionID)
    && runtime.GetActiveCompactionId sessionID = ""
    && not (runtime.IsCompacted sessionID)
    && (match stateOpt with
        | Some s -> s.Lifecycle = FallbackLifecycle.Active
        | None -> false)
    && (match pending with
        | Some p -> p.ContinuationID = lease.ContinuationID && p.Status = expectedStatus
        | None -> false)

let verifyLease (runtime: FallbackRuntimeStore) (sessionID: string) (lease: PendingLease) : bool =
    verifyLeaseWithStatus LeaseStatus.Requested runtime sessionID lease

let ensureActiveAndOwner (runtime: FallbackRuntimeStore) (sessionID: string) (lease: PendingLease) : bool =
    let state = runtime.GetOrCreateState sessionID

    state.Lifecycle = FallbackLifecycle.Active
    && runtime.GetSessionOwner sessionID = SessionOwner.Fallback
    && lease.Owner = SessionOwner.Fallback
    && not (runtime.IsForceStopped sessionID)
    && runtime.GetActiveCompactionId sessionID = ""
    && not (runtime.IsCompacted sessionID)
    && runtime.GetHumanTurnId sessionID = lease.HumanTurnID
    && runtime.GetSessionGeneration sessionID = lease.SessionGeneration
    && runtime.GetCancelGeneration sessionID = lease.CancelGeneration

let checkIsStale
    (isEventContIdMatch: bool)
    (eventOpt: FallbackEvent option)
    (eventTurnIdOpt: string option)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    : bool =
    match eventOpt with
    | None -> false
    | Some evt ->
        if not isEventContIdMatch then
            true
        elif evt = FallbackEvent.NewUserMessage then
            false
        else
            let isAbortError =
                match evt with
                | FallbackEvent.SessionError err -> Wanxiangshu.Kernel.FallbackKernel.Decision.errorInputIsAbort err
                | _ -> false

            if isAbortError then
                let eventTurnId = eventTurnIdOpt |> Option.defaultValue ""

                if eventTurnId <> "" && eventTurnId <> runtime.GetHumanTurnId sessionID then
                    true
                else
                    let activeGen = runtime.GetActiveContinuationGeneration sessionID
                    let activeCancel = runtime.GetActiveContinuationCancelGeneration sessionID

                    activeGen < runtime.GetSessionGeneration sessionID
                    || activeCancel < runtime.GetCancelGeneration sessionID
            else
                let state = runtime.GetOrCreateState sessionID

                state.Lifecycle = FallbackLifecycle.Cancelled
                || (let activeGen = runtime.GetActiveContinuationGeneration sessionID
                    let activeCancel = runtime.GetActiveContinuationCancelGeneration sessionID

                    activeGen < runtime.GetSessionGeneration sessionID
                    || activeCancel < runtime.GetCancelGeneration sessionID)

let checkContinuationMatches
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (continuationId: string)
    : bool * bool =
    let pending = runtime.TryGetPendingLease sessionID

    let activeMatch () =
        let activeGen = runtime.GetActiveContinuationGeneration sessionID
        let activeCancel = runtime.GetActiveContinuationCancelGeneration sessionID

        activeGen = runtime.GetSessionGeneration sessionID
        && activeCancel = runtime.GetCancelGeneration sessionID

    let isMatched =
        match pending with
        | Some lease ->
            if continuationId = "" then
                activeMatch ()
            else
                continuationId = lease.ContinuationID
        | None -> false

    let isContIdMatch =
        match pending with
        | Some lease ->
            if continuationId = "" then
                activeMatch ()
            else
                continuationId = lease.ContinuationID
        | None -> true

    isMatched, isContIdMatch

let isTerminalOrSettled
    (evt: FallbackEvent)
    (currentState: SessionFallbackState)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    : bool =
    let terminalSessionFallbackState =
        currentState.Lifecycle = FallbackLifecycle.Cancelled
        || currentState.Lifecycle = FallbackLifecycle.TaskComplete

    let settledFallbackLease =
        match runtime.TryGetPendingLease sessionID with
        | Some lease -> lease.Status = LeaseStatus.Settled || lease.Status = LeaseStatus.Cancelled
        | None -> false

    evt <> FallbackEvent.NewUserMessage
    && (terminalSessionFallbackState || settledFallbackLease)

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

                runtime.SetMainContinuationAwaitingStart sessionID false
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

            runtime.SetMainContinuationAwaitingStart sessionID false

        runtime.UpdateState sessionID (runtime.GetOrCreateState sessionID)
    }
