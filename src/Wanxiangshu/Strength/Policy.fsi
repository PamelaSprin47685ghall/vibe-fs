namespace Wanxiangshu.Strength

open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Strength.Prediction

type StrengthOpportunity =
    { IsRootWork: bool
      RequestKind: ProviderRequestKind
      CanonicalRole: Role
      SelectedTier: AgentTier
      SelectedAgent: string
      EffectiveAgent: string
      IsFallbackRetry: bool
      HasPrefixProbe: bool
      IsReviewerOrFinality: bool
      IsAttachedOrInternalLeaf: bool
      OwnerCancelled: bool
      TargetProviderRunBound: bool
      EventStoreHealthy: bool
      HostCanaryHealthy: bool
      FastPeerAvailable: bool
      CostModelAvailable: bool }

type StrengthPrediction =
    { P1: float
      P2: float
      EvidenceCount: int }

type StrengthPolicyConfig =
    { K1Margin: float
      K2Margin: float
      K2MinimumEvidence: int }

[<RequireQualifiedAccess>]
type StrengthEligibility =
    | Ineligible of reason: string
    | Eligible

[<RequireQualifiedAccess>]
type StrengthDecision =
    | Skip of reason: string
    | ControlHoldout
    | Speculate of budget: StrengthBudget * estimate: StrengthValueEstimate

module StrengthPolicy =
    val eligibleRoles: Set<Role>
    val eligibility: opportunity: StrengthOpportunity -> StrengthEligibility

    val controlBucket:
        sha256: (string -> string) -> policyVersion: string -> authorityRoot: string -> targetRun: string -> int

    val isControlHoldout: rateBasisPoints: int -> bucket: int -> bool

    val decideFromFacts:
        opportunity: StrengthOpportunity ->
        control: bool ->
        shadow: bool ->
        prediction: StrengthPrediction ->
        estimate: StrengthValueEstimate ->
        config: StrengthPolicyConfig ->
            StrengthDecision
