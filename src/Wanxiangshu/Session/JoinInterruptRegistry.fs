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
type IJoinInterruptRegistry =
    abstract Register: SessionId * JoinInterrupt -> IDisposable
    abstract SignalUserMessage: SessionId -> unit

/// Thread-safe process-local registry (Dictionary + lock).
type JoinInterruptRegistry() =
    let gate = obj ()
    let entries = Dictionary<string, ResizeArray<JoinInterrupt>>()

    interface IJoinInterruptRegistry with
        member _.Register(sessionId: SessionId, interrupt: JoinInterrupt) : IDisposable =
            let key = SessionId.value sessionId

            lock gate (fun () ->
                match entries.TryGetValue key with
                | true, list -> list.Add interrupt
                | false, _ ->
                    let list = ResizeArray<JoinInterrupt>()
                    list.Add interrupt
                    entries.[key] <- list)

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
                    | true, list -> list |> Seq.toList
                    | false, _ -> [])

            // trySetResult is idempotent on an already-completed TCS.
            for interrupt in targets do
                interrupt.Signal JoinInterruptReason.UserMessageArrived
