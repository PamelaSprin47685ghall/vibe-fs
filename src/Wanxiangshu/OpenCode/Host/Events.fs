namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.TerminalSignal

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session

module Events =

    /// A fresh terminal source. The product owns the terminal facts; this is
    /// the only adapter-side place that names the product's implementation, and
    /// it names it only to obtain an instance — never to hold one.
    let newTerminalSource () : ISessionTerminalSource = TerminalRegistry()

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
