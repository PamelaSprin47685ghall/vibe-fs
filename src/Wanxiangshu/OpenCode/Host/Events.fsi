namespace Wanxiangshu.OpenCode

open System
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

module Events =
    type ListenerRegistration =
        { Listener: TerminalCompletionListener
          mutable Live: bool }

    type HostEventPort =
        new: unit -> HostEventPort
        interface IEventObservationPort
