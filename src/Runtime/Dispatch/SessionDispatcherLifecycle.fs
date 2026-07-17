namespace Wanxiangshu.Runtime.Dispatch

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Dispatch.Events
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Runtime.PromiseQueue

/// Type extensions for `SessionDispatcher`. Marked `[<AutoOpen>]` so every
/// file that opens `Wanxiangshu.Runtime.Dispatch` automatically sees the
/// lifecycle methods without a manual `open`.
[<AutoOpen>]
module SessionDispatcherExtensions =

    type SessionDispatcher with
        /// Persist a "host accepted the prompt" event from a peer source.
        member this.BindHostUserMessage (logicalTurnId: string) (messageId: string) : unit =
            this.State.Queue.Enqueue(fun () ->
                promise {
                    match this.State.Active with
                    | Some r when r.Identity.LogicalTurnId = logicalTurnId && r.Terminal.IsNone ->
                        match r.Phase with
                        | Requested
                        | TransportStarted
                        | HostAccepted
                        | RunObserved ->
                            DispatchOps.observeRun r messageId (DispatchOps.getNowMs ()) this.State.EventLogger
                        | Terminal _ -> ()
                    | _ -> ()
                })
            |> ignore

        /// Try to cancel a dispatch by logical turn id.
        member this.CancelByTurn
            (logicalTurnId: string)
            (physicalAbort: unit -> JS.Promise<bool>)
            : JS.Promise<DispatchCancelResult> =
            this.State.Queue.Enqueue(fun () ->
                promise {
                    match this.State.Active with
                    | Some r when r.Identity.LogicalTurnId = logicalTurnId ->
                        match r.Terminal with
                        | Some t -> return AlreadyTerminal t
                        | None ->
                            r.CancelRequested <- true
                            r.CancelToken.Cancel()

                            match r.Phase with
                            | Requested
                            | TransportStarted ->
                                DispatchOps.resolveRecord r Cancelled
                                return CancelledBeforeAcceptance
                            | HostAccepted
                            | RunObserved ->
                                let! ok = physicalAbort ()
                                r.AbortSent <- ok

                                if ok then
                                    DispatchOps.resolveRecord r Cancelled
                                    return AbortSent
                                else
                                    let err =
                                        { ErrorName = "AbortUnavailable"
                                          DomainError = None
                                          Message = "physical session abort API missing or refused"
                                          StatusCode = None
                                          IsRetryable = Some false }

                                    DispatchOps.resolveRecord r (AbortUnknown err)
                                    return AbortUnavailable
                            | Terminal t -> return AlreadyTerminal t
                    | _ -> return AlreadyTerminal Superseded
                })

        /// Mark the active dispatch as completed.
        member this.CompleteByTurn(logicalTurnId: string) : bool =
            match this.State.Active with
            | Some r when r.Identity.LogicalTurnId = logicalTurnId && r.Terminal.IsNone ->
                this.State.Queue.Enqueue(fun () ->
                    promise {
                        DispatchOps.resolveRecord r Completed
                        return ()
                    })
                |> ignore

                true
            | _ -> false

        /// Mark the active dispatch as failed.
        member this.FailByTurn (logicalTurnId: string) (err: ErrorInput) : bool =
            match this.State.Active with
            | Some r when r.Identity.LogicalTurnId = logicalTurnId && r.Terminal.IsNone ->
                this.State.Queue.Enqueue(fun () ->
                    promise {
                        DispatchOps.resolveRecord r (Failed err)
                        return ()
                    })
                |> ignore

                true
            | _ -> false

        /// Force-clear the active dispatch and cancel any in-flight waiter.
        member this.OnSessionClosed() : unit =
            this.State.Queue.Enqueue(fun () ->
                promise {
                    match this.State.Active with
                    | Some r when r.Terminal.IsNone ->
                        r.CancelToken.Cancel()
                        DispatchOps.resolveRecord r SessionClosed
                    | _ -> ()
                })
            |> ignore
