namespace Wanxiangshu.Sphinx.V2.Plugins

open Wanxiangshu.Sphinx.V2.Core

/// The decision model that turns ordinal observations into plan estimates.
///
/// WHAT[sphinx-v2-013]: this module consumes an observation model's output and emits
/// estimates; it does not itself decide. The Estimator it produces is what the Runtime's
/// `Decision` calls, so a bootstrap provisional order, an ordinal fit and a recording
/// double are interchangeable without Decision knowing the difference.
///
/// WHAT[sphinx-v2-029]: answer.now is estimated like any other plan. It is never zero
/// cost and never automatically best.

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

    /// An absent comparison never becomes usable merely by carrying a rank.
    let usable (estimate: ContributionEstimate) : bool =
        estimate.Kind <> ContributionKind.Unestimated && estimate.Rank.IsSome

    /// Whether a scope's estimates can support a numeric comparison. A single-response
    /// provisional order is a real answer with a real limitation: it is not an
    /// independent panel (WHAT[sphinx-v2-021]).
    let supportsNumericComparison (estimates: ContributionEstimate list) : bool =
        let kinds =
            estimates |> List.filter usable |> List.map (fun estimate -> estimate.Kind)

        not (List.isEmpty kinds)
        && (kinds
            |> List.forall (fun kind ->
                match kind with
                | ContributionKind.ModelEstimate _ -> true
                | _ -> false))
