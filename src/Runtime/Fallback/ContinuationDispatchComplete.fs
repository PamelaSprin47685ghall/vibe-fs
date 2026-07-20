module Wanxiangshu.Runtime.Fallback.ContinuationDispatchComplete

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
open Wanxiangshu.Runtime.MuxLogicalReceipt

/// Cancel a dispatch after it has been started.
let cancelAfterDispatch
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
            let session = runtime.GetSession sessionID

            if session.AbortUnavailable then
                do!
                    finishContinuation
                        runtime
                        workspaceRoot
                        sessionID
                        lease
                        ContinuationOutcome.AbortUnknown
                        (reason + " (" + abortUnavailableMessage + ")")
            else
                try
                    do! executor.AbortRun sessionID
                    do! finishContinuation runtime workspaceRoot sessionID lease ContinuationOutcome.Cancelled reason
                with ex when isAbortUnavailableMessage ex.Message ->
                    runtime.Update(sessionID, setAbortUnavailable true)

                    do!
                        finishContinuation
                            runtime
                            workspaceRoot
                            sessionID
                            lease
                            ContinuationOutcome.AbortUnknown
                            (reason + " (" + ex.Message + ")")
    }

let private tryMarkDispatched
    (runtime: FallbackRuntimeStore)
    (sessionID: string)
    (lease: PendingLease)
    (current: PendingLease)
    : bool =
    match current.Status with
    | LeaseStatus.Requested ->
        runtime.UpdateSessionReturning(
            sessionID,
            tryTransitionPendingLeaseReturning lease.ContinuationID LeaseStatus.Requested LeaseStatus.Dispatched
        )
    | LeaseStatus.DispatchStarted ->
        runtime.UpdateSessionReturning(
            sessionID,
            tryTransitionPendingLeaseReturning lease.ContinuationID LeaseStatus.DispatchStarted LeaseStatus.Dispatched
        )
    | LeaseStatus.AcceptanceUnknown ->
        runtime.UpdateSessionReturning(
            sessionID,
            tryTransitionPendingLeaseReturning lease.ContinuationID LeaseStatus.AcceptanceUnknown LeaseStatus.Dispatched
        )
    | LeaseStatus.Running ->
        runtime.UpdateSessionReturning(
            sessionID,
            tryTransitionPendingLeaseReturning lease.ContinuationID LeaseStatus.Running LeaseStatus.Dispatched
        )
    | LeaseStatus.Dispatched -> true
    | LeaseStatus.Cancelled
    | LeaseStatus.Settled -> false

/// Handle post-dispatch completion: validate lease, write event, update state.
let handleDispatchComplete
    (runtime: FallbackRuntimeStore)
    (_executor: IActionExecutor)
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

            if tryMarkDispatched runtime sessionID lease current then
                runtime.Update(sessionID, setInjected model atMs)
        | _ ->
            // Terminal handling or a newer lease won the race. The prompt
            // completion is stale evidence; never append a late cancellation.
            ()
    }
