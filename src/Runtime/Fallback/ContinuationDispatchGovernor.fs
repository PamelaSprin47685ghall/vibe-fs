module Wanxiangshu.Runtime.Fallback.ContinuationDispatchGovernor

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.LeaseValidation
open Wanxiangshu.Runtime.ContinuationEventWriter
open Wanxiangshu.Runtime.Fallback.RetryDispatchGovernor
open Wanxiangshu.Runtime.Fallback.ContinuationSessionReenter
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchComplete
open Wanxiangshu.Runtime.MuxLogicalReceipt
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchRegistry
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps

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
        elif not (leaseStillDispatchable runtime sessionID lease) then
            ()
        else
            do! dispatchAction ()
            do! handleTransportReturned runtime executor workspaceRoot sessionID lease model agent
    }

let handleDispatchException
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (lease: PendingLease)
    (ex: exn)
    : JS.Promise<unit> =
    promise {
        if isAcceptanceUnknownMessage ex.Message then
            do!
                finishContinuation
                    runtime
                    workspaceRoot
                    sessionID
                    lease
                    ContinuationOutcome.AcceptanceUnknown
                    ex.Message
        elif isAbortUnavailableMessage ex.Message then
            runtime.Update(sessionID, setAbortUnavailable true)
            do! finishContinuation runtime workspaceRoot sessionID lease ContinuationOutcome.AbortUnknown ex.Message
        else
            do! finishContinuation runtime workspaceRoot sessionID lease ContinuationOutcome.Failed ex.Message
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
        let transportKey =
            ProviderModelTransportKey.Create(workspaceRoot, model.ProviderID, model.ModelID, ?variant = model.Variant)

        let stillValid () =
            leaseStillDispatchable runtime sessionID lease

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
            | RetryDispatchResult.Dispatched -> ()
            | RetryDispatchResult.CancelledBeforeDispatch ->
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
            do! handleDispatchException runtime workspaceRoot sessionID lease ex
    }
