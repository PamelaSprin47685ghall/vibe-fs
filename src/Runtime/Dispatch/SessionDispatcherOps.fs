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

    /// Await the user-supplied host prompt promise. Asynchronous rejection
    /// is captured and turned into an empty `UserMessageAccepted` so the
    /// caller's `applyReceipt` switches to the `AcceptanceUnknown` terminal.
    /// Synchronous throws are caught at the dispatch site instead.
    /// The result is raced against a cancel promise (CancelWaiter) so that
    /// OnSessionClosed / CancelByTurn can unblock the transport immediately.
    let awaitReceipt (awaited: JS.Promise<DispatchAcceptance>) (r: DispatchRecord) : JS.Promise<DispatchAcceptance> =
        let cancelPromise: JS.Promise<DispatchAcceptance option> =
            Promise.create (fun resolve _ -> r.CancelWaiter <- Some(fun () -> resolve None))

        let safeAwaited: JS.Promise<DispatchAcceptance option> =
            promise {
                try
                    let! r = awaited
                    return Some r
                with _ ->
                    return Some(UserMessageAccepted "")
            }

        promise {
            let! result = Promise.race [| safeAwaited; cancelPromise |]

            match result with
            | Some receipt -> return receipt
            | None -> return UserMessageAccepted ""
        }

    let applyReceipt (r: DispatchRecord) (receipt: DispatchAcceptance) (logger: IDispatchEventLogger) : unit =
        match receipt with
        | UserMessageAccepted "" -> DispatchOps.rejectUnknown r "EmptyReceipt" "host returned empty user message id"
        | _ -> DispatchOps.acceptRecord r receipt (DispatchOps.getNowMs ()) logger
