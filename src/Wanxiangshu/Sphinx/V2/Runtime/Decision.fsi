namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

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
    /// Select the best-ranked plan in one scope and produce the receipt that explains it.
    val choose:
        scopeId: string ->
        rule: string ->
        ruleVersion: string ->
        budgetBefore: Map<string, float> ->
        renderReserve: Map<string, float> ->
        consideredQuestions: string list ->
        approximationTags: string list ->
        sourceObservations: string list ->
        candidates: PlanEstimate list ->
        unestimated: UnestimatedPlan list ->
            Result<SelectionOutcome, DecisionError>

    val exclude: PlanId -> string -> ExcludedPlan
