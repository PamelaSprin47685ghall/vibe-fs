namespace Wanxiangshu.Runtime.Dispatch

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Subsession.Types

/// Per-dispatch transport classification kept with the receipt waiter so
/// `QueryDispatchStatus` can report pre-send vs. post-send failures honestly.
type HostReceiptWaiterTransportState =
    | InFlight
    | BeforeSendRejected of ErrorInput
    | AfterSendUnknown of ErrorInput
    | ReceiptResolved of HostStartReceipt
    | ReceiptRejected of DispatchFailure
    | UserCancelled

type ResolveAttemptResult =
    | ResolvedNow
    | AlreadyCompleted
    | NotFound

/// Exactly-once waiter for a host `UserMessageObserved` / `OrderedTurnMarkerObserved`
/// receipt. Keyed by `(WorkspaceId, PhysicalSessionId, LogicalTurnId)` so two
/// plugin instances in the same Node process cannot collide on the same nonce.
type HostReceiptWaiter =
    { WorkspaceId: WorkspaceId
      PhysicalSessionId: string
      LogicalTurnId: string
      Promise: JS.Promise<Result<HostStartReceipt, DispatchFailure>>
      Resolve: Result<HostStartReceipt, DispatchFailure> -> unit
      mutable Completed: bool
      mutable TransportState: HostReceiptWaiterTransportState
      Cleanup: unit -> unit }

module HostReceiptWaiter =

    let cancelError: ErrorInput =
        { ErrorName = "Cancelled"
          DomainError = None
          Message = "Pending host receipt cancelled"
          StatusCode = None
          IsRetryable = Some false }

    let sessionClosedError: ErrorInput =
        { ErrorName = "SessionClosed"
          DomainError = None
          Message = "Session closed while waiting for host receipt"
          StatusCode = None
          IsRetryable = Some false }

    let cancelledPromise () : JS.Promise<Result<HostStartReceipt, DispatchFailure>> =
        Promise.lift (Error(HostRejected cancelError))

    /// Translate a dispatcher acceptance into the canonical host receipt.
    let dispatchAcceptanceToHostReceipt (a: Protocol.DispatchAcceptance) : HostStartReceipt =
        match a with
        | Protocol.UserMessageAccepted mid -> UserMessageObserved mid
        | Protocol.RunAccepted rid -> HostRunAccepted rid
        | Protocol.OpaqueAccepted _ -> OrderedTurnMarkerObserved

    let private errorInputForTerminal (t: Protocol.DispatchTerminal) : ErrorInput =
        match t with
        | Protocol.RejectedBeforeSend e
        | Protocol.TransportUnavailable e
        | Protocol.Failed e
        | Protocol.AcceptanceUnknown e
        | Protocol.AbortUnknown e
        | Protocol.TimedOut e -> e
        | Protocol.Cancelled -> cancelError
        | Protocol.SessionClosed -> sessionClosedError
        | Protocol.Superseded ->
            { ErrorName = "Superseded"
              DomainError = None
              Message = "dispatch superseded by a newer request"
              StatusCode = None
              IsRetryable = Some false }
        | Protocol.Poisoned s ->
            { ErrorName = "Poisoned"
              DomainError = None
              Message = s
              StatusCode = None
              IsRetryable = Some false }
        | Protocol.Completed ->
            { ErrorName = "Completed"
              DomainError = None
              Message = "dispatch completed"
              StatusCode = None
              IsRetryable = Some false }

    /// Translate a dispatcher terminal into the `DispatchFailure` returned by
    /// `ISubsessionHost.Dispatch`.
    let dispatchFailureOfTerminal (t: Protocol.DispatchTerminal) : DispatchFailure =
        let err = errorInputForTerminal t

        match t with
        | Protocol.RejectedBeforeSend _
        | Protocol.TransportUnavailable _
        | Protocol.Cancelled
        | Protocol.SessionClosed -> HostRejected err
        | _ -> HostAcceptanceUnknown err

    let private transportStateOfTerminal (t: Protocol.DispatchTerminal) : HostReceiptWaiterTransportState option =
        match t with
        | Protocol.Completed -> None
        | Protocol.RejectedBeforeSend e -> Some(BeforeSendRejected e)
        | Protocol.TransportUnavailable e -> Some(BeforeSendRejected e)
        | Protocol.Cancelled
        | Protocol.SessionClosed -> Some UserCancelled
        | Protocol.Failed e
        | Protocol.AcceptanceUnknown e
        | Protocol.AbortUnknown e
        | Protocol.TimedOut e -> Some(AfterSendUnknown e)
        | Protocol.Superseded -> Some(AfterSendUnknown(errorInputForTerminal t))
        | Protocol.Poisoned s -> Some(AfterSendUnknown(errorInputForTerminal t))

    let resolve (w: HostReceiptWaiter) (receipt: HostStartReceipt) : ResolveAttemptResult =
        if w.Completed then
            AlreadyCompleted
        else
            w.Completed <- true
            w.TransportState <- ReceiptResolved receipt
            w.Resolve(Ok receipt)
            w.Cleanup()
            ResolvedNow

    let reject
        (w: HostReceiptWaiter)
        (failure: DispatchFailure)
        (transport: HostReceiptWaiterTransportState)
        : ResolveAttemptResult =
        if w.Completed then
            AlreadyCompleted
        else
            w.Completed <- true
            w.TransportState <- transport
            w.Resolve(Error failure)
            w.Cleanup()
            ResolvedNow

    /// Resolve from a dispatcher acceptance.
    /// Only strong receipts (`UserMessageAccepted` / `RunAccepted`) resolve the
    /// waiter. `OpaqueAccepted` intentionally does nothing: OpenCode dispatches
    /// require a real `chat.message` observation before the host receipt is
    /// known. OMP does not use the `HostReceiptWaiter` path at all.
    let resolveFromAcceptance (w: HostReceiptWaiter) (a: Protocol.DispatchAcceptance) : unit =
        match a with
        | Protocol.UserMessageAccepted mid -> resolve w (UserMessageObserved mid) |> ignore
        | Protocol.RunAccepted rid -> resolve w (HostRunAccepted rid) |> ignore
        | Protocol.OpaqueAccepted _ -> ()

    /// Resolve from a terminal outcome. `Completed` is a no-op because the
    /// acceptance path should have already resolved the waiter.
    let resolveFromTerminal (w: HostReceiptWaiter) (t: Protocol.DispatchTerminal) : unit =
        match transportStateOfTerminal t with
        | None -> ()
        | Some transport ->
            let failure = dispatchFailureOfTerminal t
            reject w failure transport |> ignore

    /// Resolve from an already-known host receipt.
    let resolveFromReceipt (w: HostReceiptWaiter) (receipt: HostStartReceipt) : unit = resolve w receipt |> ignore
