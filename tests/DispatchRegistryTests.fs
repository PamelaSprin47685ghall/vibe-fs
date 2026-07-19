module Wanxiangshu.Tests.DispatchRegistryTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Dispatch.Identity
open Wanxiangshu.Kernel.Dispatch.Protocol
open Wanxiangshu.Runtime.Dispatch

/// Verify NotifySessionClosed on the shared registry reaches the
/// dispatcher registered via GetOrCreate, resolves the waiter with
/// SessionClosed, and clears the Active slot.
let notifySessionClosedReachesRegisteredDispatcher () =
    promise {
        let ws = Id.workspaceIdQuick "test-notify-sid"
        let sid = "phys-session-1"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-1" "human-1" ""

        let sendPrompt (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.create (fun _resolve _reject -> ())

        let cancellation = System.Threading.CancellationToken.None
        let outcomeP = dispatcher.Dispatch identity sendPrompt cancellation
        do! Promise.sleep 10
        sharedDispatchRegistry.NotifySessionClosed ws sid
        let! (outcome, _waiter) = outcomeP

        check "registry removed after NotifySessionClosed" (sharedDispatchRegistry.TryGet ws sid |> Option.isNone)

        let isSessionClosed =
            match outcome with
            | Failed(SessionClosed) -> true
            | _ -> false

        check "outcome is SessionClosed" isSessionClosed
        check "dispatcher cleared active" (not dispatcher.HasActive)
    }

/// After NotifySessionClosed clears the Active slot, a second dispatch
/// on the same physical session must succeed (not RejectedBeforeSend).
let dispatchSlotReusableAfterSessionClosed () =
    promise {
        let ws = Id.workspaceIdQuick "test-reuse-sid"
        let sid = "phys-session-reuse"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity1 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-reuse-1" "human-1" ""

        let sendPrompt1 (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.create (fun _resolve _reject -> ())

        let cancellation = System.Threading.CancellationToken.None
        let staleP = dispatcher.Dispatch identity1 sendPrompt1 cancellation
        do! Promise.sleep 10
        sharedDispatchRegistry.NotifySessionClosed ws sid
        let! (staleOutcome, _staleWaiter) = staleP

        check
            "first dispatch SessionClosed"
            (match staleOutcome with
             | Failed(SessionClosed) -> true
             | _ -> false)

        check "slot cleared" (not dispatcher.HasActive)

        // Second dispatch must actually succeed.
        let identity2 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-reuse-2" "human-2" ""

        let mutable resolved = false

        let sendPrompt2 (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            promise {
                resolved <- true
                return UserMessageAccepted "msg-2"
            }

        let! (outcome2, _waiter2) = dispatcher.Dispatch identity2 sendPrompt2 cancellation

        let isAccepted =
            match outcome2 with
            | Accepted(UserMessageAccepted "msg-2") -> true
            | _ -> false

        check "second dispatch accepted (slot reused)" isAccepted
        check "sendPrompt2 was invoked" resolved
        sharedDispatchRegistry.Remove ws sid
    }

/// Verify that when sendPrompt rejects asynchronously, the dispatch
/// resolves to Failed(AcceptanceUnknown) rather than a raw promise
/// rejection, and the Active slot is cleared for reuse.
let sendPromptAsyncRejectionResolvesAsAcceptanceUnknown () =
    promise {
        let ws = Id.workspaceIdQuick "test-reject-sid"
        let sid = "phys-session-reject"
        let logger = InMemoryDispatchEventLogger()

        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-reject" "human-reject" ""

        let sendPrompt (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> = Promise.reject (exn "host exploded")

        let cancellation = System.Threading.CancellationToken.None
        let! (outcome, _waiter) = dispatcher.Dispatch identity sendPrompt cancellation

        let isAcceptanceUnknown =
            match outcome with
            | Failed(AcceptanceUnknown e) -> e.ErrorName = "EmptyReceipt"
            | _ -> false

        check "outcome is Failed(AcceptanceUnknown)" isAcceptanceUnknown
        check "slot cleared after rejection" (not dispatcher.HasActive)

        sharedDispatchRegistry.Remove ws sid
    }

/// Verify that CompleteByTurn before transport resolve prevents
/// acceptRecord from overwriting the terminal state.
let completeByTurnBeforeTransportResolve () =
    promise {
        let ws = Id.workspaceIdQuick "test-complete-sid"
        let sid = "phys-session-complete"
        let logger = InMemoryDispatchEventLogger()

        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-complete" "human-complete" ""

        let mutable transportResolved = false

        let sendPrompt (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            promise {
                do! Promise.sleep 50
                transportResolved <- true
                return UserMessageAccepted "msg-late"
            }

        let cancellation = System.Threading.CancellationToken.None
        let outcomeP = dispatcher.Dispatch identity sendPrompt cancellation

        do! Promise.sleep 10

        let completed = dispatcher.CompleteByTurn "turn-complete"
        check "CompleteByTurn returned true" completed

        let! (outcome, _waiter) = outcomeP

        let isCompleted =
            match outcome with
            | Failed(Completed) -> true
            | _ -> false

        check "outcome is Completed" isCompleted
        check "transport did resolve eventually" transportResolved

        // After CompleteByTurn, the late receipt must NOT overwrite terminal.
        // Phase should remain Terminal Completed, not HostAccepted.
        check "slot cleared after complete" (not dispatcher.HasActive)

        sharedDispatchRegistry.Remove ws sid
    }

let run () =
    promise {
        do! notifySessionClosedReachesRegisteredDispatcher ()
        do! dispatchSlotReusableAfterSessionClosed ()
        do! sendPromptAsyncRejectionResolvesAsAcceptanceUnknown ()
        do! completeByTurnBeforeTransportResolve ()
    }
