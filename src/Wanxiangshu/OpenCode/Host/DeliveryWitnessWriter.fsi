namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Programming.Js.OpenCode

/// The durable writer of one delivery settlement: the js programming surface
/// states the narrow capability, this adapter carries it onto the durable
/// witness fact and commits it through the injected journal.
[<RequireQualifiedAccess>]
module DeliveryWitnessWriter =
    /// The durable witness of one delivery: the js witness's fence and
    /// material, plus the digest that seals the material it states.
    val toWitness: witness: DeliveryWitness -> TaskDeliveryWitness

    /// The writer the settle point uses. A refused commit is reported as a
    /// refused delivery — the caller learns that the delivery is not
    /// established.
    val create: journal: AgentJournal -> IDeliveryWitnessWriter
