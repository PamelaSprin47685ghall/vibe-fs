namespace Wanxiangshu.Sphinx

module Methodology =

    type MethodDefinition =
        { Name: string
          FormWeights: Map<QuestionForm, float>
          FacetWeights: Map<string, float>
          BaseCost: float
          CombinesExistingKnowledge: bool }

    let private methodDef name forms facets cost combines =
        { Name = name
          FormWeights = Map.ofList forms
          FacetWeights = Map.ofList facets
          BaseCost = cost
          CombinesExistingKnowledge = combines }

    let library =
        [ methodDef
              "Multidisciplinary"
              [ QuestionForm.Why, 1.0; QuestionForm.How, 0.7; QuestionForm.Other, 0.35 ]
              [ "explanatory", 1.0; "causal", 0.8; "multi-domain", 1.0 ]
              1.1
              false
          methodDef
              "Abduction"
              [ QuestionForm.Why, 1.0; QuestionForm.Polar, 0.55; QuestionForm.How, 0.45 ]
              [ "causal", 1.0; "explanatory", 0.8; "diagnostic", 0.9 ]
              1.0
              false
          methodDef
              "Analogy"
              [ QuestionForm.Polar, 0.9; QuestionForm.Which, 0.7; QuestionForm.How, 0.55 ]
              [ "predictive", 0.9; "comparative", 1.0 ]
              0.9
              false
          methodDef
              "Counterexample"
              [ QuestionForm.Polar, 0.9; QuestionForm.Which, 0.65; QuestionForm.Why, 0.55 ]
              [ "falsification", 1.0; "comparative", 0.7; "causal", 0.5 ]
              0.85
              false
          methodDef
              "Synthesis"
              [ QuestionForm.Why, 0.95; QuestionForm.How, 0.85; QuestionForm.What, 0.45 ]
              [ "explanatory", 0.9; "causal", 0.65 ]
              0.7
              true
          methodDef
              "CausalMechanism"
              [ QuestionForm.Why, 1.0; QuestionForm.How, 0.7; QuestionForm.Polar, 0.4 ]
              [ "causal", 1.0; "explanatory", 0.85 ]
              1.0
              false
          methodDef
              "BaseRate"
              [ QuestionForm.Polar, 1.0; QuestionForm.Which, 0.75; QuestionForm.What, 0.45 ]
              [ "predictive", 1.0; "comparative", 0.75 ]
              0.8
              false
          methodDef
              "Dialectic"
              [ QuestionForm.Why, 0.75; QuestionForm.Polar, 0.8; QuestionForm.How, 0.55 ]
              [ "conflict", 1.0; "explanatory", 0.7; "comparative", 0.7 ]
              0.9
              false
          methodDef
              "Falsification"
              [ QuestionForm.Polar, 1.0; QuestionForm.Why, 0.65; QuestionForm.Which, 0.6 ]
              [ "falsification", 1.0; "predictive", 0.8; "causal", 0.55 ]
              0.8
              false
          methodDef
              "BoundarySearch"
              [ QuestionForm.Which, 0.9; QuestionForm.Polar, 0.75; QuestionForm.How, 0.6 ]
              [ "boundary", 1.0; "comparative", 0.8; "predictive", 0.55 ]
              0.9
              false
          methodDef
              "SourceTriangulation"
              [ QuestionForm.What, 0.65; QuestionForm.Polar, 0.75; QuestionForm.Why, 0.55 ]
              [ "evidence", 1.0; "predictive", 0.7; "factual", 0.9 ]
              1.0
              false
          methodDef
              "MeasurementCritique"
              [ QuestionForm.What, 0.65; QuestionForm.Why, 0.7; QuestionForm.Polar, 0.75 ]
              [ "measurement", 1.0; "predictive", 0.75; "causal", 0.6 ]
              1.0
              false
          methodDef
              "OntologyRepair"
              [ QuestionForm.What, 0.8; QuestionForm.Why, 0.65; QuestionForm.Which, 0.7 ]
              [ "conceptual", 1.0; "conflict", 0.8; "classification", 0.9 ]
              1.0
              false
          methodDef
              "UnknownExpansion"
              [ QuestionForm.Why, 0.65; QuestionForm.How, 0.65; QuestionForm.Polar, 0.7 ]
              [ "uncertain", 1.0; "predictive", 0.65; "exploratory", 1.0 ]
              1.2
              false
          methodDef
              "ScaleShift"
              [ QuestionForm.Why, 0.85; QuestionForm.How, 0.75 ]
              [ "causal", 0.75; "multi-scale", 1.0; "explanatory", 0.8 ]
              1.0
              false
          methodDef
              "ExperimentDesign"
              [ QuestionForm.Polar, 0.9; QuestionForm.How, 0.75; QuestionForm.Why, 0.65 ]
              [ "causal", 0.8; "falsification", 0.95; "experimental", 1.0 ]
              1.2
              false ]

    let phase0Names =
        set [ "Multidisciplinary"; "Abduction"; "Analogy"; "Counterexample"; "Synthesis" ]

    let private weightedScore weights belief defaultWeight =
        belief
        |> Map.fold
            (fun score key probability ->
                score
                + probability * (weights |> Map.tryFind key |> Option.defaultValue defaultWeight))
            0.0

    let private facetScore (definition: MethodDefinition) (facets: Map<string, float>) =
        if Map.isEmpty facets then
            0.25
        else
            let total = facets |> Map.fold (fun sum _ p -> sum + max 0.0 p) 0.0

            if total <= 0.0 then
                0.25
            else
                facets
                |> Map.fold
                    (fun score facet p ->
                        score
                        + max 0.0 p
                          * (definition.FacetWeights |> Map.tryFind facet |> Option.defaultValue 0.2))
                    0.0
                |> fun score -> score / total

    let utility (state: EpistemicState) (definition: MethodDefinition) =
        match state.RootContract with
        | None -> 0.0
        | Some root ->
            let form = weightedScore definition.FormWeights root.FormBelief 0.2
            let facets = facetScore definition root.Facets

            let unresolved =
                state.Actions
                |> Map.toSeq
                |> Seq.filter (fun (_, action) -> action.Status <> ActionStatus.Resolved)
                |> Seq.length
                |> float

            let synthesisReadiness =
                if definition.CombinesExistingKnowledge then
                    if Map.isEmpty state.Findings then
                        -0.7
                    else
                        min 0.55 (0.12 * float state.Findings.Count)
                else
                    0.0

            let saturationPenalty =
                if definition.CombinesExistingKnowledge then
                    0.0
                else
                    min 0.35 (0.04 * unresolved)

            0.58 * form + 0.42 * facets + synthesisReadiness
            - saturationPenalty
            - 0.08 * definition.BaseCost

    let activate (state: EpistemicState) =
        library
        |> List.map (fun definition -> definition, utility state definition)
        |> List.filter (fun (_, score) -> score >= 0.28)
        |> List.sortByDescending snd
        |> List.map (fst >> fun definition -> definition.Name)

    let generationMethods (state: EpistemicState) =
        activate state |> List.filter ((<>) "Synthesis")

    let synthesisAvailable (state: EpistemicState) =
        not (Map.isEmpty state.Findings)
        && (activate state |> List.contains "Synthesis")
