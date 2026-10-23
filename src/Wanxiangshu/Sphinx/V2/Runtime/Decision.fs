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
      EstimateRefs: (PlanId * string) list
      ExcludedPlans: ExcludedPlan list
      UnestimatedPlans: UnestimatedPlan list
      TieBreak: string option }

type SelectionOutcome =
    { Selected: PlanEstimate
      Receipt: DecisionReceiptBody
      Alternatives: PlanEstimate list }

type DecisionError = { Code: string; Message: string }

module Decision =

    let private error code message : Result<'value, DecisionError> =
        Error { Code = code; Message = message }

    /// The rule this module actually executes, written down once and recorded in every
    /// receipt so a reader can check the outcome against the stated rule.
    ///
    /// It is deliberately free of any seed: the outcome is a function of the input
    /// alone, so the same candidates always select the same plan.
    let selectionRule =
        "lowest rank first; an equal rank is settled by plan id ascending"

    /// The tie-break this module applies, named exactly as it is applied.
    let tieBreakRule = "plan id ascending"

    let private rankOf (estimate: PlanEstimate) : int =
        defaultArg estimate.Rank System.Int32.MaxValue

    /// The operation key that settles an equal rank. It is a property of the plan
    /// itself, so it needs no randomness and no seed.
    let private operationKey (estimate: PlanEstimate) : string = PlanId.value estimate.PlanId

    /// The selected plan, and whether an equal rank actually occurred. A tie that
    /// never happened is not recorded as handled.
    let private selectBest (candidates: PlanEstimate list) : Result<PlanEstimate * bool, DecisionError> =
        let ranked =
            candidates
            |> List.filter (fun estimate -> estimate.Kind <> EstimateKind.Unestimated && estimate.Rank.IsSome)
            |> List.sortBy (fun estimate -> rankOf estimate, operationKey estimate)

        match ranked with
        | [] -> error "no-ranked-alternative" "no plan carries a usable estimate in this scope"
        | best :: rest -> Ok(best, rest |> List.exists (fun estimate -> rankOf estimate = rankOf best))

    let choose
        (scopeId: string)
        (candidates: PlanEstimate list)
        (unestimated: UnestimatedPlan list)
        : Result<SelectionOutcome, DecisionError> =
        if candidates |> List.isEmpty then
            error "empty-plan-set" "a scope must offer at least one candidate plan"
        elif candidates |> List.exists (fun estimate -> estimate.ScopeId <> scopeId) then
            error "scope-mismatch" "candidate estimates must share the decision scope"
        else
            selectBest candidates
            |> Result.map (fun (selected, tied) ->
                let others =
                    candidates |> List.filter (fun estimate -> estimate.PlanId <> selected.PlanId)

                { Selected = selected
                  Alternatives = others
                  Receipt =
                    { ScopeId = scopeId
                      SelectedPlanId = selected.PlanId
                      SelectionRule = selectionRule
                      EstimateRefs = candidates |> List.map (fun estimate -> estimate.PlanId, string estimate.Kind)
                      ExcludedPlans = []
                      UnestimatedPlans = unestimated
                      TieBreak = if tied then Some tieBreakRule else None } })

    /// Excluding a plan records a typed operational reason. A plan that cannot run is
    /// not thereby worth less — the receipt must be able to say "excluded for
    /// permissions" without implying it ranked low.
    let exclude (planId: PlanId) (reason: string) : ExcludedPlan = { PlanId = planId; Reason = reason }
