namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.TerminalSignal

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module Events =

    /// A fresh terminal source. The product owns the terminal facts; this is
    /// the only adapter-side place that names the product's implementation, and
    /// it names it only to obtain an instance — never to hold one.
    val newTerminalSource: unit -> ISessionTerminalSource

    type HostEventPort =
        new: terminalSource: ISessionTerminalSource -> HostEventPort
        interface IEventObservationPort
        interface ISessionTerminalSource
