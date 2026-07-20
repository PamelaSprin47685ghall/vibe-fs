module Wanxiangshu.Runtime.Fallback.ContinuationDispatchRegistry

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeaseAcceptancePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Clock
open Wanxiangshu.Runtime.ContinuationEventWriter

let private modelString (model: FallbackModel) =
    match model.Variant with
    | Some v -> $"{model.ProviderID}/{model.ModelID}:{v}"
    | None -> $"{model.ProviderID}/{model.ModelID}"

let private emitDispatchedFacts
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    (lease: PendingLease)
    (agent: string)
    : JS.Promise<unit> =
    promise {
        // Claim the once-flag synchronously before any await so concurrent
        // ChatHooks + ActionExecutor receipt paths cannot double-append.
        let atMs = getTimestampMs ()
        let model = lease.Model

        let claimed =
            runtime.UpdateSessionReturning(
                sessionID,
                fun s ->
                    if s.InjectedAt.IsSome then
                        s, false
                    else
                        setInjected model atMs s, true
            )

        if claimed then
            do!
                appendContinuationDispatchedOrFail
                    workspaceRoot
                    sessionID
                    continuationID
                    (modelString model)
                    agent
                    atMs
                    lease.ContinuationOrdinal
    }

/// Sole production path that may advance a continuation lease to Dispatched and
/// emit continuation_dispatched. Trigger = host evidence (OpenCode: chat.message
/// / HostReceiptWaiter), never the prompt() Promise return. Idempotent on both
/// status and event emission (InjectedAt once-flag).
let recordHostAcceptedContinuation
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionID: string)
    (continuationID: string)
    : JS.Promise<bool> =
    promise {
        let session = runtime.GetSession sessionID

        match session.PendingLease with
        | None -> return false
        | Some lease when lease.ContinuationID <> continuationID -> return false
        | Some lease ->
            match lease.Status with
            | LeaseStatus.Cancelled
            | LeaseStatus.Settled -> return false
            | LeaseStatus.AcceptanceUnknown
            | LeaseStatus.Dispatched
            | LeaseStatus.Running ->
                do! emitDispatchedFacts runtime workspaceRoot sessionID continuationID lease session.AgentName
                return true
            | LeaseStatus.Requested
            | LeaseStatus.DispatchStarted ->
                let accepted =
                    runtime.UpdateSessionReturning(sessionID, tryAcceptPendingLeaseReturning continuationID "")

                if not accepted then
                    return false
                else
                    do! emitDispatchedFacts runtime workspaceRoot sessionID continuationID lease session.AgentName
                    return true
    }
