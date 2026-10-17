namespace Wanxiangshu.Repository.Programming.Js

/// Durable Js-transaction event vocabulary. The transaction store, its
/// integration rule and the persistence vocabulary join these names instead of
/// restating them.
module JsTransactionEventTypes =
    val Prepared: string
    val Committed: string
    val all: string list
    val isTransactionEvent: eventType: string -> bool

/// Durable Js parked-site lifecycle audit vocabulary. These names join the
/// lifecycle audit store, its integration rule and the persistence
/// vocabulary. The audit stream is separate from the mutation-ledger stream.
module JsLifecycleAuditEventTypes =
    val Parked: string
    val Unparked: string
    val Abandoned: string
    val Reaped: string
    val all: string list
    val isLifecycleAuditEvent: eventType: string -> bool
