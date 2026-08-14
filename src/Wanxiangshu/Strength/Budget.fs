namespace Wanxiangshu.Strength

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Dispatch
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

/// Strength K0/K1/K2 budget — request-gated progression.
/// K0 = no speculation. K1 = one request batch. K2 = two request batches.
/// Request count is the unit, not tool-call count.
/// Threshold is holdout-measured ExpectedValue(K) > margin.

[<RequireQualifiedAccess>]
type StrengthBudget =
    | K0
    | K1
    | K2

module StrengthBudget =

    let parse =
        function
        | "K0" -> Some StrengthBudget.K0
        | "K1" -> Some StrengthBudget.K1
        | "K2" -> Some StrengthBudget.K2
        | _ -> None

    let wire =
        function
        | StrengthBudget.K0 -> "K0"
        | StrengthBudget.K1 -> "K1"
        | StrengthBudget.K2 -> "K2"

    /// STRENGTH-003: K is a provider-request budget, never a tool-call budget.
    let requestLimit =
        function
        | StrengthBudget.K0 -> 0
        | StrengthBudget.K1 -> 1
        | StrengthBudget.K2 -> 2

    /// Holdout-gated promotion: K0->K1 needs ExpectedValue(K1) > K1Margin.
    /// K1->K2 needs ExpectedValue(K2) > K2Margin where K2Margin > K1Margin.
    let canPromoteToK1 (expectedValueK1: float) (k1Margin: float) : bool = expectedValueK1 > k1Margin

    let canPromoteToK2 (expectedValueK2: float) (k2Margin: float) : bool = expectedValueK2 > k2Margin

    /// Gate: K2 never before minimum evidence floor, steering risk higher for K2.
    /// Ineligible / unknown cost -> stay K0. Non-deep / fallback side -> K0.
    let isEligibleForK2 (evidenceCount: int) (minFloor: int) : bool = evidenceCount >= minFloor
