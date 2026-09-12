namespace Wanxiangshu.Strength.Persistence

open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module StrengthDurability =
    val setFatalTripHandler: handler: (string -> string -> unit) -> unit

    val create: store: IEventStore -> StrengthDurabilityPort
