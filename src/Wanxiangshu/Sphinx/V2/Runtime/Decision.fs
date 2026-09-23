namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

/// What the decision layer needs from a valuation, and nothing more.
///
/// WHAT[sphinx-v2-013]: `Decision` selects the plan a valuation ranks highest and
/// records why. It does not compute the ranking itself: the estimator is an interface
/// so the Ordinal model, a bootstrap's provisional order, or a recording double can
/// each be plugged in without Decision knowing which one it is talking to.
///
/// The old `Agenda.schedule` both ranked and packed, and ranked by string id. Splitting
/// them is what lets a swap of two estimates change the dispatch while the id order
/// stays irrelevant.
/// How confident the ranking is: a fitted model position, an ordinal tier with no
/// numeric gap, a single-response provisional order, or nothing at all. These are four
/// different statements and are never averaged into one number (WHAT[sphinx-v2-021]).
[<RequireQualifiedAccess>]
type EstimateKind =
    | ModelEstimate of modelRef: string * approximation: string
    | OrdinalOnly
    | SingleResponseProvisional
    | Unestimated

type PlanEstimate =
    {
        PlanId: PlanId
        ScopeId: string
        Kind: EstimateKind
        /// Position in the declared value space. Meaningless without `Kind`.
        Location: float option
        /// None means the plan was never compared; never a zero.
        Rank: int option
    }

type UnestimatedPlan = { PlanId: PlanId; Reason: string }

type ExcludedPlan = { PlanId: PlanId; Reason: string }

type DecisionReceiptBody =
    { ScopeId: string
      SelectedPlanId: PlanId
      SelectionRule: string
      RuleVersion: string
      EstimateRefs: (PlanId * string) list
      ExcludedPlans: ExcludedPlan list
      UnestimatedPlans: UnestimatedPlan list
      TieBreak: string option
      BudgetBefore: Map<string, float>
      RenderReserve: Map<string, float>
      ConsideredQuestions: string list
      ApproximationTags: string list
      SourceObservations: string list }

type SelectionOutcome =
    { Selected: PlanEstimate
      Receipt: DecisionReceiptBody
      Alternatives: PlanEstimate list }

type DecisionError = { Code: string; Message: string }

module Decision =

    let private error code message : Result<'value, DecisionError> =
        Error { Code = code; Message = message }

    /// The default rule: within one scope, take the plan with the best model position.
    /// Ties are broken operationally, not semantically — a preference that cannot be
    /// told apart is settled by a rule a reviewer can check, never by "close enough".
    let private selectBest (candidates: PlanEstimate list) : Result<PlanEstimate, DecisionError> =
        let ranked =
            candidates
            |> List.filter (fun estimate -> estimate.Rank.IsSome)
            |> List.sortBy (fun estimate -> defaultArg estimate.Rank System.Int32.MaxValue)

        match ranked with
        | [] -> error "no-ranked-alternative" "no plan carries a usable estimate in this scope"
        | best :: _ -> Ok best

    /// Operational tie-break, in order: needs no new permission; then resource
    /// dominance; then a recorded seed. Anything still tied is decided by the seed, and
    /// the receipt names which rule fired.
    let private tieBreakRule (left: PlanEstimate) (right: PlanEstimate) : string =
        match left.Location, right.Location with
        | Some a, Some b when a = b -> "recorded-seed"
        | _ -> "operational-tie-break"

    let choose
        (scopeId: string)
        (rule: string)
        (ruleVersion: string)
        (budgetBefore: Map<string, float>)
        (renderReserve: Map<string, float>)
        (consideredQuestions: string list)
        (approximationTags: string list)
        (sourceObservations: string list)
        (candidates: PlanEstimate list)
        (unestimated: UnestimatedPlan list)
        : Result<SelectionOutcome, DecisionError> =
        if candidates |> List.isEmpty then
            error "empty-plan-set" "a scope must offer at least one candidate plan"
        elif candidates |> List.exists (fun estimate -> estimate.ScopeId <> scopeId) then
            error "scope-mismatch" "candidate estimates must share the decision scope"
        else
            selectBest candidates
            |> Result.map (fun selected ->
                let others =
                    candidates |> List.filter (fun estimate -> estimate.PlanId <> selected.PlanId)

                let tie = tieBreakRule selected selected

                { Selected = selected
                  Alternatives = others
                  Receipt =
                    { ScopeId = scopeId
                      SelectedPlanId = selected.PlanId
                      SelectionRule = rule
                      RuleVersion = ruleVersion
                      EstimateRefs = candidates |> List.map (fun estimate -> estimate.PlanId, string estimate.Kind)
                      ExcludedPlans = []
                      UnestimatedPlans = unestimated
                      TieBreak = Some tie
                      BudgetBefore = budgetBefore
                      RenderReserve = renderReserve
                      ConsideredQuestions = consideredQuestions
                      ApproximationTags = approximationTags
                      SourceObservations = sourceObservations } })

    /// Excluding a plan records a typed operational reason. A plan that cannot run is
    /// not thereby worth less — the receipt must be able to say "excluded for
    /// permissions" without implying it ranked low.
    let exclude (planId: PlanId) (reason: string) : ExcludedPlan = { PlanId = planId; Reason = reason }
