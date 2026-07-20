module Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor
open Wanxiangshu.Runtime.Fallback.ContinuationSessionReenter
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchComplete

/// Shared per-process retry dispatch governor.
let private retryGovernor = RetryDispatchGovernor()

/// Clear rate-limit / queue memory between isolated tests.
let resetRetryGovernorForTests () : unit = retryGovernor.Reset()

type SessionReenter = ContinuationSessionReenter.SessionReenter

let inlineReenter = ContinuationSessionReenter.inlineReenter
let queueReenter = ContinuationSessionReenter.queueReenter
let handleDispatchComplete = ContinuationDispatchComplete.handleDispatchComplete

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
        // Sync ownership gate after claim reenter yields: human cancel may have
        // cleared the lease on the actor queue between claim and this line.
        // No await between the check and starting dispatchAction, so JS cannot
        // interleave another actor turn before physical transport begins.
        elif not (leaseStillDispatchable runtime sessionID lease) then
            ()
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
        let modelKey =
            RetryModelKey.Create(workspaceRoot, sessionID, model.ProviderID, model.ModelID, ?variant = model.Variant)

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
            let! dispatchResult = retryGovernor.RunWhenAllowed(modelKey, stillValid, dispatchWithLease)

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
