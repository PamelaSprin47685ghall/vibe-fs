namespace Wanxiangshu.Runtime.Dispatch

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Dispatch.Events
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.PromiseQueue

/// Helper module split out so the dispatcher class body stays under the
/// function-length limit.
module SessionDispatcherOps =

    /// Await the user-supplied host prompt promise. Returns Ok receipt on
    /// success, or Error exn if the promise rejects. The result is raced
    /// against a cancel promise (CancelWaiter) so that OnSessionClosed /
    /// CancelByTurn can unblock the transport immediately.
    let awaitReceipt
        (awaited: JS.Promise<DispatchAcceptance>)
        (r: DispatchRecord)
        : JS.Promise<Result<DispatchAcceptance, exn>> =
        let cancelPromise: JS.Promise<Result<DispatchAcceptance, exn>> =
            Promise.create (fun resolve _ ->
                r.CancelWaiter <- Some(fun () -> resolve (Error(System.Exception("cancelled")))))

        let safeAwaited: JS.Promise<Result<DispatchAcceptance, exn>> =
            promise {
                try
                    let! r = awaited
                    return Ok r
                with ex ->
                    return Error ex
            }

        promise {
            let! result = Promise.race [| safeAwaited; cancelPromise |]
            return result
        }

    /// Apply a receipt (or transport error) to the dispatch record.
    /// When the exception message contains "opencode_session_api_missing"
    /// the record is resolved as TransportUnavailable so the caller sees
    /// HostRejected rather than HostAcceptanceUnknown.
    let applyReceipt
        (r: DispatchRecord)
        (receiptOrError: Result<DispatchAcceptance, exn>)
        (logger: IDispatchEventLogger)
        : unit =
        match receiptOrError with
        | Ok receipt ->
            match receipt with
            | UserMessageAccepted "" -> DispatchOps.rejectUnknown r "EmptyReceipt" "host returned empty user message id"
            | _ -> DispatchOps.acceptRecord r receipt (DispatchOps.getNowMs ()) logger
        | Error exn when exn.Message.Contains("opencode_session_api_missing") ->
            let err =
                { ErrorName = "TransportUnavailable"
                  DomainError = None
                  Message = "opencode_session_api_missing: " + exn.Message
                  StatusCode = None
                  IsRetryable = Some false }

            DispatchOps.resolveRecord r (TransportUnavailable err)
        | Error _ -> DispatchOps.rejectUnknown r "EmptyReceipt" "host returned empty user message id"
