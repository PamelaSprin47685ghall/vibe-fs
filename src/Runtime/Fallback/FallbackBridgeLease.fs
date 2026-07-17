// ARCHITECTURE_EXEMPT: 313-line file, needs splitting
module Wanxiangshu.Runtime.Fallback.FallbackBridgeLease

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.GateTransitions
open Wanxiangshu.Runtime.Fallback.ModelInjection
open Wanxiangshu.Runtime.Fallback.FallbackBridgePorts
open Wanxiangshu.Runtime.EventLogAppendSession
open Wanxiangshu.Runtime.EventLogAppendReview
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.EventLogRuntimeAppend

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

    let isMatched =
        match pending with
        | Some lease -> continuationId = lease.ContinuationID
        | None -> false

    let isContIdMatch =
        match pending with
        | Some lease -> continuationId = lease.ContinuationID
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

let updateBusyLeases (runtime: FallbackRuntimeStore) (sessionID: string) : unit =
    let markRunning (lease: PendingLease) =
        { lease with
            Status = LeaseStatus.Running }

    runtime.TryGetPendingLease sessionID
    |> Option.filter (fun l -> l.Status = LeaseStatus.DispatchStarted || l.Status = LeaseStatus.Dispatched)
    |> Option.iter (fun l -> runtime.SetPendingLease(sessionID, { l with Status = LeaseStatus.Running }))

    runtime.TryGetPendingNudgeLease sessionID
    |> Option.filter (fun l -> l.Status = LeaseStatus.DispatchStarted || l.Status = LeaseStatus.Dispatched)
    |> Option.iter (fun l -> runtime.SetPendingNudgeLease(sessionID, { l with Status = LeaseStatus.Running }))

let handleTerminalPostSettlement
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (evt: FallbackEvent)
    (finalState2: SessionFallbackState)
    (intentOpt: 'Intent option)
    : JS.Promise<unit> =
    promise {
        let isPostTerminal =
            evt <> FallbackEvent.SessionBusy
            && (finalState2.Lifecycle = FallbackLifecycle.TaskComplete
                || finalState2.Lifecycle = FallbackLifecycle.Cancelled
                || finalState2.Phase = FallbackPhase.Exhausted
                || (finalState2.Phase = FallbackPhase.Idle && intentOpt.IsNone))

        if isPostTerminal then
            match runtime.TryGetPendingLease sessionID with
            | Some lease ->
                if lease.Status <> LeaseStatus.Cancelled then
                    do!
                        appendContinuationSettledOrFail
                            workspaceRoot
                            sessionID
                            lease.ContinuationID
                            lease.HumanTurnID
                            lease.SessionGeneration
                            "completed"
                            lease.ContinuationOrdinal

                if runtime.TryClearPendingLease(sessionID, lease.ContinuationID) then
                    if runtime.GetSessionOwner sessionID = SessionOwner.Fallback then
                        runtime.SetSessionOwner sessionID SessionOwner.NoOwner

                    runtime.SetMainContinuationAwaitingStart sessionID false
            | None -> ()
    }

let calculateConsumed (evt: FallbackEvent) (statePhase: FallbackPhase) (finalPhase: FallbackPhase) : bool =
    match evt with
    | FallbackEvent.SessionError _ -> finalPhase <> FallbackPhase.Exhausted
    | FallbackEvent.SessionIdle ->
        finalPhase = FallbackPhase.ScanningToolCallText
        || finalPhase = FallbackPhase.RecoveringToolCallText
    | FallbackEvent.SessionBusy ->
        match statePhase with
        | FallbackPhase.Retrying _
        | FallbackPhase.Scanning _ -> true
        | _ -> false
    | _ -> false

let handleUserAbort (runtime: FallbackRuntimeStore) (workspaceRoot: string) (sessionID: string) : JS.Promise<unit> =
    promise {
        do! appendUserAbortObservedOrFail workspaceRoot sessionID
        let _ = runtime.IncrementCancelGeneration sessionID

        match runtime.TryGetPendingLease sessionID with
        | Some lease ->
            do!
                appendContinuationCancelledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    "User aborted"
                    lease.ContinuationOrdinal

            let cleared = runtime.TryClearPendingLease(sessionID, lease.ContinuationID)

            if cleared then
                if runtime.GetSessionOwner sessionID = SessionOwner.Fallback then
                    runtime.SetSessionOwner sessionID SessionOwner.NoOwner

                runtime.SetMainContinuationAwaitingStart sessionID false
        | None -> ()

        match runtime.TryGetPendingNudgeLease sessionID with
        | Some nudgeLease ->
            do!
                appendNudgeCancelledOrFail
                    workspaceRoot
                    sessionID
                    nudgeLease.NudgeID
                    "User aborted"
                    nudgeLease.NudgeOrdinal

            let _ = runtime.ApplyCancelNudgeLease(sessionID, nudgeLease.NudgeID)
            ()
        | None -> ()
    }

let cancelPendingLeasesAndNudges
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    : JS.Promise<unit> =
    promise {
        match runtime.TryGetPendingLease sessionID with
        | Some lease ->
            do!
                appendContinuationCancelledOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    "New user message"
                    lease.ContinuationOrdinal

            let cleared = runtime.TryClearPendingLease(sessionID, lease.ContinuationID)

            if cleared then
                if runtime.GetSessionOwner sessionID = SessionOwner.Fallback then
                    runtime.SetSessionOwner sessionID SessionOwner.NoOwner

                runtime.SetMainContinuationAwaitingStart sessionID false
        | None -> ()

        match runtime.TryGetPendingNudgeLease sessionID with
        | Some nudgeLease ->
            do!
                appendNudgeCancelledOrFail
                    workspaceRoot
                    sessionID
                    nudgeLease.NudgeID
                    "New user message"
                    nudgeLease.NudgeOrdinal

            let _ = runtime.ApplyCancelNudgeLease(sessionID, nudgeLease.NudgeID)
            ()
        | None -> ()
    }

let resolveChain
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (cfg: FallbackConfig)
    (sessionID: string)
    (agentName: string)
    : JS.Promise<FallbackModel list> =
    promise {
        let existing = runtime.GetChain sessionID

        if not (List.isEmpty existing) then
            return existing
        else
            let! currentModel = executor.CaptureCurrentModel sessionID

            let resolved =
                Map.tryFind (normalizeAgentName agentName) cfg.AgentChains
                |> Option.defaultValue cfg.DefaultChain

            let finalChain =
                match currentModel with
                | Some c ->
                    match resolved with
                    | f :: _ when f.ProviderID = c.ProviderID && f.ModelID = c.ModelID -> resolved
                    | _ ->
                        c
                        :: (resolved
                            |> List.filter (fun m -> m.ProviderID <> c.ProviderID || m.ModelID <> c.ModelID))
                | None -> resolved

            if not (List.isEmpty finalChain) then
                runtime.SetChain sessionID finalChain

            return finalChain
    }
