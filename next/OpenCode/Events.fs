namespace Wanxiangshu.Next.OpenCode

open System
open Wanxiangshu.Next.Kernel.Identity

type TerminalOutcome =
    | Completed of messageId: MessageId
    | Aborted of reason: string
    | Failed of error: string

type TerminalCompletionListener = SessionId -> TerminalOutcome -> unit

type IEventObservationPort =
    abstract SubscribeTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool
    abstract IsCompleted: sessionId: SessionId -> bool

module Events =

    type DeterministicEventPort() =
        let listeners = ResizeArray<TerminalCompletionListener>()
        let completedSessions = System.Collections.Generic.HashSet<SessionId>()
        let lockObj = obj ()

        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                lock lockObj (fun () -> listeners.Add(listener))

                { new IDisposable with
                    member _.Dispose() =
                        lock lockObj (fun () -> listeners.Remove(listener) |> ignore) }

            member _.NotifyTerminal sessionId outcome =
                let handlers =
                    lock lockObj (fun () ->
                        if completedSessions.Contains(sessionId) then
                            []
                        else
                            completedSessions.Add(sessionId) |> ignore
                            listeners |> Seq.toList)

                if List.isEmpty handlers then
                    false
                else
                    for h in handlers do
                        h sessionId outcome

                    true

            member _.IsCompleted sessionId =
                lock lockObj (fun () -> completedSessions.Contains(sessionId))
