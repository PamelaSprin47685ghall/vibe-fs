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
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchOps
open Wanxiangshu.Hosts.Opencode.ChatHooksDecoders
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.Fallback.ContinuationDispatchRegistry
open Wanxiangshu.Kernel.HostTools

let consumeAndDispatch
    (activeNudgeNonce: string)
    (nonce: string)
    (hostUserMessageId: string)
    (s: FallbackSessionRuntime)
    : FallbackSessionRuntime * bool =
    let sBound, bound =
        tryBindNudgeHostUserMessage nonce hostUserMessageId s

    let s1, transitioned =
        match sBound.PendingNudgeLease with
        | Some lease when lease.Nonce = nonce ->
            tryTransitionPendingNudgeLeaseReturning
                lease.NudgeID
                LeaseStatus.DispatchStarted
                LeaseStatus.Dispatched
                sBound
        | _ -> sBound, false

    let s2, consumed =
        if activeNudgeNonce <> "" && nonce = activeNudgeNonce then
            tryConsumeNudgeNonce nonce s1
        else
            s1, false

    s2, bound || transitioned || consumed

/// Attempt to consume a nudge or subsession nonce observed in a
/// chat.message hook payload. Returns true when the nonce is recognised.
/// Binds HostUserMessageId on accept (SPEC §七 step 3).
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
            fr.UpdateSessionReturning(sessionIDStr, consumeAndDispatch activeNudgeNonce nonce msgId)
        else
            false

    if receiptMatched || nudgeMatched then
        true
    else
        match (fr.GetSession sessionIDStr).PendingLease with
        | Some lease when
            (lease.Status = LeaseStatus.DispatchStarted
             || lease.Status = LeaseStatus.AcceptanceUnknown
             || lease.Status = LeaseStatus.Dispatched
             || lease.Status = LeaseStatus.Running
             || lease.Status = LeaseStatus.Requested)
            && lease.ContinuationID = nonce
            ->
            let _ =
                fr.UpdateSessionReturning(sessionIDStr, tryAcceptPendingLeaseReturning nonce msgId)

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

    // chat.message is host evidence. Sync status first so classifiers see
    // Dispatched immediately; async path emits continuation_dispatched once.
    let acceptedSync = fr.UpdateSessionReturning(sessionIDStr, tryAcceptPendingLeaseReturning continuationId msgId)

    if acceptedSync then
        recordHostAcceptedContinuation fr workspaceRoot sessionIDStr continuationId
        |> Promise.map ignore
        |> Promise.start

    acceptedSync || receiptMatched

/// Classify whether a chat.message hook payload is system-synthesised.
/// Also binds PendingDispatch → HostUserMessageId when a marker matches.
/// Metadata is a probe only; durable fact is the bound host message id.
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
