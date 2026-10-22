namespace Wanxiangshu.Process

open System
open System.Threading.Tasks

/// Process table for one programming call. `Start` begins one command and
/// returns a ticket. `Wait` resolves when it exits, with only the output
/// since the previous drain. `Signal` and `Send` address that ticket.
/// `TakeDelta` drains unread output of every still-running command.
module JsCallLoop =
    type Host =
        interface IDisposable
        new: root: string -> Host
        member Start: command: obj -> obj
        member Wait: ticket: obj -> Task<obj>
        member SignalParked: signal: obj -> Task<obj>
        member SendParked: input: obj -> Task<obj>
        member TakeDelta: unit -> obj
        member DisposeAsync: unit -> Task<unit>
