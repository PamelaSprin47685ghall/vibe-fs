namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.TerminalSignal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

module Events =

    /// A fresh terminal source. The product owns the terminal facts; this is
    /// the only adapter-side place that names the product's implementation, and
    /// it names it only to obtain an instance — never to hold one.
    val newTerminalSource: unit -> ISessionTerminalSource

    /// HNI P-07: host-neutral signal source for OpenCode host events.
    type HostSignalSource =
        new: unit -> HostSignalSource
        member Publish: signal: HostSignal -> Task
        interface ISignalSource

    type HostEventPort =
        new: terminalSource: ISessionTerminalSource -> HostEventPort
        interface IEventObservationPort
        interface ISessionTerminalSource
