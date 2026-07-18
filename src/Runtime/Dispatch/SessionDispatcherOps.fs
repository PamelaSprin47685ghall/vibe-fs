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
    let awaitReceipt (awaited: JS.Promise<DispatchAcceptance>) : JS.Promise<DispatchAcceptance> =
        promise {
            try
                return! awaited
            with _ex ->
                return UserMessageAccepted ""
        }

    let applyReceipt (r: DispatchRecord) (receipt: DispatchAcceptance) (logger: IDispatchEventLogger) : unit =
        match receipt with
        | UserMessageAccepted "" -> DispatchOps.rejectUnknown r "EmptyReceipt" "host returned empty user message id"
        | _ -> DispatchOps.acceptRecord r receipt (DispatchOps.getNowMs ()) logger
