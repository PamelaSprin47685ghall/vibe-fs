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
        ///
        /// Domain cancel is turn-scoped; host abort is physical-session-scoped.
        /// Physical abort runs only after a second actor re-entry confirms:
        /// active dispatch id, generation, physical ownership, abort-not-sent,
        /// and session not closed. Stale aborts are logged and never reach the host.
        member this.CancelByTurn
            (logicalTurnId: string)
            (physicalAbort: unit -> JS.Promise<bool>)
            : JS.Promise<DispatchCancelResult> =
            promise {
                let logStaleEarly (dispatchId: DispatchId) (reason: string) =
                    this.State.EventLogger.Log(
                        DispatchStaleAbort(dispatchId, logicalTurnId, reason, DispatchOps.getNowMs ())
                    )

                let! phase1 =
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
                                        return Choice1Of2 CancelledBeforeAcceptance
                                    | HostAccepted
                                    | RunObserved ->
                                        if r.AbortSent then
                                            logStaleEarly r.Identity.DispatchId "abort_already_sent"
                                            return Choice1Of2(AlreadyTerminal Cancelled)
                                        else
                                            let capturedGeneration = this.State.Generation
                                            let capturedDispatchId = r.Identity.DispatchId
                                            return Choice2Of2(r, capturedGeneration, capturedDispatchId)
                                    | Terminal t -> return Choice1Of2(AlreadyTerminal t)
                                | Some other ->
                                    logStaleEarly other.Identity.DispatchId "superseded_by_new_turn"
                                    return Choice1Of2(AlreadyTerminal Superseded)
                                | None ->
                                    logStaleEarly
                                        (DispatchId.create ("stale:" + logicalTurnId))
                                        "slot_empty"
                                    return Choice1Of2(AlreadyTerminal Superseded)
                        })

                match phase1 with
                | Choice1Of2 result -> return result
                | Choice2Of2(target, generation, dispatchId) ->
                    // Hold the mailbox across ownership re-check AND host abort so a
                    // new turn cannot Reserve the physical session mid-abort (S-08).
                    return!
                        this.State.Queue.Enqueue(fun () ->
                            promise {
                                let logStale (reason: string) =
                                    this.State.EventLogger.Log(
                                        DispatchStaleAbort(
                                            dispatchId,
                                            logicalTurnId,
                                            reason,
                                            DispatchOps.getNowMs ()
                                        )
                                    )

                                if this.State.IsClosed then
                                    logStale "session_closed"
                                    return AlreadyTerminal SessionClosed
                                else
                                    match this.State.Active with
                                    | Some current
                                        when obj.ReferenceEquals(current, target)
                                             && current.Identity.LogicalTurnId = logicalTurnId
                                             && current.Identity.DispatchId = dispatchId
                                             && current.Terminal.IsNone
                                             && not current.AbortSent
                                             && this.State.Generation = generation ->
                                        let! abortSucceeded =
                                            promise {
                                                try
                                                    return! physicalAbort ()
                                                with _ ->
                                                    return false
                                            }

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
                                    | Some current when current.AbortSent
                                                        && obj.ReferenceEquals(current, target) ->
                                        logStale "abort_already_sent"
                                        return AlreadyTerminal Cancelled
                                    | Some _ ->
                                        logStale "superseded_by_new_turn"
                                        return AlreadyTerminal Superseded
                                    | None ->
                                        match target.Terminal with
                                        | Some t ->
                                            logStale "target_already_terminal"
                                            return AlreadyTerminal t
                                        | None ->
                                            logStale "slot_empty"
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
