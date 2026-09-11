namespace Wanxiangshu.Repository.Programming.Js

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
            fun current envelope ->
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
