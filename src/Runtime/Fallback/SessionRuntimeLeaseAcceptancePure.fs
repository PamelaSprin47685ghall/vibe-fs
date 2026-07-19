module Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

/// Mark a continuation accepted when its real host user message is observed.
/// A late prompt Promise must not be the acceptance signal.
let tryAcceptPendingLease expectedID (s: FallbackSessionRuntime) =
    match s.PendingLease with
    | Some lease when lease.ContinuationID = expectedID ->
        match lease.Status with
        | LeaseStatus.Requested -> tryTransitionPendingLease expectedID LeaseStatus.Requested LeaseStatus.Dispatched s
        | LeaseStatus.DispatchStarted ->
            tryTransitionPendingLease expectedID LeaseStatus.DispatchStarted LeaseStatus.Dispatched s
        | LeaseStatus.Dispatched
        | LeaseStatus.Running -> Some s
        | LeaseStatus.Cancelled
        | LeaseStatus.Settled -> None
    | _ -> None

let tryAcceptPendingLeaseReturning expectedID (s: FallbackSessionRuntime) : FallbackSessionRuntime * bool =
    match tryAcceptPendingLease expectedID s with
    | Some s' -> s', true
    | None -> s, false
