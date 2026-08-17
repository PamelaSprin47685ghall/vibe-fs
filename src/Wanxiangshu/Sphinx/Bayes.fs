namespace Wanxiangshu.Sphinx

open System

module Bayes =

    let private finite value =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private normalize (distribution: Map<string, float>) =
        let clean =
            distribution
            |> Map.map (fun _ value -> if finite value then max 0.0 value else 0.0)

        let total = clean |> Map.fold (fun sum _ value -> sum + value) 0.0

        if total <= 0.0 then
            Map.empty
        else
            clean |> Map.map (fun _ value -> value / total)

    let private entropy posterior =
        posterior
        |> Map.fold
            (fun value _ probability ->
                if probability <= 0.0 then
                    value
                else
                    value - probability * log probability / log 2.0)
            0.0

    let private risk posterior =
        if Map.isEmpty posterior then
            1.0
        else
            1.0 - (posterior |> Map.toSeq |> Seq.map snd |> Seq.max)

    let private initialPrior (hypotheses: Map<string, Hypothesis>) =
        let explicit =
            hypotheses
            |> Map.toSeq
            |> Seq.choose (fun (key, hypothesis) -> hypothesis.Prior |> Option.map (fun prior -> key, prior))
            |> Map.ofSeq

        if explicit.Count = hypotheses.Count && hypotheses.Count > 0 then
            normalize explicit
        elif hypotheses.Count > 0 then
            let p = 1.0 / float hypotheses.Count
            hypotheses |> Map.map (fun _ _ -> p)
        else
            Map.empty

    let private validLikelihood (hypothesisKeys: Set<string>) (evidence: Evidence) =
        evidence.NumericQualified
        && evidence.Likelihoods.Count = hypothesisKeys.Count
        && (evidence.Likelihoods
            |> Map.forall (fun key value ->
                Set.contains key hypothesisKeys && finite value && value >= 0.0 && value <= 1.0))

    let private independentQualifiedEvidence hypothesisKeys (evidence: Map<string, Evidence>) =
        evidence
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.filter (validLikelihood hypothesisKeys)
        |> Seq.groupBy (fun item -> item.DependencyKey)
        |> Seq.map (fun (_, group) -> group |> Seq.sortBy (fun item -> item.SemanticKey) |> Seq.head)
        |> Seq.toList

    let private beliefFromQualified (state: EpistemicState) (qualified: Evidence list) : BayesianBelief option =
        let posterior =
            qualified
            |> List.fold
                (fun current evidence ->
                    current
                    |> Map.map (fun key prior -> prior * evidence.Likelihoods[key])
                    |> normalize)
                (initialPrior state.Hypotheses)

        if Map.isEmpty posterior then
            None
        else
            Some
                { Posterior = posterior
                  Entropy = entropy posterior
                  BayesRisk = risk posterior }

    let private updateEligible (state: EpistemicState) : BayesianBelief option =
        let hypothesisKeys = state.Hypotheses |> Map.toSeq |> Seq.map fst |> Set.ofSeq
        let qualified = independentQualifiedEvidence hypothesisKeys state.Evidence

        if List.isEmpty qualified then
            None
        else
            beliefFromQualified state qualified

    let update (state: EpistemicState) : BayesianBelief option =
        if state.Hypotheses.Count < 2 then
            None
        else
            updateEligible state

    let likelihoodQualified (state: EpistemicState) =
        match update state with
        | Some _ -> true
        | None -> false

    let posteriorFor key state =
        state.Bayesian |> Option.bind (fun belief -> Map.tryFind key belief.Posterior)

    let frozenInference (hypotheses: Hypothesis list) (evidence: Evidence list) =
        let state =
            { State.create "bayesian inference" with
                Hypotheses = hypotheses |> List.map (fun h -> h.SemanticKey, h) |> Map.ofList
                Evidence = evidence |> List.map (fun e -> e.SemanticKey, e) |> Map.ofList }

        update state
