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

/// Internal actor state. Kept as a plain record so `SessionDispatcher`
/// can be a single class without cross-file type-extension pain.
type SessionDispatcherState =
    { Queue: SerialQueue
      mutable Active: DispatchRecord option
      Workspace: WorkspaceId
      PhysicalSessionId: string
      EventLogger: IDispatchEventLogger }

/// Per-session mailbox. Hosts lifecycle transitions and the actual `sendPrompt`
/// invocation inside one serial queue so two dispatches on the same physical
/// session can never race.
type SessionDispatcher(workspace: WorkspaceId, physicalSessionId: string, eventLogger: IDispatchEventLogger) =

    let state =
        { Queue = SerialQueue()
          Active = None
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
        : JS.Promise<DispatchOutcome> =
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

                return outcome
            else
                let r =
                    { Identity = identity
                      Phase = Requested
                      AcceptedMessageId = ""
                      AcceptedRunId = ""
                      Waiter = None
                      CancelRequested = false
                      AbortSent = false
                      Terminal = None
                      CancelToken = new System.Threading.CancellationTokenSource()
                      OnResolve = ignore
                      CancelWaiter = None }

                let resultPromise: JS.Promise<DispatchOutcome> =
                    Promise.create (fun resolve _ -> r.Waiter <- Some(fun o -> resolve o))

                r.OnResolve <-
                    (fun () ->
                        match state.Active with
                        | Some active when obj.ReferenceEquals(active, r) -> state.Active <- None
                        | _ -> ())

                do! this.Reserve r
                do! this.RunTransport r sendPrompt cancellation
                return! resultPromise
        }

    /// Reserve the per-session slot. Refuses if another dispatch is in flight.
    member private this.Reserve(r: DispatchRecord) : JS.Promise<unit> =
        state.Queue.Enqueue(fun () ->
            promise {
                match state.Active with
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

                    eventLogger.Log(
                        DispatchRequested(r.Identity, "host_prompt", DispatchOps.digestForPrompt r.Identity)
                    )
            })

    /// Run the user-supplied `sendPrompt` and translate the result into the
    /// dispatch state machine.
    member private this.RunTransport
        (r: DispatchRecord)
        (sendPrompt: DispatchIdentity -> JS.Promise<DispatchAcceptance>)
        (cancellation: System.Threading.CancellationToken)
        : JS.Promise<unit> =
        state.Queue.Enqueue(fun () ->
            promise {
                if DispatchOps.isCancelRequested r cancellation then
                    DispatchOps.resolveRecord r Cancelled
                else
                    r.Phase <- TransportStarted
                    eventLogger.Log(DispatchTransportStarted(r.Identity.DispatchId, DispatchOps.getNowMs ()))

                    let awaitedOpt: JS.Promise<DispatchAcceptance> option =
                        try
                            Some(sendPrompt r.Identity)
                        with _ex ->
                            None

                    match awaitedOpt with
                    | None -> DispatchOps.rejectUnknown r "TransportThrew" "sendPrompt threw synchronously"
                    | Some awaited ->
                        let! receipt = SessionDispatcherOps.awaitReceipt awaited r
                        SessionDispatcherOps.applyReceipt r receipt eventLogger
            })
