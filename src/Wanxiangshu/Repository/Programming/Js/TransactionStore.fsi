namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// Narrow durable capability exposed to js-* workflow/tool wiring. The Host
/// registry never owns both AgentJournal and the raw EventStore capability.
type IJsTransactionPersistence =
    abstract AppendPrepared: prepared: JsTransactionPrepared -> Task<Result<EventId, string>>
    abstract AppendCommitted: transactionId: JsTransactionId -> Task<Result<EventId, string>>

/// JS-012/JS-015: durable transaction facts through the unified EventStore —
/// the only persistence a js-* transaction may use (forbid js-transaction.db
/// / feature store). A transaction is Prepared before any filesystem effect
/// and Committed after; an uncommitted Prepared remains interrupted-tool evidence.
module JsToolsTransactionStore =
    val TransactionStream: string
    val PreparedEventType: string
    val CommittedEventType: string

    [<RequireQualifiedAccess>]
    type DecodedTransactionEvent =
        | Prepared of JsTransactionPrepared
        | Committed of JsTransactionCommitted

    val isTransactionEventType: eventType: string -> bool
    val tryDecodeEnvelope: envelope: EventEnvelope -> Result<DecodedTransactionEvent, string>
    val appendPrepared: store: IEventStore -> prepared: JsTransactionPrepared -> Task<Result<EventId, string>>
    val appendCommitted: store: IEventStore -> transactionId: JsTransactionId -> Task<Result<EventId, string>>
    val createPersistence: store: IEventStore -> IJsTransactionPersistence
