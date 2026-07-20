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

        // NotifySessionClosed closes the old dispatcher; the registry recreates
        // a fresh dispatcher for the same physical session.
        let dispatcher2 = sharedDispatchRegistry.GetOrCreate ws sid logger

        let! (outcome2, _waiter2) = dispatcher2.Dispatch identity2 sendPrompt2 cancellation

        let isAccepted =
            match outcome2 with
            | Accepted(UserMessageAccepted "msg-2") -> true
            | _ -> false

        check "second dispatch accepted (slot reused via new dispatcher)" isAccepted
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
        let mutable resolveTransport = fun () -> ()
        let transportResolvedP =
            Promise.create (fun resolve _ -> resolveTransport <- resolve)

        let sendPrompt (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            promise {
                do! Promise.sleep 50
                transportResolved <- true
                resolveTransport ()
                return UserMessageAccepted "msg-late"
            }

        let cancellation = System.Threading.CancellationToken.None
        let outcomeP = dispatcher.Dispatch identity sendPrompt cancellation

        do! Promise.sleep 10

        let! completed = dispatcher.CompleteByTurn "turn-complete"
        check "CompleteByTurn returned true" completed

        let! (outcome, _waiter) = outcomeP

        let isCompleted =
            match outcome with
            | Failed(Completed) -> true
            | _ -> false

        check "outcome is Completed" isCompleted
        do! transportResolvedP
        check "transport did resolve eventually" transportResolved

        // After CompleteByTurn, the late receipt must NOT overwrite terminal.
        // Phase should remain Terminal Completed, not HostAccepted.
        check "slot cleared after complete" (not dispatcher.HasActive)

        sharedDispatchRegistry.Remove ws sid
    }

