namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// Js-transaction-owned canonical integration oracle. The persistence spine
/// receives it through explicit `CanonicalIntegrator.createWithRules`
/// injection and never references this module.
[<RequireQualifiedAccess>]
module JsTransactionIntegrationRules =
    val jsTransactionRule: IntegrationRule
    val rules: IntegrationRule list

/// Js parked-site lifecycle audit integration oracle (PLAN S2 step 8 /
/// algorithm H3). Accepts exactly the lifecycle audit event types and fails
/// closed on anything else.
[<RequireQualifiedAccess>]
module JsLifecycleIntegrationRules =
    type JsLifecycleAuditProjection = { Head: EventId option }
    val empty: JsLifecycleAuditProjection
    val jsLifecycleRule: IntegrationRule
    val rules: IntegrationRule list
