namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Persistence.EventStore

/// Casebook-owned canonical integration oracle, relocated from the persistence
/// spine. Rule `Name`, accepted event types, fold behavior, fault scope and
/// cut/reset semantics are unchanged; the persistence spine receives it
/// through explicit `CanonicalIntegrator.createWithRules` injection and never
/// references this module.
[<RequireQualifiedAccess>]
module CasebookIntegrationRules =

    let casebookRule: IntegrationRule =
        { Name = "Casebook"
          Initial = box CasebookProjection.emptyState
          FaultScope = fun _ -> "global"
          Accepts = fun envelope -> CasebookStore.isCasebookEventType envelope.EventType
          Integrate =
            fun current envelope ->
                match CasebookStore.tryDecodeEnvelope envelope with
                | Error error -> Error error
                | Ok event ->
                    CasebookProjection.apply (unbox<CasebookProjection.State> current) event
                    |> box
                    |> Ok
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let rules: IntegrationRule list = [ casebookRule ]
