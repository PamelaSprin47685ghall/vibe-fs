namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Programming.Js

/// The durable writer of one delivery settlement: the js programming surface
/// states the narrow capability, this adapter carries it onto the durable
/// witness fact and commits it through the injected journal.
[<RequireQualifiedAccess>]
module DeliveryWitnessWriter =

    /// The writer the settle point uses. A refused commit is reported as a
    /// refused delivery — the caller learns that the delivery is not
    /// established. `onCommitted` runs only after the durable commit
    /// succeeded; a refused commit registers nothing.
    val create: journal: AgentJournal -> onCommitted: (DeliveryWitness -> unit) -> IDeliveryWitnessWriter
