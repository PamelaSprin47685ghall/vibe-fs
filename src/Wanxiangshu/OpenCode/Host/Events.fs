namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.TerminalSignal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session
open Wanxiangshu.Host.Ports

module Events =

    /// A fresh terminal source. The product owns the terminal facts; this is
    /// the only adapter-side place that names the product's implementation, and
    /// it names it only to obtain an instance — never to hold one.
    let newTerminalSource () : ISessionTerminalSource = TerminalRegistry()

    let private notifyOne (listener: HostSignal -> Task) (signal: HostSignal) : Task =
        task {
            try
                do! listener signal
            with _ ->
                ()
        }

    /// HNI P-07: host-neutral signal source for OpenCode host events.
    type HostSignalSource() =
        let listeners = ResizeArray<HostSignal -> Task>()
        let syncRoot = obj ()

        member _.Publish(signal: HostSignal) : Task =
            task {
                let snapshot = lock syncRoot (fun () -> listeners.ToArray())

                for listener in snapshot do
                    do! notifyOne listener signal
            }

        interface ISignalSource with
            member _.Subscribe(listener: HostSignal -> Task) : IDisposable =
                lock syncRoot (fun () -> listeners.Add listener)

                { new IDisposable with
                    member _.Dispose() =
                        lock syncRoot (fun () -> listeners.Remove listener |> ignore) }

    /// The one event port.
    ///
    /// The terminal facts, the sticky cache and the listener list live in the
    /// product's terminal registry (`Execution/Session/TerminalRegistry`): the
    /// host does not own a terminal outcome, it only observes one. This port is
    /// the host-facing face of that registry — it holds no second copy of the
    /// sticky cache, of the idempotence cache or of the listener registry, so
    /// there is exactly one truth about a terminal fact.
    ///
    /// The port implements the shared `ISessionTerminalSource` contract; the
    /// product's registry is handed in, never constructed here.
    type HostEventPort(terminalSource: ISessionTerminalSource) =

        interface IEventObservationPort with
            member _.SubscribeTerminalListener(listener) =
                terminalSource.SubscribeTerminalListener(listener)

            member _.SubscribeFutureTerminalListener(listener) =
                terminalSource.SubscribeFutureTerminalListener(listener)

            member _.NotifyTerminal sessionId outcome =
                terminalSource.NotifyTerminal sessionId outcome

        interface ISessionTerminalSource with
            member _.SubscribeTerminal(sessionId, listener) =
                terminalSource.SubscribeTerminal(sessionId, listener)

            member _.SubscribeFutureTerminal(sessionId, listener) =
                terminalSource.SubscribeFutureTerminal(sessionId, listener)

            member _.HasSubscriber(sessionId) = terminalSource.HasSubscriber(sessionId)
