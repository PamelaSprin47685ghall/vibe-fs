namespace Wanxiangshu.Sphinx.V2.Composition

open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore

/// Native composition boundary for the mandatory, owner-supplied settlement capability.
module SettlementSurface =
    val create: store: EventStoreHandle -> configuration: obj -> onIncident: (obj -> unit) -> RuntimeHandle

    val start: handle: RuntimeHandle -> args: obj -> Task<obj>

    /// The incident and append error are opaque receipts from their original owners.
    val settleAppendCutUnknown:
        handle: RuntimeHandle -> incident: AppendCutUnknownIncident -> originalError: obj -> unit

    val dispose: handle: RuntimeHandle -> unit
