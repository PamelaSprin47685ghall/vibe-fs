namespace Wanxiangshu.Session

open System
open System.Collections.Generic
open Wanxiangshu.Kernel.Identity

/// Process-local pulse that an external user message arrived for a session.
/// Not journaled — wake-only (Phase 4 join interrupt publish).
type ExternalUserIngressPulse =
    { SessionId: SessionId
      PhysicalMessageId: PhysicalUserMessageId }

/// Session-scoped join wait registrations. SignalUserMessage fans out
/// UserMessageArrived to every active JoinInterrupt for that session.
/// When no waiters exist, the pulse is latched once so a later Register
/// still wakes (Signal-before-Register race).
type IJoinInterruptRegistry =
    abstract Register: SessionId * JoinInterrupt -> IDisposable
    abstract SignalUserMessage: SessionId -> unit
    /// Drop waiter list + one-shot latch for a deleted session. Does not signal.
    abstract ClearSession: SessionId -> unit

/// Thread-safe process-local registry (Dictionary + lock).
type JoinInterruptRegistry() =
    let gate = obj ()
    let entries = Dictionary<string, ResizeArray<JoinInterrupt>>()
    // One-shot latch: SignalUserMessage with zero waiters records the session;
    // next Register consumes and signals. Not used for OperatorAbort.
    let pendingUserMessage = HashSet<string>()

    interface IJoinInterruptRegistry with
        member _.Register(sessionId: SessionId, interrupt: JoinInterrupt) : IDisposable =
            let key = SessionId.value sessionId

            // Consume latch outside the lock so trySetResult is not under contention.
            // (Avoid *Pending names: dsl-ownership behaviour-bool gate.)
            if
                lock gate (fun () ->
                    match entries.TryGetValue key with
                    | true, list -> list.Add interrupt
                    | false, _ ->
                        let list = ResizeArray<JoinInterrupt>()
                        list.Add interrupt
                        entries.[key] <- list

                    pendingUserMessage.Remove key)
            then
                interrupt.Signal JoinInterruptReason.UserMessageArrived

            { new IDisposable with
                member _.Dispose() =
                    lock gate (fun () ->
                        match entries.TryGetValue key with
                        | true, list ->
                            list.Remove interrupt |> ignore

                            if list.Count = 0 then
                                entries.Remove key |> ignore
                        | false, _ -> ()) }

        member _.SignalUserMessage(sessionId: SessionId) : unit =
            let key = SessionId.value sessionId

            let targets =
                lock gate (fun () ->
                    match entries.TryGetValue key with
                    | true, list when list.Count > 0 ->
                        // Active waiters: fan-out only; do not leave a latch for a later join.
                        list |> Seq.toList
                    | _ ->
                        pendingUserMessage.Add key |> ignore
                        [])

            // trySetResult is idempotent on an already-completed TCS.
            for interrupt in targets do
                interrupt.Signal JoinInterruptReason.UserMessageArrived

        member _.ClearSession(sessionId: SessionId) : unit =
            let key = SessionId.value sessionId

            lock gate (fun () ->
                entries.Remove key |> ignore
                pendingUserMessage.Remove key |> ignore)
