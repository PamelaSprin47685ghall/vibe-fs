namespace Wanxiangshu.Strength

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
