namespace Wanxiangshu.Repository.Programming.Js

/// Durable Js-transaction event vocabulary. The transaction store, its
/// integration rule and the persistence vocabulary join these names instead of
/// restating them.
module JsTransactionEventTypes =
    let Prepared = "JsTransactionPrepared"
    let Committed = "JsTransactionCommitted"

    let all = [ Prepared; Committed ]

    let isTransactionEvent eventType = all |> List.contains eventType
