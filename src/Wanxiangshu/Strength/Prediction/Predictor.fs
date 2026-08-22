namespace Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Strength

[<RequireQualifiedAccess>]
type StrengthPrimarySymbol =
    | ReadonlyBatch
    | MutatingOrExecuting
    | TextOnly
    | Other

/// STRENGTH-010: a frozen, restart-independent feature key. No score, model id,
/// random bucket or Replica provenance is allowed into the key.
type StrengthFeatureKey =
    { CanonicalRole: Role
      RecentPrimary: StrengthPrimarySymbol list
      VisibleByteBucket: int }

type StrengthPredictorBucket =
    { Opportunities: int
      ReadonlyFirst: int
      SecondObservations: int
      ReadonlySecond: int }

type StrengthPredictorState = private StrengthPredictorState of Map<StrengthFeatureKey, StrengthPredictorBucket>

module StrengthPredictor =

    let empty: StrengthPredictorState = StrengthPredictorState Map.empty

    let private emptyBucket =
        { Opportunities = 0
          ReadonlyFirst = 0
          SecondObservations = 0
          ReadonlySecond = 0 }

    let private byteBucket visibleBytes =
        if visibleBytes <= 0 then 0
        elif visibleBytes <= 4096 then 1
        elif visibleBytes <= 16384 then 2
        elif visibleBytes <= 65536 then 3
        else 4

    let feature (role: Role) (recent: StrengthPrimarySymbol list) (visibleBytes: int) : StrengthFeatureKey =
        { CanonicalRole = role
          RecentPrimary = recent |> List.truncate 3
          VisibleByteBucket = byteBucket visibleBytes }

    let bucket (feature: StrengthFeatureKey) (StrengthPredictorState state) : StrengthPredictorBucket =
        state |> Map.tryFind feature |> Option.defaultValue emptyBucket

    let observeFirst
        (feature: StrengthFeatureKey)
        (first: StrengthPrimarySymbol)
        (StrengthPredictorState state)
        : StrengthPredictorState * bool =
        let current = state |> Map.tryFind feature |> Option.defaultValue emptyBucket
        let isReadonly = first = StrengthPrimarySymbol.ReadonlyBatch

        let next =
            { current with
                Opportunities = current.Opportunities + 1
                ReadonlyFirst =
                    if isReadonly then
                        current.ReadonlyFirst + 1
                    else
                        current.ReadonlyFirst }

        StrengthPredictorState(state |> Map.add feature next), isReadonly

    let observeSecond
        (feature: StrengthFeatureKey)
        (second: StrengthPrimarySymbol)
        (StrengthPredictorState state)
        : StrengthPredictorState =
        let current = state |> Map.tryFind feature |> Option.defaultValue emptyBucket

        let next =
            { current with
                SecondObservations = current.SecondObservations + 1
                ReadonlySecond =
                    if second = StrengthPrimarySymbol.ReadonlyBatch then
                        current.ReadonlySecond + 1
                    else
                        current.ReadonlySecond }

        StrengthPredictorState(state |> Map.add feature next)

    let predict
        (feature: StrengthFeatureKey)
        (StrengthPredictorState state)
        : Wanxiangshu.Strength.StrengthPrediction =
        let b = state |> Map.tryFind feature |> Option.defaultValue emptyBucket

        let p1 =
            if b.Opportunities = 0 then
                0.0
            else
                float b.ReadonlyFirst / float b.Opportunities

        let p2 =
            if b.SecondObservations = 0 then
                0.0
            else
                float b.ReadonlySecond / float b.SecondObservations

        { P1 = p1
          P2 = p2
          EvidenceCount = min b.Opportunities b.SecondObservations }
