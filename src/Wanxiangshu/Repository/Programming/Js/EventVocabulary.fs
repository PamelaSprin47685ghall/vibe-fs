namespace Wanxiangshu.Repository.Programming.Js

/// Durable Js-transaction event vocabulary. The transaction store, its
/// integration rule and the persistence vocabulary join these names instead of
/// restating them.
module JsTransactionEventTypes =
    let Prepared = "JsTransactionPrepared"
    let Committed = "JsTransactionCommitted"

    let all = [ Prepared; Committed ]

    let isTransactionEvent eventType = all |> List.contains eventType

/// Durable Js parked-site lifecycle audit vocabulary. The lifecycle audit
/// store, its integration rule and the persistence vocabulary join these
/// names instead of restating them. These events live on their own audit
/// stream, separate from the mutation-ledger stream (PLAN S2 step 8 /
/// algorithm H3): they record that a lifecycle transition happened and never
/// participate in write replay.
module JsLifecycleAuditEventTypes =
    let Parked = "JsLifecycleParked"
    let Unparked = "JsLifecycleUnparked"
    let Abandoned = "JsLifecycleAbandoned"
    let Reaped = "JsLifecycleReaped"

    let all = [ Parked; Unparked; Abandoned; Reaped ]

    let isLifecycleAuditEvent eventType = all |> List.contains eventType
