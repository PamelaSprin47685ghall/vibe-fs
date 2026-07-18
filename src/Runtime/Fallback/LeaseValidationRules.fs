module Wanxiangshu.Runtime.Fallback.LeaseValidationRules

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.LeaseTransitions

let verifyLeaseWithStatus
    (expectedStatus: LeaseStatus)
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (lease: PendingLease)
    : bool =
    let stateOpt = runtime.TryGetState sessionID
    let pending = runtime.TryGetPendingLease sessionID

    lease.SessionGeneration = (runtime.GetSession sessionID).SessionGeneration
    && lease.HumanTurnID = (runtime.GetSession sessionID).HumanTurnId
    && lease.CancelGeneration = (runtime.GetSession sessionID).CancelGeneration
    && lease.Owner = SessionOwner.Fallback
    && (runtime.GetSession sessionID).Owner = SessionOwner.Fallback
    && not ((runtime.GetSession sessionID).CompactionForceStopped)
    && (runtime.GetSession sessionID).CompactionActiveId = ""
    && not ((runtime.GetSession sessionID).CompactionCompacted)
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
    && (runtime.GetSession sessionID).Owner = SessionOwner.Fallback
    && lease.Owner = SessionOwner.Fallback
    && not ((runtime.GetSession sessionID).CompactionForceStopped)
    && (runtime.GetSession sessionID).CompactionActiveId = ""
    && not ((runtime.GetSession sessionID).CompactionCompacted)
    && (runtime.GetSession sessionID).HumanTurnId = lease.HumanTurnID
    && (runtime.GetSession sessionID).SessionGeneration = lease.SessionGeneration
    && (runtime.GetSession sessionID).CancelGeneration = lease.CancelGeneration

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

                if eventTurnId <> "" && eventTurnId <> (runtime.GetSession sessionID).HumanTurnId then
                    true
                else
                    let activeGen = (runtime.GetSession sessionID).ActiveContinuationGen
                    let activeCancel = (runtime.GetSession sessionID).ActiveContinuationCancelGen

                    activeGen < (runtime.GetSession sessionID).SessionGeneration
                    || activeCancel < (runtime.GetSession sessionID).CancelGeneration
            else
                let state = runtime.GetOrCreateState sessionID

                state.Lifecycle = FallbackLifecycle.Cancelled
                || (let activeGen = (runtime.GetSession sessionID).ActiveContinuationGen
                    let activeCancel = (runtime.GetSession sessionID).ActiveContinuationCancelGen

                    activeGen < (runtime.GetSession sessionID).SessionGeneration
                    || activeCancel < (runtime.GetSession sessionID).CancelGeneration)

let checkContinuationMatches
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (continuationId: string)
    : bool * bool =
    let pending = runtime.TryGetPendingLease sessionID

    let activeMatch () =
        let activeGen = (runtime.GetSession sessionID).ActiveContinuationGen
        let activeCancel = (runtime.GetSession sessionID).ActiveContinuationCancelGen

        activeGen = (runtime.GetSession sessionID).SessionGeneration
        && activeCancel = (runtime.GetSession sessionID).CancelGeneration

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
