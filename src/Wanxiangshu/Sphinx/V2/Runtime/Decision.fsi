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
    /// The rule this module actually executes, recorded in every receipt.
    val selectionRule: string

    /// The tie-break this module applies, named exactly as it is applied.
    val tieBreakRule: string

    /// Select the best-ranked plan in one scope and produce the receipt that explains it.
    val choose:
        scopeId: string ->
        candidates: PlanEstimate list ->
        unestimated: UnestimatedPlan list ->
            Result<SelectionOutcome, DecisionError>

    val exclude: PlanId -> string -> ExcludedPlan
