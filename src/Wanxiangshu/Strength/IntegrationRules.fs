namespace Wanxiangshu.Strength

open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Projection

/// Strength-owned canonical integration oracle, relocated from the persistence
/// spine. Rule `Name`, accepted event types, fold behavior, fault scope and
/// cut/reset semantics are unchanged.
[<RequireQualifiedAccess>]
module StrengthIntegrationRules =

    let strengthRule: IntegrationRule =
        { Name = "Strength"
          Initial = box StrengthProjection.empty
          FaultScope = fun _ -> "global"
          Accepts = fun envelope -> StrengthEventTypes.isStrengthEvent envelope.EventType
          Integrate =
            fun current envelope ->
                match StrengthStore.tryDecodeEnvelope envelope with
                | Error error -> Error error
                | Ok event ->
                    StrengthProjection.apply (unbox<StrengthProjection> current) event
                    |> Result.map box
                    |> Result.mapError (fun error -> sprintf "Strength integration rejected: %A" error)
          PlanCut = fun _ _ _ _ -> Ok { ResetJson = "{}" }
          ApplyCut = fun current _ -> Ok current }

    let rules: IntegrationRule list = [ strengthRule ]
