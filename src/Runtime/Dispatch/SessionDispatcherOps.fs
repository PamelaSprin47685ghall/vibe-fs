namespace Wanxiangshu.Runtime.Dispatch

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Dispatch.Events
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.PromiseQueue

/// Internal actor state. Kept as a plain record so `SessionDispatcher`
/// can be a single class without cross-file type-extension pain.
type SessionDispatcherState =
    { Queue: SerialQueue
      mutable Active: DispatchRecord option
      mutable Generation: int
      mutable IsClosed: bool
      Workspace: WorkspaceId
      PhysicalSessionId: string
      EventLogger: IDispatchEventLogger }

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
<<<<<<< HEAD
        | Error exn ->
            // Preserve host/transport failure text so callers can classify
            // (helpers missing, nudge missing, session mismatch, etc.).
            let msg =
                let m = exn.Message
                if System.String.IsNullOrEmpty m then "transport failed" else m
            let name =
                if msg.Contains("AcceptanceUnknown") then "AcceptanceUnknown"
                elif msg.StartsWith("Busy:") then "Busy"
                elif msg.StartsWith("Failed:") then "Failed"
                elif msg.Contains("AbortUnknown") || msg.Contains("AbortUnavailable") then "AbortUnknown"
                else "TransportFailed"

            let err =
                { ErrorName = name
                  DomainError = None
                  Message = msg
                  StatusCode = None
                  IsRetryable = Some (name = "Busy") }

            let terminal =
                if name = "AcceptanceUnknown" then AcceptanceUnknown err
                elif name = "AbortUnknown" then AbortUnknown err
                elif name = "Failed" then Failed err
                elif msg.Contains("opencode_session_api_missing") then TransportUnavailable err
                else RejectedBeforeSend err

            DispatchOps.resolveRecord r terminal

    /// Reserve the per-session slot. Refuses if another dispatch is in flight or the session is closed.
    let reserveRecord (state: SessionDispatcherState) (r: DispatchRecord) (logger: IDispatchEventLogger) : JS.Promise<unit> =
        state.Queue.Enqueue(fun () ->
            promise {
                if state.IsClosed then
                    DispatchOps.resolveRecord
                        r
                        (RejectedBeforeSend
                            { ErrorName = "SessionClosed"
                              DomainError = None
                              Message = "DispatchRegistry: physical session has been closed"
                              StatusCode = None
                              IsRetryable = Some false })
                else
                    match state.Active with
                    | Some existing when existing.Identity.LogicalTurnId <> r.Identity.LogicalTurnId ->
                        // A physical session cannot safely host two turns.  Do not
                        // overwrite the active record: the old host run may still be
                        // executing and its late events must retain their identity.
                        DispatchOps.resolveRecord
                            r
                            (RejectedBeforeSend
                                { ErrorName = "AnotherDispatchInFlight"
                                  DomainError = None
                                  Message =
                                    "DispatchRegistry: physical session already has an active dispatch (id="
                                    + DispatchId.value existing.Identity.DispatchId
                                    + ")"
                                  StatusCode = None
                                  IsRetryable = Some false })
                    | Some existing ->
                        DispatchOps.resolveRecord
                            r
                            (RejectedBeforeSend
                                { ErrorName = "AnotherDispatchInFlight"
                                  DomainError = None
                                  Message =
                                    "DispatchRegistry: physical session already has an active dispatch (id="
                                    + DispatchId.value existing.Identity.DispatchId
                                    + ")"
                                  StatusCode = None
                                  IsRetryable = Some false })
                    | None ->
                        state.Active <- Some r
                        state.Generation <- state.Generation + 1

                        logger.Log(
                            DispatchRequested(r.Identity, "host_prompt", DispatchOps.digestForPrompt r.Identity)
                        )
            })

    /// Run the user-supplied `sendPrompt` and translate the result into the
    /// dispatch state machine.
    let runTransport
        (state: SessionDispatcherState)
        (r: DispatchRecord)
        (sendPrompt: DispatchIdentity -> JS.Promise<DispatchAcceptance>)
        (cancellation: System.Threading.CancellationToken)
        (logger: IDispatchEventLogger)
        : JS.Promise<unit> =
        promise {
            let! shouldSend =
                state.Queue.Enqueue(fun () ->
                    promise {
                        if r.Terminal.IsSome then
                            return false
                        elif state.IsClosed then
                            if r.Terminal.IsNone then
                                DispatchOps.resolveRecord r SessionClosed
                            return false
                        elif DispatchOps.isCancelRequested r cancellation then
                            if r.Terminal.IsNone then
                                DispatchOps.resolveRecord r Cancelled
                            return false
                        elif (state.Active
                              |> Option.exists (fun active -> not (obj.ReferenceEquals(active, r)))) then
                            if r.Terminal.IsNone then
                                DispatchOps.resolveRecord r Superseded
                            return false
                        else
                            r.Phase <- TransportStarted
                            logger.Log(
                                DispatchTransportStarted(r.Identity.DispatchId, DispatchOps.getNowMs ())
                            )

                            return true
                    })

            if shouldSend then
                let awaitedOpt: JS.Promise<DispatchAcceptance> option =
                    try
                        Some(sendPrompt r.Identity)
                    with _ex ->
                        None

                match awaitedOpt with
                | None ->
                    do!
                        state.Queue.Enqueue(fun () ->
                            promise {
                                if r.Terminal.IsNone then
                                    DispatchOps.rejectUnknown r "TransportThrew" "sendPrompt threw synchronously"
                            })
                | Some awaited ->
                    let! receipt = awaitReceipt awaited r

                    do!
                        state.Queue.Enqueue(fun () ->
                            promise {
                                if r.Terminal.IsNone then
                                    applyReceipt r receipt logger
                            })
        }
