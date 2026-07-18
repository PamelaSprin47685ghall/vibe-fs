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
        /// Mark CancelRequested and trigger CancelWaiter immediately so the
        /// in-flight transport promise is unblocked.  The physical abort is
        /// fire-and-forget: its result is recorded on the record but does
        /// not block the serial queue.
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

                            match r.CancelWaiter with
                            | Some w -> w ()
                            | None -> ()

                            match r.Phase with
                            | Requested
                            | TransportStarted ->
                                DispatchOps.resolveRecord r Cancelled
                                return CancelledBeforeAcceptance
                            | HostAccepted
                            | RunObserved ->
                                // Fire-and-forget physical abort; do not await
                                // inside the serial queue.
                                physicalAbort ()
                                |> Promise.map (fun ok ->
                                    r.AbortSent <- ok
                                    DispatchOps.resolveRecord r Cancelled)
                                |> ignore

                                return CancelledBeforeAcceptance
                            | Terminal t -> return AlreadyTerminal t
                    | _ -> return AlreadyTerminal Superseded
                })

        /// Mark the active dispatch as completed.
        member this.CompleteByTurn(logicalTurnId: string) : bool =
            match this.State.Active with
            | Some r when r.Identity.LogicalTurnId = logicalTurnId && r.Terminal.IsNone ->
                DispatchOps.resolveRecord r Completed
                true
            | _ -> false

        /// Mark the active dispatch as failed.
        member this.FailByTurn (logicalTurnId: string) (err: ErrorInput) : bool =
            match this.State.Active with
            | Some r when r.Identity.LogicalTurnId = logicalTurnId && r.Terminal.IsNone ->
                DispatchOps.resolveRecord r (Failed err)
                true
            | _ -> false

        /// Force-clear the active dispatch and cancel any in-flight waiter.
        /// Runs outside the SerialQueue: terminal resolution is a synchronous
        /// field assignment + waiter resolve.  The in-flight transport task
        /// is unblocked via CancelWaiter (Promise.race in awaitReceipt).
        member this.OnSessionClosed() : unit =
            match this.State.Active with
            | Some r when r.Terminal.IsNone ->
                r.CancelToken.Cancel()

                match r.CancelWaiter with
                | Some w -> w ()
                | None -> ()

                DispatchOps.resolveRecord r SessionClosed
            | _ -> ()
