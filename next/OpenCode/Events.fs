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

/// Optional output boundary used to isolate one prompt/run from prior session history.
type IEventOutputBoundaryPort =
    abstract GetSessionOutputWatermark: sessionId: SessionId -> int
    abstract GetSessionOutputSince: sessionId: SessionId * watermark: int -> string list

type IEventObservationPort =
    inherit IEventOutputBoundaryPort
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

        interface IEventOutputBoundaryPort with
            member _.GetSessionOutputWatermark _ = 0
            member _.GetSessionOutputSince(_, _) = []

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

        /// Terminal completions are produced by TerminalPolicies via
        /// NotifyTerminal. Raw host event observation is handled upstream by the
        /// signal stack (HostSignalAdapter / HostSignalSubscribe), so Observe is
        /// a no-op.
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

        interface IEventOutputBoundaryPort with
            member _.GetSessionOutputWatermark sessionId =
                lock lockObj (fun () ->
                    match sessionOutputs.TryGetValue(sessionId) with
                    | true, output -> output.Count
                    | false, _ -> 0)

            member _.GetSessionOutputSince(sessionId, watermark) =
                lock lockObj (fun () ->
                    match sessionOutputs.TryGetValue(sessionId) with
                    | true, output ->
                        let start = max 0 (min watermark output.Count)
                        output |> Seq.skip start |> Seq.toList
                    | false, _ -> [])
