namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Persistence.EventStore

/// Js-transaction-owned canonical integration oracle. The persistence spine
/// receives it through explicit `CanonicalIntegrator.createWithRules`
/// injection and never references this module.
[<RequireQualifiedAccess>]
module JsTransactionIntegrationRules =
    val jsTransactionRule: IntegrationRule
    val rules: IntegrationRule list
