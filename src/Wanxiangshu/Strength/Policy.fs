namespace Wanxiangshu.Strength

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Host
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength.Prediction

open System
open Wanxiangshu.Foundation

/// STRENGTH-002: frozen evidence only. No mutable stage/phase appears here.
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

    let eligibleRoles = set [ Role.Coder; Role.Inspector; Role.DevOps; Role.Inquiry ]

    let eligibility (opportunity: StrengthOpportunity) : StrengthEligibility =
        if not opportunity.IsRootWork then
            StrengthEligibility.Ineligible "not-root-work"
        elif opportunity.RequestKind <> ProviderRequestKind.WorkMain then
            StrengthEligibility.Ineligible "not-work-main"
        elif not (Set.contains opportunity.CanonicalRole eligibleRoles) then
            StrengthEligibility.Ineligible "role-ineligible"
        elif opportunity.SelectedTier <> AgentTier.Deep then
            StrengthEligibility.Ineligible "selected-tier-not-deep"
        elif not (String.Equals(opportunity.SelectedAgent, opportunity.EffectiveAgent, StringComparison.Ordinal)) then
            StrengthEligibility.Ineligible "effective-agent-not-selected"
        elif opportunity.IsFallbackRetry then
            StrengthEligibility.Ineligible "fallback-retry"
        elif opportunity.HasPrefixProbe then
            StrengthEligibility.Ineligible "prefix-probe"
        elif opportunity.IsReviewerOrFinality then
            StrengthEligibility.Ineligible "review-or-finality"
        elif opportunity.IsAttachedOrInternalLeaf then
            StrengthEligibility.Ineligible "attached-or-internal-leaf"
        elif opportunity.OwnerCancelled then
            StrengthEligibility.Ineligible "owner-cancelled"
        elif not opportunity.TargetProviderRunBound then
            StrengthEligibility.Ineligible "target-provider-run-unbound"
        elif not opportunity.EventStoreHealthy then
            StrengthEligibility.Ineligible "event-store-unhealthy"
        elif not opportunity.HostCanaryHealthy then
            StrengthEligibility.Ineligible "host-canary-unhealthy"
        elif not opportunity.FastPeerAvailable then
            StrengthEligibility.Ineligible "fast-peer-unavailable"
        elif not opportunity.CostModelAvailable then
            StrengthEligibility.Ineligible "cost-model-unavailable"
        else
            StrengthEligibility.Eligible

    /// A deterministic hash-to-bucket adapter. The hash implementation is owned
    /// by the caller; the policy consumes canonical hex so assignment remains
    /// restart-stable and contains no RNG/time source.
    let controlBucket (sha256: string -> string) (policyVersion: string) (authorityRoot: string) (targetRun: string) =
        let digest =
            sha256 (String.concat "\u001f" [ authorityRoot; targetRun; policyVersion ])

        let prefix =
            if String.IsNullOrEmpty digest then
                "0"
            else
                digest.Substring(0, min 16 digest.Length)

        // The digest is already the uniformizing primitive. A small ordinal fold
        // avoids platform-specific integer parsing while preserving a stable
        // 0..9999 bucket in both .NET and Fable/JS.
        prefix |> Seq.fold (fun acc ch -> (acc * 131 + int ch) % 10000) 0

    let isControlHoldout (rateBasisPoints: int) (bucket: int) =
        let rate = max 0 (min 10000 rateBasisPoints)
        bucket >= 0 && bucket < rate

    let private speculationDecision k1Worthwhile k2Worthwhile estimate =
        if k2Worthwhile && k1Worthwhile then
            StrengthDecision.Speculate(StrengthBudget.K2, estimate)
        elif k1Worthwhile then
            StrengthDecision.Speculate(StrengthBudget.K1, estimate)
        else
            StrengthDecision.Skip "non-positive-value"

    /// Pure Evidence → Decision. `shadow=true` computes upstream prediction/value
    /// but never intervenes. `control=true` is checked only after eligibility so
    /// ineligible traffic never masquerades as a holdout observation.
    let decideFromFacts
        (opportunity: StrengthOpportunity)
        (control: bool)
        (shadow: bool)
        (prediction: StrengthPrediction)
        (estimate: StrengthValueEstimate)
        (config: StrengthPolicyConfig)
        : StrengthDecision =
        match eligibility opportunity with
        | StrengthEligibility.Ineligible reason -> StrengthDecision.Skip reason
        | StrengthEligibility.Eligible when shadow -> StrengthDecision.Skip "shadow-k0"
        | StrengthEligibility.Eligible when control -> StrengthDecision.ControlHoldout
        | StrengthEligibility.Eligible ->
            let k1Worthwhile = estimate.V1 > config.K1Margin

            let k2Worthwhile =
                prediction.EvidenceCount >= config.K2MinimumEvidence
                && config.K2Margin > config.K1Margin
                && estimate.V2 > estimate.V1 + config.K2Margin

            speculationDecision k1Worthwhile k2Worthwhile estimate

    let budgetOf (decision: StrengthDecision) : StrengthBudget =
        match decision with
        | StrengthDecision.Skip _
        | StrengthDecision.ControlHoldout -> StrengthBudget.K0
        | StrengthDecision.Speculate(budget, _) -> budget
