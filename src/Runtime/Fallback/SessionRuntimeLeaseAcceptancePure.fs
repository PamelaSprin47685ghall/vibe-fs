module Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

/// Mark a continuation accepted when its real host user message is observed.
/// Binds HostUserMessageId so later assistant.parentID can strictly correlate.
/// A late prompt Promise must not be the acceptance signal.
let tryAcceptPendingLease expectedID (hostUserMessageId: string) (s: FallbackSessionRuntime) =
    match s.PendingLease with
    | Some lease when lease.ContinuationID = expectedID ->
        let bindHostId (current: FallbackSessionRuntime) =
            match current.PendingLease with
            | Some l when hostUserMessageId <> "" && l.HostUserMessageId = "" ->
                { current with
                    PendingLease = Some { l with HostUserMessageId = hostUserMessageId } }
            | _ -> current

        match lease.Status with
        | LeaseStatus.Requested ->
            tryTransitionPendingLease expectedID LeaseStatus.Requested LeaseStatus.Dispatched s
            |> Option.map bindHostId
        | LeaseStatus.DispatchStarted ->
            tryTransitionPendingLease expectedID LeaseStatus.DispatchStarted LeaseStatus.Dispatched s
            |> Option.map bindHostId
        | LeaseStatus.Dispatched
        | LeaseStatus.Running -> Some(bindHostId s)
        | LeaseStatus.Cancelled
        | LeaseStatus.Settled -> None
    | _ -> None

let tryAcceptPendingLeaseReturning
    expectedID
    (hostUserMessageId: string)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime * bool =
    match tryAcceptPendingLease expectedID hostUserMessageId s with
    | Some s' -> s', true
    | None -> s, false
