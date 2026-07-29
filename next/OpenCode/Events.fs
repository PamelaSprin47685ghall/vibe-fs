namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity

open Wanxiangshu.Next.Kernel

type TerminalOutcome =
    | Completed of result: AgentRunResult
    | Aborted of reason: string
    | Failed of error: string

type TerminalCompletionListener = SessionId -> TerminalOutcome -> unit

type IEventObservationPort =
    abstract SubscribeTerminalListener: listener: TerminalCompletionListener -> IDisposable
    abstract NotifyTerminal: sessionId: SessionId -> outcome: TerminalOutcome -> bool
    abstract IsCompleted: sessionId: SessionId -> bool
    abstract GetSessionOutput: sessionId: SessionId -> string list

module Events =

    type DeterministicEventPort() =
        let listeners = ResizeArray<TerminalCompletionListener>()
        let lockObj = obj ()

        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                lock lockObj (fun () -> listeners.Add(listener))

                { new IDisposable with
                    member _.Dispose() =
                        lock lockObj (fun () -> listeners.Remove(listener) |> ignore) }

            member _.NotifyTerminal sessionId outcome =
                let handlers = lock lockObj (fun () -> listeners |> Seq.toList)

                if List.isEmpty handlers then
                    false
                else
                    for h in handlers do
                        h sessionId outcome

                    true

            member _.IsCompleted sessionId = false

            member _.GetSessionOutput _ = []

    type HostEventPort() =
        let listeners = ResizeArray<TerminalCompletionListener>()

        let sessionOutputs =
            System.Collections.Generic.Dictionary<SessionId, ResizeArray<string>>()

        let lockObj = obj ()

        let recordOutput sessionId text =
            lock lockObj (fun () ->
                match sessionOutputs.TryGetValue(sessionId) with
                | true, output -> output.Add(text)
                | false, _ ->
                    let output = ResizeArray<string>()
                    output.Add(text)
                    sessionOutputs.[sessionId] <- output)

        let notify sessionId outcome =
            let handlers = lock lockObj (fun () -> listeners |> Seq.toList)

            for handler in handlers do
                handler sessionId outcome

        member _.RecordSessionOutput (sessionId: SessionId) (text: string) =
            if not (String.IsNullOrWhiteSpace text) then
                recordOutput sessionId text

        /// Terminal completions arrive through NotifyTerminal from the reconcile
        /// path. Raw host event observation is handled upstream by the signal stack
        /// (HostSignalAdapter / HostSignalSubscribe), so Observe is a no-op.
        member _.Observe(_rawEvent: obj) = ()

        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                lock lockObj (fun () -> listeners.Add(listener))

                { new IDisposable with
                    member _.Dispose() =
                        lock lockObj (fun () -> listeners.Remove(listener) |> ignore) }

            member _.NotifyTerminal sessionId outcome =
                let hasListeners = lock lockObj (fun () -> listeners.Count > 0)
                notify sessionId outcome
                hasListeners

            member _.IsCompleted sessionId = false

            member _.GetSessionOutput sessionId =
                lock lockObj (fun () ->
                    match sessionOutputs.TryGetValue(sessionId) with
                    | true, output -> output |> Seq.toList
                    | false, _ -> [])
