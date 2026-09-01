namespace Wanxiangshu.Strength.OpenCode

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Replica

/// Completed counterfactual pair — typed outcome consumed once by the owning CE.
/// Business layer observes only this type, never the collector's internal registries.
type CounterfactualPair =
    { Feature: StrengthFeatureKey
      FirstSymbol: StrengthPrimarySymbol
      SecondSymbol: StrengthPrimarySymbol }

/// STRENGTH-*: decision-local replica ownership/capability registry plus bounded
/// predictor evidence for one plugin instance. Durable causality stays in
/// EventStore; this is only live physical-session state (STRENGTH-014).
type PluginStrengthScope =
    new: unit -> PluginStrengthScope

    member StrengthRuntime: StrengthRuntime
    member AttachStrengthReplicaRuntime: runtime: StrengthReplicaRuntime -> unit
    member StrengthReplicaRuntime: StrengthReplicaRuntime option

    member StrengthFeature: sessionId: SessionId * role: Role * visibleBytes: int -> StrengthFeatureKey

    member StrengthPrediction: feature: StrengthFeatureKey -> StrengthPrediction
    member TripStrengthFuse: reason: string -> unit
    member StrengthFuseReason: string option
    member StrengthFuse: Result<unit, string>

    member ArmStrengthCounterfactual:
        sessionId: SessionId * targetRun: ProviderRunIdentity * feature: StrengthFeatureKey -> unit

    member ObserveStrengthPrimary:
        sessionId: SessionId * providerRun: ProviderRunIdentity * symbol: StrengthPrimarySymbol ->
            CounterfactualPair option

    member ClearSession: sessionId: string -> unit
    member Dispose: unit -> unit
