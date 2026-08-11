namespace Wanxiangshu.Domain

/// Strength pure policy — Phase 0 forced K0.
///
/// Every decision is Skip / K0. No eligibility scoring, no live replica spawn,
/// no K1/K2 enablement. Call sites may invoke `decide` as a no-op gate so the
/// architecture splice is wired without changing ordinary provider behavior.

[<RequireQualifiedAccess>]
type StrengthDecision =
    /// No speculation. Phase 0 always lands here.
    | Skip of reason: string
    /// Shadow/control holdout (not enabled in Phase 0).
    | ControlHoldout
    /// Speculate with budget K1 or K2 (not enabled in Phase 0).
    | Speculate of budget: StrengthBudget

module StrengthPolicy =

    /// Phase 0 forced path: always Skip with K0. Ignores opportunity evidence.
    let decide (_opportunity: unit) : StrengthDecision =
        StrengthDecision.Skip "phase-0-forced-k0"

    /// Budget implied by a decision. Phase 0: always K0.
    let budgetOf (decision: StrengthDecision) : StrengthBudget =
        match decision with
        | StrengthDecision.Skip _ -> StrengthBudget.K0
        | StrengthDecision.ControlHoldout -> StrengthBudget.K0
        | StrengthDecision.Speculate budget -> budget

    /// True only when the decision would spawn a replica (never in Phase 0).
    let isSpeculate =
        function
        | StrengthDecision.Speculate _ -> true
        | StrengthDecision.Skip _
        | StrengthDecision.ControlHoldout -> false
