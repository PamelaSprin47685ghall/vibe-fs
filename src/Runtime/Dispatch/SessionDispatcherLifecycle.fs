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
        /// in-flight transport promise is unblocked.  Physical abort is invoked
        /// inside the mailbox after turn/generation/session-open validation; its
        /// result is committed only after the mailbox re-confirms the same
        /// dispatch still owns the session.
        member this.CancelByTurn
            (logicalTurnId: string)
            (physicalAbort: unit -> JS.Promise<bool>)
            : JS.Promise<DispatchCancelResult> =
            promise {
                let! decision =
                    this.State.Queue.Enqueue(fun () ->
                        promise {
                            if this.State.IsClosed then
                                return Choice1Of2(AlreadyTerminal SessionClosed)
                            else
                                match this.State.Active with
                                | Some r when
                                    r.Identity.LogicalTurnId = logicalTurnId
                                    && r.Terminal.IsNone
                                    ->
                                    r.CancelRequested <- true
                                    r.CancelToken.Cancel()

                                    match r.CancelWaiter with
                                    | Some w -> w ()
                                    | None -> ()

                                    match r.Phase with
                                    | Requested
                                    | TransportStarted ->
                                        DispatchOps.resolveRecord r Cancelled
                                        return Choice1Of2(CancelledBeforeAcceptance)
                                    | HostAccepted
                                    | RunObserved ->
                                        let capturedGeneration = this.State.Generation
                                        // physicalAbort is invoked inside the mailbox
                                        // after active/turn/session-open re-validation.
                                        let abortP = physicalAbort ()
                                        let payload = (r, capturedGeneration, abortP)
                                        return Choice2Of2 payload
                                    | Terminal t -> return Choice1Of2(AlreadyTerminal t)
                                | _ -> return Choice1Of2(AlreadyTerminal Superseded)
                        })

                match decision with
                | Choice1Of2 result -> return result
                | Choice2Of2 (target, generation, abortP) ->
                    let! abortSucceeded =
                        promise {
                            try
                                return! abortP
                            with _ ->
                                return false
                        }

                    return!
                        this.State.Queue.Enqueue(fun () ->
                            promise {
                                if this.State.IsClosed then
                                    return AlreadyTerminal SessionClosed
                                else
                                    match this.State.Active with
                                    | Some current
                                        when obj.ReferenceEquals(current, target)
                                             && current.Identity.LogicalTurnId = logicalTurnId
                                             && current.Terminal.IsNone
                                             && this.State.Generation = generation ->
                                        if abortSucceeded then
                                            current.AbortSent <- true
                                            DispatchOps.resolveRecord current Cancelled
                                            return AbortSent
                                        else
                                            let err =
                                                { ErrorName = "AbortUnavailable"
                                                  DomainError = None
                                                  Message = "Host abort did not confirm termination"
                                                  StatusCode = None
                                                  IsRetryable = Some false }

                                            DispatchOps.resolveRecord current (AbortUnknown err)
                                            return AbortUnavailable
                                    | Some _ ->
                                        // A different turn now owns the slot.
                                        return AlreadyTerminal Superseded
                                    | None ->
                                        match target.Terminal with
                                        | Some t -> return AlreadyTerminal t
                                        | None ->
                                            // The slot is empty and the target
                                            // was never terminal; treat as superseded.
                                            return AlreadyTerminal Superseded
                            })
            }

        /// Mark the active dispatch as completed. Runs inside the serial
        /// mailbox so terminal resolution is ordered relative to Dispatch,
        /// CancelByTurn, and OnSessionClosed.
        member this.CompleteByTurn(logicalTurnId: string) : JS.Promise<bool> =
            this.State.Queue.Enqueue(fun () ->
                promise {
                    match this.State.Active with
                    | Some r when r.Identity.LogicalTurnId = logicalTurnId && r.Terminal.IsNone ->
                        DispatchOps.resolveRecord r Completed
                        return true
                    | _ -> return false
                })

        /// Mark the active dispatch as failed. Runs inside the serial mailbox
        /// so terminal resolution is ordered relative to Dispatch,
        /// CancelByTurn, and OnSessionClosed.
        member this.FailByTurn (logicalTurnId: string) (err: ErrorInput) : JS.Promise<bool> =
            this.State.Queue.Enqueue(fun () ->
                promise {
                    match this.State.Active with
                    | Some r when r.Identity.LogicalTurnId = logicalTurnId && r.Terminal.IsNone ->
                        DispatchOps.resolveRecord r (Failed err)
                        return true
                    | _ -> return false
                })

        /// Mark the session as closed and force-clear the active dispatch.
        /// Runs inside the serial mailbox so the closed flag and terminal
        /// resolution are ordered relative to Dispatch, Reserve, and
        /// CancelByTurn. The in-flight transport task is unblocked via
        /// CancelWaiter (Promise.race in awaitReceipt).
        member this.OnSessionClosed() : JS.Promise<unit> =
            this.State.Queue.Enqueue(fun () ->
                promise {
                    this.State.IsClosed <- true

                    match this.State.Active with
                    | Some r when r.Terminal.IsNone ->
                        r.CancelToken.Cancel()

                        match r.CancelWaiter with
                        | Some w -> w ()
                        | None -> ()

                        DispatchOps.resolveRecord r SessionClosed
                    | _ -> ()
                })
