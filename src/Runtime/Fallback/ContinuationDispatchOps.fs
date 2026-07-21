module Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps

open Fable.Core
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

/// Shared per-process retry dispatch governor.
let retryGovernor = RetryDispatchGovernor()

/// Clear rate-limit / queue memory between isolated tests.
let resetRetryGovernorForTests () : unit = retryGovernor.Reset()

type SessionReenter = ContinuationSessionReenter.SessionReenter

let inlineReenter = ContinuationSessionReenter.inlineReenter
let queueReenter = ContinuationSessionReenter.queueReenter
let handleDispatchComplete = ContinuationDispatchComplete.handleDispatchComplete

let leaseStillDispatchable (runtime: FallbackRuntimeStore) (sessionID: string) (lease: PendingLease) : bool =
    let pending = (runtime.GetSession sessionID).PendingLease

    ensureActiveAndOwner runtime sessionID lease
    && (match pending with
        | Some p when p.ContinuationID = lease.ContinuationID ->
            match p.Status with
            | LeaseStatus.Requested
            | LeaseStatus.DispatchStarted -> true
            | LeaseStatus.AcceptanceUnknown
            | LeaseStatus.Dispatched
            | LeaseStatus.Running
            | LeaseStatus.Cancelled
            | LeaseStatus.Settled -> false
        | _ -> false)

let claimDispatchStarted
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

let private transitionLeaseToDispatchStarted
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (continuationID: string)
    (status: LeaseStatus)
    : bool =
    match status with
    | LeaseStatus.Requested ->
        runtime.UpdateSessionReturning(
            sessionID,
            tryTransitionPendingLeaseReturning continuationID LeaseStatus.Requested LeaseStatus.DispatchStarted
        )
    | LeaseStatus.DispatchStarted -> false
    | LeaseStatus.AcceptanceUnknown ->
        runtime.UpdateSessionReturning(
            sessionID,
            tryTransitionPendingLeaseReturning continuationID LeaseStatus.AcceptanceUnknown LeaseStatus.DispatchStarted
        )
    | LeaseStatus.Running ->
        runtime.UpdateSessionReturning(
            sessionID,
            tryTransitionPendingLeaseReturning continuationID LeaseStatus.Running LeaseStatus.DispatchStarted
        )
    | LeaseStatus.Dispatched -> true
    | LeaseStatus.Cancelled
    | LeaseStatus.Settled -> false

/// Transport returned. Must NOT promote lease to Dispatched — that is solely
/// `recordHostAcceptedContinuation` on host evidence. Stale prompt completion
/// is ignored; never append a late cancellation from transport return alone.
let handleTransportReturned
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
                transitionLeaseToDispatchStarted runtime sessionID lease.ContinuationID current.Status

            if canTransition then
                runtime.Update(sessionID, setInjected model atMs)
        | _ -> ()
    }