/// A second dispatch on the same physical session while another turn is still
/// in flight must be rejected with AnotherDispatchInFlight, without disturbing
/// the active turn.
let samePhysicalSessionReentryRejected () =
    promise {
        let ws = Id.workspaceIdQuick "test-overlap-ws"
        let sid = "phys-session-overlap"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity1 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-overlap-1" "human-1" ""

        let sendPrompt1 (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.create (fun _resolve _reject -> ())

        let cancellation = System.Threading.CancellationToken.None
        let firstP = dispatcher.Dispatch identity1 sendPrompt1 cancellation

        let identity2 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-overlap-2" "human-2" ""

        let mutable secondSendCalled = false

        let sendPrompt2 (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            promise {
                secondSendCalled <- true
                return UserMessageAccepted "msg-overlap-2"
            }

        let! (outcome2, _waiter2) = dispatcher.Dispatch identity2 sendPrompt2 cancellation

        let isRejectedBeforeSend =
            match outcome2 with
            | Failed(RejectedBeforeSend e) when e.ErrorName = "AnotherDispatchInFlight" -> true
            | _ -> false

        check "second dispatch rejected with AnotherDispatchInFlight" isRejectedBeforeSend
        check "second sendPrompt was not invoked" (not secondSendCalled)
        check "first dispatch still active" dispatcher.HasActive

        sharedDispatchRegistry.NotifySessionClosed ws sid

        let! (outcome1, _waiter1) = firstP

        check
            "first dispatch resolved to SessionClosed"
            (match outcome1 with
             | Failed(SessionClosed) -> true
             | _ -> false)
    }

/// Cancellation after the host has accepted the prompt must issue a physical
/// abort; when the abort confirms, the result is AbortSent and the slot frees.
let hostAcceptedAbortSucceeds () =
    promise {
        let ws = Id.workspaceIdQuick "test-abort-ok-ws"
        let sid = "phys-session-abort-ok"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity1 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-abort-ok" "human-1" ""

        let sendPrompt1 (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.lift (UserMessageAccepted "msg-abort-ok")

        let cancellation = System.Threading.CancellationToken.None
        let! (outcome1, _waiter1) = dispatcher.Dispatch identity1 sendPrompt1 cancellation

        check
            "dispatch reached HostAccepted"
            (match outcome1 with
             | Accepted(UserMessageAccepted "msg-abort-ok") -> true
             | _ -> false)

        check "slot remains active after acceptance" dispatcher.HasActive

        let! cancelResult = dispatcher.CancelByTurn "turn-abort-ok" (fun () -> Promise.lift true)

        check "cancel result is AbortSent" (cancelResult = AbortSent)
        check "slot cleared after successful abort" (not dispatcher.HasActive)

        let identity2 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-abort-ok-next" "human-2" ""

        let! (outcome2, _waiter2) =
            dispatcher.Dispatch identity2 (fun _ -> Promise.lift (UserMessageAccepted "msg-abort-ok-next")) cancellation

        check
            "new turn accepted after abort"
            (match outcome2 with
             | Accepted(UserMessageAccepted "msg-abort-ok-next") -> true
             | _ -> false)

        sharedDispatchRegistry.NotifySessionClosed ws sid
    }

/// Cancellation after HostAccepted where the physical abort does not confirm
/// must report AbortUnavailable and still free the slot for a new turn.
let hostAcceptedAbortFails () =
    promise {
        let ws = Id.workspaceIdQuick "test-abort-fail-ws"
        let sid = "phys-session-abort-fail"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity1 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-abort-fail" "human-1" ""

        let sendPrompt1 (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.lift (UserMessageAccepted "msg-abort-fail")

        let cancellation = System.Threading.CancellationToken.None
        let! (outcome1, _waiter1) = dispatcher.Dispatch identity1 sendPrompt1 cancellation

        check
            "dispatch reached HostAccepted"
            (match outcome1 with
             | Accepted(UserMessageAccepted "msg-abort-fail") -> true
             | _ -> false)

        check "slot remains active after acceptance" dispatcher.HasActive

        let! cancelResult = dispatcher.CancelByTurn "turn-abort-fail" (fun () -> Promise.lift false)

        check "cancel result is AbortUnavailable" (cancelResult = AbortUnavailable)
        check "slot cleared after failed abort" (not dispatcher.HasActive)

        let identity2 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-abort-fail-next" "human-2" ""

        let! (outcome2, _waiter2) =
            dispatcher.Dispatch identity2 (fun _ -> Promise.lift (UserMessageAccepted "msg-abort-fail-next")) cancellation

        check
            "new turn accepted after failed abort"
            (match outcome2 with
             | Accepted(UserMessageAccepted "msg-abort-fail-next") -> true
             | _ -> false)

        sharedDispatchRegistry.NotifySessionClosed ws sid
    }

/// S-08: host abort holds the session mailbox. A competing Dispatch must wait
/// until the abort finishes, so a delayed abort can never kill a newer turn.
let abortHoldsMailboxUntilHostReturns () =
    promise {
        let ws = Id.workspaceIdQuick "test-abort-holds-ws"
        let sid = "phys-session-abort-holds"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity1 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-holds" "human-1" ""

        let sendPrompt1 (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.lift (UserMessageAccepted "msg-holds")

        let cancellation = System.Threading.CancellationToken.None
        let! (outcome1, _waiter1) = dispatcher.Dispatch identity1 sendPrompt1 cancellation

        check
            "dispatch reached HostAccepted"
            (match outcome1 with
             | Accepted(UserMessageAccepted "msg-holds") -> true
             | _ -> false)

        let mutable resolveAbortStarted = fun () -> ()
        let abortStartedP = Promise.create (fun resolve _ -> resolveAbortStarted <- resolve)
        let mutable abortResolve: (bool -> unit) option = None
        let mutable abortCalls = 0

        let physicalAbort () : JS.Promise<bool> =
            promise {
                abortCalls <- abortCalls + 1
                resolveAbortStarted ()
                let! result = Promise.create (fun resolve _ -> abortResolve <- Some resolve)
                return result
            }

        let cancelP = dispatcher.CancelByTurn "turn-holds" physicalAbort
        do! abortStartedP

        // While abort holds the mailbox, CompleteByTurn and a new Dispatch stay queued.
        let completeP = dispatcher.CompleteByTurn "turn-holds"

        let identity2 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-holds-next" "human-2" ""

        let mutable nextSendCalled = false

        let nextP =
            dispatcher.Dispatch
                identity2
                (fun _ ->
                    nextSendCalled <- true
                    Promise.lift (UserMessageAccepted "msg-holds-next"))
                cancellation

        do! Promise.sleep 20
        check "physical abort issued once while holding mailbox" (abortCalls = 1)
        check "new turn sendPrompt not invoked during abort" (not nextSendCalled)
        check "old turn still owns slot during abort" (dispatcher.ActiveLogicalTurnId = Some "turn-holds")

        match abortResolve with
        | Some resolve -> resolve true
        | None -> ()

        let! cancelResult = cancelP
        check "cancel result is AbortSent" (cancelResult = AbortSent)

        let! completed = completeP
        check "CompleteByTurn loses the race to AbortSent" (not completed)

        let! (outcome2, _waiter2) = nextP

        check
            "new turn accepted only after abort released mailbox"
            (match outcome2 with
             | Accepted(UserMessageAccepted "msg-holds-next") -> true
             | _ -> false)

        check "new turn sendPrompt invoked after abort" nextSendCalled
        check "new turn owns the active slot" (dispatcher.ActiveLogicalTurnId = Some "turn-holds-next")
        check "host abort called exactly once" (abortCalls = 1)

        let! _ = dispatcher.CompleteByTurn "turn-holds-next"
        sharedDispatchRegistry.Remove ws sid
    }

/// S-08: cancel for a prior turn after a new turn owns the physical session
/// must not call host abort; it logs DispatchStaleAbort and reports Superseded.
let staleAbortAfterNewTurnIsLogOnly () =
    promise {
        let ws = Id.workspaceIdQuick "test-stale-abort-ws"
        let sid = "phys-session-stale-abort"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity1 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-stale" "human-1" ""

        let! (outcome1, _waiter1) =
            dispatcher.Dispatch
                identity1
                (fun _ -> Promise.lift (UserMessageAccepted "msg-stale"))
                System.Threading.CancellationToken.None

        check
            "first dispatch HostAccepted"
            (match outcome1 with
             | Accepted(UserMessageAccepted "msg-stale") -> true
             | _ -> false)

        let! completed = dispatcher.CompleteByTurn "turn-stale"
        check "old turn completed" completed

        let identity2 =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-stale-next" "human-2" ""

        let! (outcome2, _waiter2) =
            dispatcher.Dispatch
                identity2
                (fun _ -> Promise.lift (UserMessageAccepted "msg-stale-next"))
                System.Threading.CancellationToken.None

        check
            "new turn HostAccepted"
            (match outcome2 with
             | Accepted(UserMessageAccepted "msg-stale-next") -> true
             | _ -> false)

        let mutable abortCalled = false

        let physicalAbort () : JS.Promise<bool> =
            abortCalled <- true
            Promise.lift true

        let! cancelResult = dispatcher.CancelByTurn "turn-stale" physicalAbort

        check "stale abort reports AlreadyTerminal Superseded" (cancelResult = AlreadyTerminal Superseded)
        check "physical abort was not called for stale turn" (not abortCalled)
        check "new turn still owns the active slot" (dispatcher.ActiveLogicalTurnId = Some "turn-stale-next")

        let staleEvents =
            logger.Events
            |> List.choose (function
                | Wanxiangshu.Kernel.Dispatch.Events.DispatchStaleAbort(_, turnId, reason, _) -> Some(turnId, reason)
                | _ -> None)

        check "stale abort event emitted" (staleEvents.Length = 1)
        check "stale abort names the old turn" (fst staleEvents.Head = "turn-stale")

        let! _ = dispatcher.CompleteByTurn "turn-stale-next"
        sharedDispatchRegistry.Remove ws sid
    }

/// Duplicate CancelByTurn after AbortSent must not re-issue host abort.
let duplicateAbortAfterAbortSentIsIgnored () =
    promise {
        let ws = Id.workspaceIdQuick "test-dup-abort-ws"
        let sid = "phys-session-dup-abort"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-dup" "human-1" ""

        let! (outcome, _waiter) =
            dispatcher.Dispatch
                identity
                (fun _ -> Promise.lift (UserMessageAccepted "msg-dup"))
                System.Threading.CancellationToken.None

        check
            "dispatch HostAccepted"
            (match outcome with
             | Accepted(UserMessageAccepted "msg-dup") -> true
             | _ -> false)

        let mutable abortCalls = 0

        let physicalAbort () : JS.Promise<bool> =
            abortCalls <- abortCalls + 1
            Promise.lift true

        let! first = dispatcher.CancelByTurn "turn-dup" physicalAbort
        check "first cancel AbortSent" (first = AbortSent)
        check "host abort once" (abortCalls = 1)

        let! second = dispatcher.CancelByTurn "turn-dup" physicalAbort
        check "second cancel AlreadyTerminal" (match second with | AlreadyTerminal _ -> true | _ -> false)
        check "host abort still once" (abortCalls = 1)

        sharedDispatchRegistry.Remove ws sid
    }

/// If CancelByTurn is called after the turn already completed, it must report
/// AlreadyTerminal Superseded and must not issue a physical abort.
let cancelAfterCompleteReportsSuperseded () =
    promise {
        let ws = Id.workspaceIdQuick "test-cancel-after-complete-ws"
        let sid = "phys-session-cancel-after-complete"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-complete-cancel" "human-1" ""

        let sendPrompt (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.lift (UserMessageAccepted "msg-complete-cancel")

        let! (outcome, _waiter) = dispatcher.Dispatch identity sendPrompt System.Threading.CancellationToken.None

        check
            "dispatch reached HostAccepted"
            (match outcome with
             | Accepted(UserMessageAccepted "msg-complete-cancel") -> true
             | _ -> false)

        check "slot remains active after acceptance" dispatcher.HasActive

        let! completed = dispatcher.CompleteByTurn "turn-complete-cancel"
        check "CompleteByTurn returned true" completed
        check "slot cleared after complete" (not dispatcher.HasActive)

        let mutable abortCalled = false

        let physicalAbort () : JS.Promise<bool> =
            abortCalled <- true
            Promise.lift true

        let! cancelResult = dispatcher.CancelByTurn "turn-complete-cancel" physicalAbort

        check "cancel result is AlreadyTerminal Superseded" (cancelResult = AlreadyTerminal Superseded)
        check "physical abort was not called after complete" (not abortCalled)

        sharedDispatchRegistry.Remove ws sid
    }

/// If the session is closed before the abort is attempted, CancelByTurn must
/// return AlreadyTerminal SessionClosed and must not call physicalAbort.
let sessionClosedBeforeAbortIsRejectedWithoutPhysicalAbort () =
    promise {
        let ws = Id.workspaceIdQuick "test-close-before-abort-ws"
        let sid = "phys-session-close-before-abort"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-close-before-abort" "human-1" ""

        let sendPrompt (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.lift (UserMessageAccepted "msg-close-before-abort")

        let cancellation = System.Threading.CancellationToken.None
        let! (outcome, _waiter) = dispatcher.Dispatch identity sendPrompt cancellation

        check
            "dispatch reached HostAccepted"
            (match outcome with
             | Accepted(UserMessageAccepted "msg-close-before-abort") -> true
             | _ -> false)

        do! dispatcher.OnSessionClosed()
        let mutable abortCalled = false

        let physicalAbort () : JS.Promise<bool> =
            abortCalled <- true
            Promise.lift true

        let! cancelResult = dispatcher.CancelByTurn "turn-close-before-abort" physicalAbort

        check "cancel result is AlreadyTerminal SessionClosed" (cancelResult = AlreadyTerminal SessionClosed)
        check "physical abort was not called after session closed" (not abortCalled)

        sharedDispatchRegistry.Remove ws sid
    }

/// Session close queued while host abort holds the mailbox runs after abort.
/// Abort still owns the turn at issue time, so host abort is legitimate (AbortSent);
/// close then clears residual state without a second host call.
let sessionCloseQueuedDuringAbortRunsAfterAbortSent () =
    promise {
        let ws = Id.workspaceIdQuick "test-close-during-abort-ws"
        let sid = "phys-session-close-during-abort"
        let logger = InMemoryDispatchEventLogger()
        let dispatcher = sharedDispatchRegistry.GetOrCreate ws sid logger

        let identity =
            DispatchIdentity.newId ws sid DispatchKind.SubsessionTurn 0 0 0 "turn-close-during-abort" "human-1" ""

        let sendPrompt (_: DispatchIdentity) : JS.Promise<DispatchAcceptance> =
            Promise.lift (UserMessageAccepted "msg-close-during-abort")

        let cancellation = System.Threading.CancellationToken.None
        let! (outcome, _waiter) = dispatcher.Dispatch identity sendPrompt cancellation

        check
            "dispatch reached HostAccepted"
            (match outcome with
             | Accepted(UserMessageAccepted "msg-close-during-abort") -> true
             | _ -> false)

        let mutable resolveAbortStarted = fun () -> ()
        let abortStartedP = Promise.create (fun resolve _ -> resolveAbortStarted <- resolve)
        let mutable abortResolve: (bool -> unit) option = None
        let mutable abortCalls = 0

        let physicalAbort () : JS.Promise<bool> =
            promise {
                abortCalls <- abortCalls + 1
                resolveAbortStarted ()
                let! result = Promise.create (fun resolve _ -> abortResolve <- Some resolve)
                return result
            }

        let cancelP = dispatcher.CancelByTurn "turn-close-during-abort" physicalAbort
        do! abortStartedP

        let closeP = dispatcher.OnSessionClosed()

        match abortResolve with
        | Some resolve -> resolve true
        | None -> ()

        let! cancelResult = cancelP
        do! closeP

        check "cancel result is AbortSent (owned turn at issue time)" (cancelResult = AbortSent)
        check "host abort called once" (abortCalls = 1)
        check "slot cleared after session close" (not dispatcher.HasActive)

        sharedDispatchRegistry.Remove ws sid
    }

let run () =
    promise {
        do! notifySessionClosedReachesRegisteredDispatcher ()
        do! dispatchSlotReusableAfterSessionClosed ()
        do! sendPromptAsyncRejectionResolvesAsAcceptanceUnknown ()
        do! completeByTurnBeforeTransportResolve ()
        do! samePhysicalSessionReentryRejected ()
        do! hostAcceptedAbortSucceeds ()
        do! hostAcceptedAbortFails ()
        do! abortHoldsMailboxUntilHostReturns ()
        do! staleAbortAfterNewTurnIsLogOnly ()
        do! duplicateAbortAfterAbortSentIsIgnored ()
        do! cancelAfterCompleteReportsSuperseded ()
        do! sessionClosedBeforeAbortIsRejectedWithoutPhysicalAbort ()
        do! sessionCloseQueuedDuringAbortRunsAfterAbortSent ()
    }
