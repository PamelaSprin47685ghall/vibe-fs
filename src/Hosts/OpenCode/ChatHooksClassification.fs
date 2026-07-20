module Wanxiangshu.Hosts.Opencode.ChatHooksClassification

open Wanxiangshu.Kernel.Primitives.Identity

open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Hosts.Opencode.SubsessionHostAdapterTypes
open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Hosts.Opencode.ChatHooksDecoders
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Kernel.HostTools

let consumeAndDispatch
    (activeNudgeNonce: string)
    (nonce: string)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime * bool =
    let s1, transitioned =
        match s.PendingNudgeLease with
        | Some lease when lease.Nonce = nonce ->
            tryTransitionPendingNudgeLeaseReturning
                lease.NudgeID
                LeaseStatus.DispatchStarted
                LeaseStatus.Dispatched
                s
        | _ -> s, false

    let s2, consumed =
        if activeNudgeNonce <> "" && nonce = activeNudgeNonce then
            tryConsumeNudgeNonce nonce s1
        else
            s1, false

    s2, transitioned || consumed

/// Attempt to consume a nudge or subsession nonce observed in a
/// chat.message hook payload. Returns true when the nonce is recognised.
let tryConsumeNudgeIfMatched
    (fr: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionIDStr: string)
    (msgId: string)
    (nonce: string)
    : bool =
    let ws = workspaceFor workspaceRoot

    let receiptResult =
        if msgId = "" then
            ResolveAttemptResult.NotFound
        else
            HostReceiptWaiterRegistry.tryResolve
                ws
                sessionIDStr
                nonce
                (Wanxiangshu.Kernel.Subsession.Types.UserMessageObserved msgId)

    let receiptMatched = receiptResult <> ResolveAttemptResult.NotFound
    let activeNudgeNonce = (fr.GetSession sessionIDStr).ActiveNudgeNonce

    let nudgeMatched =
        if receiptMatched || (activeNudgeNonce <> "" && nonce = activeNudgeNonce) then
            fr.UpdateSessionReturning(sessionIDStr, consumeAndDispatch activeNudgeNonce nonce)
        else
            false

    if receiptMatched || nudgeMatched then
        true
    else
        match (fr.GetSession sessionIDStr).PendingLease with
        | Some lease when
            (lease.Status = LeaseStatus.DispatchStarted
             || lease.Status = LeaseStatus.Dispatched
             || lease.Status = LeaseStatus.Running)
            && lease.ContinuationID = nonce
            ->
            true
        | _ -> false

let tryAcceptFallbackContinuation
    (fr: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionIDStr: string)
    (msgId: string)
    (continuationId: string)
    : bool =
    let receiptMatched =
        if msgId = "" then
            false
        else
            HostReceiptWaiterRegistry.tryResolve
                (workspaceFor workspaceRoot)
                sessionIDStr
                continuationId
                (Wanxiangshu.Kernel.Subsession.Types.UserMessageObserved msgId)
            <> ResolveAttemptResult.NotFound

    let accepted = fr.UpdateSessionReturning(sessionIDStr, tryAcceptPendingLeaseReturning continuationId)

    accepted || receiptMatched

/// Classify whether a chat.message hook payload is system-synthesised.
/// Internal so the regression suite can bind directly to this entry
/// point (mirroring the `static member internal isNaturalStop` pattern
/// in NudgeTrigger) instead of re-encoding the rule in test fixtures.
let isSystemMessage
    (parts: obj)
    (fr: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionIDStr: string)
    (msgId: string)
    : bool =
    // Continuation prompts carry versioned provenance and should never
    // be confused with nudges, so check the namespaced kind first.
    match tryGetWanxiangshuKind parts with
    | Some "fallback_continuation" ->
        match tryDecodeWanxiangshuProvenance parts with
        | Some provenance ->
            let _ = tryAcceptFallbackContinuation fr workspaceRoot sessionIDStr msgId provenance.ContinuationId
            true
        | None -> true
    | _ ->
        match tryGetNonceFromParts parts with
        | Some nonce -> tryConsumeNudgeIfMatched fr workspaceRoot sessionIDStr msgId nonce
        | None -> false
