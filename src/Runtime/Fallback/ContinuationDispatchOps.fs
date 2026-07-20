module Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor
open Wanxiangshu.Runtime.Fallback.ContinuationSessionReenter

/// Shared per-process retry dispatch governor.
let private retryGovernor = RetryDispatchGovernor()

type SessionReenter = ContinuationSessionReenter.SessionReenter

let inlineReenter = ContinuationSessionReenter.inlineReenter
let queueReenter = ContinuationSessionReenter.queueReenter

/// Cancel a dispatch after it has been started.
let private cancelAfterDispatch
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (reason: string)
    : JS.Promise<unit> =
    promise {
        let isActiveLease =
            match (runtime.GetSession sessionID).PendingLease with
            | Some pending when
                pending.ContinuationID = lease.ContinuationID
                && pending.Status <> LeaseStatus.Settled
                && pending.Status <> LeaseStatus.Cancelled
                ->
                (runtime.GetSession sessionID).Core.Lifecycle = FallbackLifecycle.Active
            | _ -> false

        if isActiveLease then
            do! executor.AbortRun sessionID
            do! finishContinuation runtime workspaceRoot sessionID lease ContinuationOutcome.Cancelled reason
    }

/// Handle post-dispatch completion: validate lease, write event, update state.
let handleDispatchComplete
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    : JS.Promise<unit> =
    promise {
        let pending = (runtime.GetSession sessionID).PendingLease
        let lifecycle = (runtime.GetSession sessionID).Core.Lifecycle

        match pending, lifecycle with
        | Some current, FallbackLifecycle.Active when current.ContinuationID = lease.ContinuationID ->
            let modelStr =
                match model.Variant with
                | Some v -> $"{model.ProviderID}/{model.ModelID}:{v}"
                | None -> $"{model.ProviderID}/{model.ModelID}"

            let atMs = getTimestampMs ()

            do!
                appendContinuationDispatchedOrFail
                    workspaceRoot
                    sessionID
                    lease.ContinuationID
                    modelStr
                    agent
                    atMs
                    lease.ContinuationOrdinal

            let canTransition =
                match current.Status with
                | LeaseStatus.Requested ->
                    runtime.UpdateSessionReturning(
                        sessionID,
                        tryTransitionPendingLeaseReturning
                            lease.ContinuationID
                            LeaseStatus.Requested
                            LeaseStatus.Dispatched
                    )
                | LeaseStatus.DispatchStarted ->
                    runtime.UpdateSessionReturning(
                        sessionID,
                        tryTransitionPendingLeaseReturning
                            lease.ContinuationID
                            LeaseStatus.DispatchStarted
                            LeaseStatus.Dispatched
                    )
                | LeaseStatus.Running ->
                    runtime.UpdateSessionReturning(
                        sessionID,
                        tryTransitionPendingLeaseReturning
                            lease.ContinuationID
                            LeaseStatus.Running
                            LeaseStatus.Dispatched
                    )
                | LeaseStatus.Dispatched -> true
                | LeaseStatus.Cancelled
                | LeaseStatus.Settled -> false

            if canTransition then
                runtime.Update(sessionID, setInjected model atMs)
        | _ ->
            // Terminal handling or a newer lease won the race. The prompt
            // completion is stale evidence; never append a late cancellation.
            ()
    }

let private leaseStillDispatchable
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (lease: PendingLease)
    : bool =
    let pending = (runtime.GetSession sessionID).PendingLease

    ensureActiveAndOwner runtime sessionID lease
    && (match pending with
        | Some p when p.ContinuationID = lease.ContinuationID ->
            match p.Status with
            | LeaseStatus.Requested
            | LeaseStatus.DispatchStarted -> true
            | LeaseStatus.Dispatched
            | LeaseStatus.Running
            | LeaseStatus.Cancelled
            | LeaseStatus.Settled -> false
        | _ -> false)

let private claimDispatchStarted
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (reenter: SessionReenter)
    : JS.Promise<bool> =
    promise {
        let mutable ok = false

        do!
            reenter (fun () ->
                promise {
                    do!
                        appendContinuationDispatchStartedOrFail
                            workspaceRoot
                            sessionID
                            lease.ContinuationID
                            lease.ContinuationOrdinal

                    ok <-
                        runtime.UpdateSessionReturning(
                            sessionID,
                            tryTransitionPendingLeaseReturning
                                lease.ContinuationID
                                LeaseStatus.Requested
                                LeaseStatus.DispatchStarted
                        )
                })

        return ok
    }

/// Inner dispatch: claim on reenter queue, physical action outside, complete on reenter.
let dispatchWithLeaseTransition
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    (dispatchAction: unit -> JS.Promise<unit>)
    (reenter: SessionReenter)
    : JS.Promise<unit> =
    promise {
        let! claimed = claimDispatchStarted runtime workspaceRoot sessionID lease reenter

        if not claimed then
            do!
                reenter (fun () ->
                    cancelAfterDispatch runtime executor workspaceRoot sessionID lease "Lease invalid at dispatch")
        else
            try
                do! dispatchAction ()

                do!
                    reenter (fun () ->
                        handleDispatchComplete runtime executor workspaceRoot sessionID lease model agent)
            with ex ->
                do!
                    reenter (fun () ->
                        finishContinuation
                            runtime
                            workspaceRoot
                            sessionID
                            lease
                            ContinuationOutcome.Failed
                            ex.Message)
    }

/// Run a dispatch action under rate-limit governor for the given model key.
let runWithRetryGovernor
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    (dispatchAction: unit -> JS.Promise<unit>)
    (reenter: SessionReenter)
    : JS.Promise<unit> =
    promise {
        // Transport scheduler key = workspace × provider credential × model × variant.
        // Session single in-flight is the session actor's job, not the transport governor.
        let transportKey =
            ProviderModelTransportKey.Create(
                workspaceRoot,
                model.ProviderID,
                model.ModelID,
                ?variant = model.Variant
            )

        let stillValid () = leaseStillDispatchable runtime sessionID lease

        let dispatchWithLease () =
            dispatchWithLeaseTransition
                runtime
                executor
                workspaceRoot
                sessionID
                lease
                model
                agent
                dispatchAction
                reenter

        try
            let! dispatchResult = retryGovernor.RunWhenAllowed(transportKey, stillValid, dispatchWithLease)

            match dispatchResult with
            | Dispatched -> ()
            | CancelledBeforeDispatch ->
                do!
                    reenter (fun () ->
                        finishContinuation
                            runtime
                            workspaceRoot
                            sessionID
                            lease
                            ContinuationOutcome.Cancelled
                            "Cancelled before dispatch (rate-limited)")
        with ex ->
            do!
                reenter (fun () ->
                    finishContinuation runtime workspaceRoot sessionID lease ContinuationOutcome.Failed ex.Message)
    }
