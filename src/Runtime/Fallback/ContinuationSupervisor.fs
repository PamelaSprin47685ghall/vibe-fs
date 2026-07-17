module Wanxiangshu.Runtime.Fallback.ContinuationSupervisor

open Fable.Core
open Wanxiangshu.Kernel.Fallback.Continuation
open Wanxiangshu.Runtime.Fallback.ContinuationHost
open Wanxiangshu.Runtime.PromiseQueue

let private hostIdentityToReceipt
    (request: ContinuationRequest)
    (identity: ContinuationHostIdentity)
    : HostDispatchReceipt option =
    match identity with
    | ContinuationHostIdentity.AwaitingUserMessage -> None
    | ContinuationHostIdentity.UserMessageIdentity userMessageId ->
        Some(HostDispatchReceipt.UserMessageAccepted userMessageId)
    | ContinuationHostIdentity.RunIdentity runId -> Some(HostDispatchReceipt.RunAccepted runId)
    | ContinuationHostIdentity.OpaqueIdentity receiptId -> Some(HostDispatchReceipt.OpaqueAccepted receiptId)

let private mapReceiptToCommand
    (request: ContinuationRequest)
    (receipt: HostDispatchReceipt)
    : ContinuationCommand option =
    match receipt with
    | HostDispatchReceipt.UserMessageAccepted messageId ->
        Some(ContinuationCommand.HostUserMessageObserved(request.ContinuationId, messageId))
    | HostDispatchReceipt.RunAccepted runId -> Some(ContinuationCommand.RunStarted(request.ContinuationId, runId))
    | HostDispatchReceipt.OpaqueAccepted _ ->
        // Host accepted the dispatch but cannot yet identify the concrete
        // message/run. The chat.message / reconcile path will supply the
        // real identity later.
        None

/// Supervises the outbox effects produced by `ContinuationCommandProcessor`.
/// All host interactions are asynchronous and post follow-up commands through
/// the supplied `postCommand` callback. The processor remains authoritative
/// for state; the supervisor only performs I/O and reports outcomes.
type ContinuationSupervisor(host: IContinuationHost, postCommand: ContinuationCommand -> JS.Promise<unit>) =
    let effectQueue = SerialQueue()

    let dispatchEffect (req: ContinuationRequest, _effectId: string) : JS.Promise<unit> =
        promise {
            try
                let! receipt = host.Dispatch req

                match mapReceiptToCommand req receipt with
                | Some cmd -> do! postCommand cmd
                | None -> ()
            with ex ->
                do!
                    postCommand (ContinuationCommand.Fail(req.ContinuationId, ex.Message))
                    |> Promise.catch (fun _ -> ())
        }

    let abortEffect
        (req: ContinuationRequest)
        (identity: ContinuationHostIdentity)
        (terminalEvent: ContinuationEvent)
        : JS.Promise<unit> =
        promise {
            try
                match hostIdentityToReceipt req identity with
                | Some receipt ->
                    let! ok = host.TryAbortOwned(req, receipt)

                    if ok then
                        do! postCommand (ContinuationCommand.HostAbortConfirmed(req.ContinuationId, terminalEvent))
                    else
                        do! postCommand (ContinuationCommand.Fail(req.ContinuationId, "host_abort_refused"))
                | None ->
                    // No concrete host artifact yet; abort the continuation locally.
                    do! postCommand (ContinuationCommand.HostAbortConfirmed(req.ContinuationId, terminalEvent))
            with ex ->
                do!
                    postCommand (ContinuationCommand.Fail(req.ContinuationId, ex.Message))
                    |> Promise.catch (fun _ -> ())
        }

    let reconcileEffect (req: ContinuationRequest) : JS.Promise<unit> =
        promise {
            try
                let! receiptOpt = host.Reconcile req

                match receiptOpt with
                | Some receipt ->
                    match mapReceiptToCommand req receipt with
                    | Some cmd -> do! postCommand cmd
                    | None -> ()
                | None ->
                    // No matching host message found yet. Reconciliation may be
                    // retried later by an external timer.
                    ()
            with ex ->
                do!
                    postCommand (ContinuationCommand.Fail(req.ContinuationId, ex.Message))
                    |> Promise.catch (fun _ -> ())
        }

    let handleEffect (effect: ContinuationEffect) : JS.Promise<unit> =
        match effect with
        | ContinuationEffect.DispatchContinuation(req, effectId) -> dispatchEffect (req, effectId)
        | ContinuationEffect.AbortContinuation(req, identity, terminalEvent) -> abortEffect req identity terminalEvent
        | ContinuationEffect.ReconcileContinuation req -> reconcileEffect req

    member _.OnEffect(effect: ContinuationEffect) : unit =
        effectQueue.Enqueue(fun () -> handleEffect effect) |> ignore
