module Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor

/// Shared per-process retry dispatch governor.
let private retryGovernor = RetryDispatchGovernor()

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
        let isValid =
            verifyLeaseWithStatus LeaseStatus.DispatchStarted runtime sessionID lease

        if not isValid then
            do! cancelAfterDispatch runtime executor workspaceRoot sessionID lease "Cancelled after dispatch"
        else
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
                runtime.TryTransitionPendingLease(
                    sessionID,
                    lease.ContinuationID,
                    LeaseStatus.DispatchStarted,
                    LeaseStatus.Dispatched
                )

            if not canTransition then
                do! cancelAfterDispatch runtime executor workspaceRoot sessionID lease "Cancelled after dispatch"
            else
                runtime.UpdateSession(sessionID, setInjected model atMs)
    }

/// Inner dispatch: write dispatch_started, transition lease, call action.
let dispatchWithLeaseTransition
    (runtime: FallbackRuntimeStore)
    (executor: IActionExecutor)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (model: FallbackModel)
    (agent: string)
    (dispatchAction: unit -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        do!
            appendContinuationDispatchStartedOrFail
                workspaceRoot
                sessionID
                lease.ContinuationID
                lease.ContinuationOrdinal

        let isValid =
            runtime.TryTransitionPendingLease(
                sessionID,
                lease.ContinuationID,
                LeaseStatus.Requested,
                LeaseStatus.DispatchStarted
            )

        if not isValid then
            do! cancelAfterDispatch runtime executor workspaceRoot sessionID lease "Lease invalid at dispatch"
        else
            do! dispatchAction ()
            do! handleDispatchComplete runtime executor workspaceRoot sessionID lease model agent
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
    : JS.Promise<unit> =
    promise {
        let modelKey =
            RetryModelKey.Create(model.ProviderID, model.ModelID, ?variant = model.Variant)

        let stillValid () =
            verifyLease runtime sessionID lease
            && ensureActiveAndOwner runtime sessionID lease

        let dispatchWithLease () =
            dispatchWithLeaseTransition runtime executor workspaceRoot sessionID lease model agent dispatchAction

        try
            let! dispatchResult = retryGovernor.RunWhenAllowed(modelKey, stillValid, dispatchWithLease)

            match dispatchResult with
            | Dispatched -> ()
            | CancelledBeforeDispatch ->
                do!
                    finishContinuation
                        runtime
                        workspaceRoot
                        sessionID
                        lease
                        ContinuationOutcome.Cancelled
                        "Cancelled before dispatch (rate-limited)"
        with ex ->
            do! finishContinuation runtime workspaceRoot sessionID lease ContinuationOutcome.Failed ex.Message
    }
