namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

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

[<RequireQualifiedAccess>]
module StrengthPredictor =

    let empty = StrengthPredictorState Map.empty

    let byteBucket visibleBytes =
        if visibleBytes <= 0 then 0
        elif visibleBytes <= 4096 then 1
        elif visibleBytes <= 16384 then 2
        elif visibleBytes <= 65536 then 3
        else 4

    let feature role recentPrimary visibleBytes =
        { CanonicalRole = role
          RecentPrimary = recentPrimary |> List.truncate 3
          VisibleByteBucket = byteBucket visibleBytes }

    let private bucketOf key (StrengthPredictorState buckets) =
        Map.tryFind key buckets
        |> Option.defaultValue
            { Opportunities = 0
              ReadonlyFirst = 0
              SecondObservations = 0
              ReadonlySecond = 0 }

    let private put key value (StrengthPredictorState buckets) =
        StrengthPredictorState(Map.add key value buckets)

    /// Counterfactual first-request label. Callers must invoke this only for
    /// shadow/control primary observations; treatment interventions are excluded
    /// by construction at the coordinator boundary.
    let observeFirst key symbol state =
        let current = bucketOf key state
        let readonly = symbol = StrengthPrimarySymbol.ReadonlyBatch

        let next =
            { current with
                Opportunities = current.Opportunities + 1
                ReadonlyFirst = current.ReadonlyFirst + (if readonly then 1 else 0) }

        put key next state, readonly

    /// Conditional R2 label, only after an observed readonly R1.
    let observeSecond key symbol state =
        let current = bucketOf key state

        let next =
            { current with
                SecondObservations = current.SecondObservations + 1
                ReadonlySecond =
                    current.ReadonlySecond
                    + (if symbol = StrengthPrimarySymbol.ReadonlyBatch then 1 else 0) }

        put key next state

    let predict key state : StrengthPrediction =
        let bucket = bucketOf key state

        let p1 =
            if bucket.Opportunities <= 0 then 0.0
            else float bucket.ReadonlyFirst / float bucket.Opportunities

        let p2 =
            if bucket.SecondObservations <= 0 then 0.0
            else float bucket.ReadonlySecond / float bucket.SecondObservations

        { P1 = p1
          P2 = p2
          EvidenceCount = min bucket.Opportunities bucket.SecondObservations }

    let bucket key state = bucketOf key state
