namespace Wanxiangshu.Strength.Prediction

open Wanxiangshu.Foundation
open Wanxiangshu.Strength

[<RequireQualifiedAccess>]
type StrengthPrimarySymbol =
    | ReadonlyBatch
    | MutatingOrExecuting
    | TextOnly
    | Other

type StrengthFeatureKey =
    { CanonicalRole: Role
      RecentPrimary: StrengthPrimarySymbol list
      VisibleByteBucket: int }

type StrengthPredictorBucket =
    { Opportunities: int
      ReadonlyFirst: int
      SecondObservations: int
      ReadonlySecond: int }

type StrengthPredictorState

module StrengthPredictor =
    val empty: StrengthPredictorState
    val feature: role: Role -> recent: StrengthPrimarySymbol list -> visibleBytes: int -> StrengthFeatureKey
    val bucket: feature: StrengthFeatureKey -> state: StrengthPredictorState -> StrengthPredictorBucket

    val observeFirst:
        feature: StrengthFeatureKey ->
        first: StrengthPrimarySymbol ->
        state: StrengthPredictorState ->
            StrengthPredictorState * bool

    val observeSecond:
        feature: StrengthFeatureKey ->
        second: StrengthPrimarySymbol ->
        state: StrengthPredictorState ->
            StrengthPredictorState

    val predict: feature: StrengthFeatureKey -> state: StrengthPredictorState -> StrengthPrediction
