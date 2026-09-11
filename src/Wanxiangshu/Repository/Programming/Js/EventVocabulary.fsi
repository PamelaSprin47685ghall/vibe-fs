namespace Wanxiangshu.Repository.Programming.Js

/// Durable Js-transaction event vocabulary. The transaction store, its
/// integration rule and the persistence vocabulary join these names instead of
/// restating them.
module JsTransactionEventTypes =
    val Prepared: string
    val Committed: string
    val all: string list
    val isTransactionEvent: eventType: string -> bool
