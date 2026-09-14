namespace Wanxiangshu.Strength.Persistence

open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module StrengthDurability =
    val create: store: IEventStore -> StrengthDurabilityPort
