namespace Wanxiangshu.Execution.Delegation.Advisor

open Fable.Core

/// Process-live record of material successfully delivered to each advisor seat.
/// The ledger law (per-caller isolation, monotone record, identity = representative plus
/// exact rendered material) is owned by `wanxiangli/advisor` (CUT-39); this host keeps the
/// one process-live instance.
module Cursor =

    [<AllowNullLiteral>]
    type private AdvisorLedger =
        abstract delivered: callerSessionId: string -> string array
        abstract record: callerSessionId: string * keys: string array -> unit
        abstract clear: callerSessionId: string -> unit
        abstract clearAll: unit -> unit

    [<AllowNullLiteral>]
    type private AdvisorLedgerLaw =
        abstract ledger: unit -> AdvisorLedger

    [<Import("AdvisorLaw", "wanxiangli/advisor")>]
    let private advisorLawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : AdvisorLedgerLaw = jsNative

    // DSL-MUTABLE: resource — the process-live ledger; its state is owned by wanxiangli/advisor
    let private ledger = (construct advisorLawClass).ledger ()

    /// Material identities already delivered to this caller session's advisor.
    let delivered (callerSessionId: string) : Set<string> =
        ledger.delivered callerSessionId |> Set.ofArray

    /// Record only material confirmed as delivered. The record is monotone.
    let record (callerSessionId: string) (anchors: string list) : unit =
        ledger.record (callerSessionId, List.toArray anchors)

    let clear (callerSessionId: string) : unit = ledger.clear callerSessionId

    let clearAll () : unit = ledger.clearAll ()
