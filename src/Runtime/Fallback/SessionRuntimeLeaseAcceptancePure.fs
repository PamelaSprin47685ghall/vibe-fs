module Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

let private bindHostUserMessageId (hostUserMessageId: string) (current: string) : string =
    if hostUserMessageId = "" then current
    elif current = "" || current = hostUserMessageId then hostUserMessageId
    else current

/// Mark a continuation accepted when its real host user message is observed.
/// Binds HostUserMessageId so later assistant.parentID can strictly correlate.
/// A late prompt Promise must not be the acceptance signal.
/// Binds HostUserMessageId for subsequent parentID attribution (F-02).
let tryAcceptPendingLease expectedID (hostUserMessageId: string) (s: FallbackSessionRuntime) =
    match s.PendingLease with
    | Some lease when lease.ContinuationID = expectedID ->
        let boundId = bindHostUserMessageId hostUserMessageId lease.HostUserMessageId

        let lease' =
            if boundId <> lease.HostUserMessageId then
                { lease with
                    HostUserMessageId = boundId }
            else
                lease

        let sBound =
            if lease' <> lease then
                { s with PendingLease = Some lease' }
            else
                s

        match lease'.Status with
        | LeaseStatus.Requested ->
            tryTransitionPendingLease expectedID LeaseStatus.Requested LeaseStatus.Dispatched sBound
        | LeaseStatus.DispatchStarted ->
<<<<<<< HEAD
            tryTransitionPendingLease expectedID LeaseStatus.DispatchStarted LeaseStatus.Dispatched sBound
=======
            tryTransitionPendingLease expectedID LeaseStatus.DispatchStarted LeaseStatus.Dispatched s
        | LeaseStatus.AcceptanceUnknown ->
            tryTransitionPendingLease expectedID LeaseStatus.AcceptanceUnknown LeaseStatus.Dispatched s
>>>>>>> 98bc01f6 (fix(mux): wire AcceptanceUnknown/AbortUnknown degrade paths end-to-end)
        | LeaseStatus.Dispatched
        | LeaseStatus.Running -> Some sBound
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

/// Bind HostUserMessageId on the matching nudge lease (by nonce) and advance
/// DispatchStarted → Dispatched when still in flight.
let tryBindNudgeHostUserMessage
    (nonce: string)
    (hostUserMessageId: string)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime * bool =
    match s.PendingNudgeLease with
    | Some lease when lease.Nonce = nonce ->
        let boundId = bindHostUserMessageId hostUserMessageId lease.HostUserMessageId

        let lease' =
            if boundId <> lease.HostUserMessageId then
                { lease with
                    HostUserMessageId = boundId }
            else
                lease

        let sBound =
            if lease' <> lease then
                { s with
                    PendingNudgeLease = Some lease' }
            else
                s

        match lease'.Status with
        | LeaseStatus.DispatchStarted ->
            match
                tryTransitionPendingNudgeLease
                    lease'.NudgeID
                    LeaseStatus.DispatchStarted
                    LeaseStatus.Dispatched
                    sBound
            with
            | Some s' -> s', true
            | None -> sBound, boundId <> "" || lease' <> lease
        | LeaseStatus.Dispatched
        | LeaseStatus.Running -> sBound, boundId <> "" || lease' <> lease
        | _ -> sBound, false
    | _ -> s, false
