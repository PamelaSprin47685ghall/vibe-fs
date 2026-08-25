// primary_owner: speculative-investigation — SpeculativeInvestigation.SurfaceSurface — KEEP — speculative-investigation-surface verified
namespace Wanxiangshu.Strength.Replica

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type StrengthProviderOutputEvidence =
    | RealOutput
    | TransportOnly
    | NoOutput

[<RequireQualifiedAccess>]
type StrengthPromotionDecision =
    | Promote
    | IgnoreWrongRun
    | AwaitOrAbandon

[<RequireQualifiedAccess>]
module StrengthPromotion =

    let private decideOutput evidence =
        match evidence with
        | StrengthProviderOutputEvidence.RealOutput -> StrengthPromotionDecision.Promote
        | StrengthProviderOutputEvidence.TransportOnly
        | StrengthProviderOutputEvidence.NoOutput -> StrengthPromotionDecision.AwaitOrAbandon

    /// STRENGTH-007: ProviderRunIdentity is the causal consumption identity.
    /// A run outcome label is intentionally not an input: a failed/aborted run
    /// that already emitted real provider output still proves the provider saw
    /// its input; a failed/aborted run with no real output does not.
    let decide
        (targetProviderRun: ProviderRunIdentity)
        (observedProviderRun: ProviderRunIdentity)
        (outputEvidence: StrengthProviderOutputEvidence)
        : StrengthPromotionDecision =
        if targetProviderRun <> observedProviderRun then
            StrengthPromotionDecision.IgnoreWrongRun
        else
            decideOutput outputEvidence
