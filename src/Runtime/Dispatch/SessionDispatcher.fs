namespace Wanxiangshu.Runtime.Dispatch

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Dispatch.Events
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.PromiseQueue

/// Per-session mailbox. Hosts lifecycle transitions and serializes the short
/// state changes around a transport call. The host promise itself never runs
/// inside the mailbox: a slow or unresolved prompt must not block lifecycle
/// events, cancellation, or a competing dispatch from being observed.
type SessionDispatcher(workspace: WorkspaceId, physicalSessionId: string, eventLogger: IDispatchEventLogger) =

    let state =
        { Queue = SerialQueue()
          Active = None
          Generation = 0
          IsClosed = false
          Workspace = workspace
          PhysicalSessionId = physicalSessionId
          EventLogger = eventLogger }

    member _.HasActive = state.Active.IsSome
    member _.ActiveIdentity = state.Active |> Option.map (fun r -> r.Identity)

    member _.ActiveLogicalTurnId =
        state.Active |> Option.map (fun r -> r.Identity.LogicalTurnId)

    member _.Workspace = state.Workspace
    member _.PhysicalSessionId = state.PhysicalSessionId
    member _.EventLogger = state.EventLogger
    member internal _.State = state

    /// Submit a dispatch to the host.
    member this.Dispatch
        (identity: DispatchIdentity)
        (sendPrompt: DispatchIdentity -> JS.Promise<DispatchAcceptance>)
        (cancellation: System.Threading.CancellationToken)
        : JS.Promise<DispatchOutcome * HostReceiptWaiter option> =
        promise {
            if identity.PhysicalSessionId <> this.PhysicalSessionId then
                let outcome: DispatchOutcome =
                    DispatchOutcome.Failed(
                        TransportUnavailable
                            { ErrorName = "DispatchIdentityMismatch"
                              DomainError = None
                              Message = "DispatchRegistry: identity.PhysicalSessionId does not match owning session"
                              StatusCode = None
                              IsRetryable = Some false }
                    )

                return (outcome, None)
            else
                let r =
                    { Identity = identity
                      Phase = Requested
                      AcceptedMessageId = ""
                      AcceptedRunId = ""
                      Waiter = None
                      ReceiptWaiter = None
                      CancelRequested = false
                      AbortSent = false
                      Terminal = None
                      CancelToken = new System.Threading.CancellationTokenSource()
                      OnResolve = ignore
                      CancelWaiter = None }

                let receiptWaiter =
                    HostReceiptWaiterRegistry.create state.Workspace this.PhysicalSessionId identity.LogicalTurnId

                r.ReceiptWaiter <- Some receiptWaiter

                let resultPromise: JS.Promise<DispatchOutcome> =
                    Promise.create (fun resolve _ -> r.Waiter <- Some(fun o -> resolve o))

                r.OnResolve <-
                    (fun () ->
                        match state.Active with
                        | Some active when obj.ReferenceEquals(active, r) -> state.Active <- None
                        | _ -> ())

                do! this.Reserve r
                // Keep the transport outside the mailbox. Its result is
                // re-entered through the queue by RunTransport.
                this.RunTransport r sendPrompt cancellation |> Promise.start
                let! outcome = resultPromise
                return (outcome, r.ReceiptWaiter)
        }

    /// Reserve the per-session slot. Refuses if another dispatch is in flight or the session is closed.
    member private this.Reserve(r: DispatchRecord) : JS.Promise<unit> =
        SessionDispatcherOps.reserveRecord state r eventLogger

    /// Run the user-supplied `sendPrompt` and translate the result into the
    /// dispatch state machine.
    member private this.RunTransport
        (r: DispatchRecord)
        (sendPrompt: DispatchIdentity -> JS.Promise<DispatchAcceptance>)
        (cancellation: System.Threading.CancellationToken)
        : JS.Promise<unit> =
        SessionDispatcherOps.runTransport state r sendPrompt cancellation eventLogger
