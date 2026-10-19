namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.EventStore

/// Js-transaction-owned canonical integration oracle, relocated from the
/// persistence spine. Rule `Name`, accepted event types, fold behavior, fault
/// scope and cut/reset semantics are unchanged.
[<RequireQualifiedAccess>]
module JsTransactionIntegrationRules =

    let jsTransactionRule: IntegrationRule =
        { Name = "JsTransaction"
          Initial = box JsTransactionProjection.empty
          FaultScope = fun _ -> "global"
          Accepts = fun envelope -> JsToolsTransactionStore.isTransactionEventType envelope.EventType
          Integrate =
            fun _ current envelope ->
                match JsToolsTransactionStore.tryDecodeEnvelope envelope with
                | Error error -> Error error
                | Ok(JsToolsTransactionStore.DecodedTransactionEvent.Prepared prepared) ->
                    JsTransactionProjection.prepared envelope.EventId prepared (unbox<JsTransactionProjection> current)
                    |> box
                    |> Ok
                | Ok(JsToolsTransactionStore.DecodedTransactionEvent.Committed committed) ->
                    JsTransactionProjection.committed
                        envelope.EventId
                        committed
                        (unbox<JsTransactionProjection> current)
                    |> box
                    |> Ok
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let rules: IntegrationRule list = [ jsTransactionRule ]

/// Js parked-site lifecycle audit integration oracle (PLAN S2 step 8 /
/// algorithm H3). The audit stream is independent of the mutation-ledger
/// stream; this rule accepts exactly the lifecycle audit event types and
/// fails closed on anything else. The fold is intentionally trivial — the
/// audit facts only need to be durable and replay-safe; no business projection
/// is derived from them.
[<RequireQualifiedAccess>]
module JsLifecycleIntegrationRules =

    /// The audit projection slot is an empty record; the audit stream has no
    /// business Current beyond the structural frontier owned by the Integrator.
    type JsLifecycleAuditProjection = { Head: EventId option }

    let empty = { Head = None }

    let jsLifecycleRule: IntegrationRule =
        { Name = "JsLifecycleAudit"
          Initial = box empty
          FaultScope = fun _ -> "global"
          Accepts = fun envelope -> JsToolsTransactionStore.isLifecycleEventType envelope.EventType
          Integrate =
            fun _ current envelope ->
                match JsToolsTransactionStore.tryDecodeLifecycleEnvelope envelope with
                | Error error -> Error error
                | Ok _ ->
                    let projection = unbox<JsLifecycleAuditProjection> current

                    box
                        { projection with
                            Head = Some envelope.EventId }
                    |> Ok
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let rules: IntegrationRule list = [ jsLifecycleRule ]
