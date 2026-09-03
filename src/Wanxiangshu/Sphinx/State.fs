namespace Wanxiangshu.Sphinx

open System

module State =

    let private clamp01 value = max 0.0 (min 1.0 value)

    let private finite value =
        not (Double.IsNaN value || Double.IsInfinity value)

    let normalizeDistribution (distribution: Map<'key, float>) : Map<'key, float> when 'key: comparison =
        let sanitized =
            distribution
            |> Map.map (fun _ value -> if finite value then max 0.0 value else 0.0)

        let total = sanitized |> Map.fold (fun sum _ value -> sum + value) 0.0

        if total <= 0.0 then
            Map.empty
        else
            sanitized |> Map.map (fun _ value -> value / total)

    let contractForForm =
        function
        | QuestionForm.Why -> AnswerContract.Explanation
        | QuestionForm.How -> AnswerContract.Plan
        | QuestionForm.What
        | QuestionForm.Who
        | QuestionForm.Where
        | QuestionForm.When -> AnswerContract.Direct
        | QuestionForm.Which -> AnswerContract.Ranking
        | QuestionForm.Polar -> AnswerContract.Judgment
        | QuestionForm.Other -> AnswerContract.Credence

    let deriveRootContract (assessment: SemanticAssessment) : RootContract =
        let forms = normalizeDistribution assessment.Forms

        let contractBelief =
            forms
            |> Map.toList
            |> List.groupBy (fst >> contractForForm)
            |> List.map (fun (contract, rows) -> contract, rows |> List.sumBy snd)
            |> Map.ofList
            |> normalizeDistribution

        { FormBelief = forms
          ContractBelief = contractBelief
          Facets = assessment.Facets |> Map.map (fun _ value -> clamp01 value)
          Targets = assessment.Targets
          Intents = assessment.Intents }

    let emptyRepresentation =
        { EquivalenceClasses = Map.empty
          ParetoFrontiers = Map.empty
          Representatives = Map.empty
          EstimatedFutureCost = 0.0 }

    let create question =
        { RootQuestion = question
          RootContract = None
          Findings = Map.empty
          Evidence = Map.empty
          Hypotheses = Map.empty
          Dependencies = Map.empty
          Actions = Map.empty
          Budget =
            { MaxYields = 100
              UsedYields = 0
              MaxCost = 100.0
              UsedCost = 0.0 }
          PendingRequest = None
          Synthesis = None
          Bayesian = None
          Search = Map.empty
          MonteCarlo = Map.empty
          Representation = emptyRepresentation
          SolverMode = SolverMode.Bellman
          NeedsGeneration = false
          Revision = 0 }

    let withYield (request: Request) (state: EpistemicState) =
        { state with
            PendingRequest = Some request
            Budget =
                { state.Budget with
                    UsedYields = state.Budget.UsedYields + 1 } }

    let clearPending state = { state with PendingRequest = None }

    let hasEvidenceSemanticKey semanticKey (state: EpistemicState) =
        state.Evidence
        |> Map.exists (fun _ evidence -> evidence.SemanticKey = semanticKey)

    let remainingYieldBudget state =
        state.Budget.MaxYields - state.Budget.UsedYields

    let remainingCostBudget state =
        state.Budget.MaxCost - state.Budget.UsedCost

    let withinBudget state =
        remainingYieldBudget state > 0 && remainingCostBudget state > 0.0

    let markActionResolved actionKey state =
        match Map.tryFind actionKey state.Actions with
        | None -> state
        | Some action ->
            { state with
                Actions =
                    state.Actions
                    |> Map.add
                        actionKey
                        { action with
                            Status = ActionStatus.Resolved }
                Budget =
                    { state.Budget with
                        UsedCost = min state.Budget.MaxCost (state.Budget.UsedCost + max 0.0 action.Cost) } }

    let addDependency (dependencyKey: string) (semanticKey: string) (dependencies: Map<string, Set<string>>) =
        let existing =
            dependencies |> Map.tryFind dependencyKey |> Option.defaultValue Set.empty

        dependencies |> Map.add dependencyKey (existing |> Set.add semanticKey)
