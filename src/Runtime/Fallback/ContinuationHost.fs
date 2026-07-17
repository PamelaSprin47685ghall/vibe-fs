module Wanxiangshu.Runtime.Fallback.ContinuationHost

open Fable.Core
open Wanxiangshu.Kernel.Fallback.Continuation

/// Receipt returned by a host adapter after a continuation dispatch attempt.
[<RequireQualifiedAccess>]
type HostDispatchReceipt =
    /// Host created a user message and can identify it.
    | UserMessageAccepted of userMessageId: string
    /// Host accepted the dispatch as a run and can identify it.
    | RunAccepted of runId: string
    /// Host accepted the dispatch but cannot provide a concrete message/run id.
    | OpaqueAccepted of receiptId: string

/// Host adapter for continuation dispatch, abort and reconciliation.
type IContinuationHost =
    /// Dispatch a continuation to the host. Returns a receipt the runtime can bind.
    abstract Dispatch: request: ContinuationRequest -> JS.Promise<HostDispatchReceipt>

    /// Attempt to abort a continuation that this runtime still owns.
    /// Returns true when the host confirmed the abort.
    abstract TryAbortOwned: request: ContinuationRequest * receipt: HostDispatchReceipt -> JS.Promise<bool>

    /// Reconcile a continuation after restart or missing events.
    /// Returns a concrete receipt if the host can locate the dispatched message/run.
    abstract Reconcile: request: ContinuationRequest -> JS.Promise<HostDispatchReceipt option>

let hostReceiptToIdentity (receipt: HostDispatchReceipt) : ContinuationHostIdentity =
    match receipt with
    | HostDispatchReceipt.UserMessageAccepted userMessageId ->
        ContinuationHostIdentity.UserMessageIdentity userMessageId
    | HostDispatchReceipt.RunAccepted runId -> ContinuationHostIdentity.RunIdentity runId
    | HostDispatchReceipt.OpaqueAccepted receiptId -> ContinuationHostIdentity.OpaqueIdentity receiptId
