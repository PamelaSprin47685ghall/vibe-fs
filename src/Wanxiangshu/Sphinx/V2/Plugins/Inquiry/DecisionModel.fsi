namespace Wanxiangshu.Sphinx.V2.Plugins

open Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type ContributionKind =
    | ModelEstimate of modelRef: string * approximation: string
    | OrdinalOnly
    | SingleResponseProvisional
    | Unestimated

type ContributionEstimate =
    { PlanId: string
      ScopeId: string
      Kind: ContributionKind
      Location: float option
      Rank: int option }

type DecisionModelError = { Code: string; Message: string }

module DecisionModel =
    /// Ranks estimates within one scope. Unestimated plans are never placed.
    val rankInScope: string -> ContributionEstimate list -> ContributionEstimate list
    val usable: ContributionEstimate -> bool
    val supportsNumericComparison: ContributionEstimate list -> bool
    val isProvisionalOnly: ContributionEstimate list -> bool
