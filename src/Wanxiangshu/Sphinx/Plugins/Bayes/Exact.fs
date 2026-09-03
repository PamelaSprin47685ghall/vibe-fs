// WHAT[EPI-009]: Bayes exact refiner admits only qualified factors and normalizes in log space.

namespace Wanxiangshu.Sphinx.Plugins.Bayes

open System
open FsToolkit.ErrorHandling
open Wanxiangshu.Sphinx.Core

module Exact =

    type Hypothesis = { Key: string; Prior: float }

    type Factor =
        { SemanticKey: string
          DependencyKey: string
          Likelihoods: Map<string, float>
          Qualified: bool }

    type Posterior =
        { Probabilities: Map<string, float>
          LogPartition: float
          UsedFactors: string list }

    /// DSL-class: Vocabulary — stable typed failure-code taxonomy
    [<RequireQualifiedAccess>]
    type ExactFault =
        | TooFewHypotheses of count: int
        | BlankHypothesisKey
        | DuplicateHypothesisKey of key: string
        | InvalidPrior of key: string
        | ZeroPriorMass
        | BlankDependencyKey of semanticKey: string
        | NoQualifiedFactor
        | UnknownHypothesisKey of semanticKey: string * key: string
        | IncompleteLikelihood of semanticKey: string * missingKey: string
        | InvalidLikelihood of semanticKey: string * key: string
        | NonPositivePartition

    let code (fault: ExactFault) : string =
        match fault with
        | ExactFault.TooFewHypotheses _ -> "too-few-hypotheses"
        | ExactFault.BlankHypothesisKey -> "blank-hypothesis-key"
        | ExactFault.DuplicateHypothesisKey _ -> "duplicate-hypothesis-key"
        | ExactFault.InvalidPrior _ -> "invalid-prior"
        | ExactFault.ZeroPriorMass -> "zero-prior-mass"
        | ExactFault.BlankDependencyKey _ -> "blank-dependency-key"
        | ExactFault.NoQualifiedFactor -> "no-qualified-factor"
        | ExactFault.UnknownHypothesisKey _ -> "unknown-hypothesis-key"
        | ExactFault.IncompleteLikelihood _ -> "incomplete-likelihood"
        | ExactFault.InvalidLikelihood _ -> "invalid-likelihood"
        | ExactFault.NonPositivePartition -> "non-positive-partition"

    let message (fault: ExactFault) : string =
        match fault with
        | ExactFault.TooFewHypotheses count -> sprintf "exact posterior needs at least two hypotheses (got %d)" count
        | ExactFault.BlankHypothesisKey -> "hypothesis keys must not be blank"
        | ExactFault.DuplicateHypothesisKey key -> sprintf "hypothesis key is declared twice: %s" key
        | ExactFault.InvalidPrior key -> sprintf "prior for hypothesis %s must be a finite nonnegative number" key
        | ExactFault.ZeroPriorMass -> "priors must carry positive finite total mass"
        | ExactFault.BlankDependencyKey semanticKey ->
            sprintf "qualified factor %s must declare an explicit dependency key" semanticKey
        | ExactFault.NoQualifiedFactor -> "no qualified factor covers the hypothesis set"
        | ExactFault.UnknownHypothesisKey(semanticKey, key) ->
            sprintf "factor %s names unknown hypothesis %s" semanticKey key
        | ExactFault.IncompleteLikelihood(semanticKey, missingKey) ->
            sprintf "factor %s misses likelihood for hypothesis %s" semanticKey missingKey
        | ExactFault.InvalidLikelihood(semanticKey, key) ->
            sprintf "likelihood of factor %s for hypothesis %s must be a finite number in [0, 1]" semanticKey key
        | ExactFault.NonPositivePartition -> "partition function is not positive; no normalized posterior exists"

    let toCoreError (fault: ExactFault) : CoreError =
        { Code = code fault
          Message = message fault }

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    let rec private distinctKeys (seen: Set<string>) (remaining: Hypothesis list) : Result<unit, ExactFault> =
        match remaining with
        | [] -> Ok()
        | hypothesis :: rest when Set.contains hypothesis.Key seen ->
            Error(ExactFault.DuplicateHypothesisKey hypothesis.Key)
        | hypothesis :: rest -> distinctKeys (Set.add hypothesis.Key seen) rest

    let private checkHypotheses (hypotheses: Hypothesis list) : Result<unit, ExactFault> =
        if hypotheses.Length < 2 then
            Error(ExactFault.TooFewHypotheses hypotheses.Length)
        elif
            hypotheses
            |> List.exists (fun hypothesis -> String.IsNullOrWhiteSpace hypothesis.Key)
        then
            Error ExactFault.BlankHypothesisKey
        else
            distinctKeys Set.empty hypotheses

    let private findBadPrior (hypotheses: Hypothesis list) : Hypothesis option =
        hypotheses
        |> List.tryFind (fun hypothesis -> not (isFiniteNumber hypothesis.Prior) || hypothesis.Prior < 0.0)

    let private checkPriorTotal (total: float) : Result<unit, ExactFault> =
        if not (isFiniteNumber total) || total <= 0.0 then
            Error ExactFault.ZeroPriorMass
        else
            Ok()

    let private scalePrior (hypotheses: Hypothesis list) (total: float) : Map<string, float> =
        hypotheses
        |> List.map (fun hypothesis -> hypothesis.Key, hypothesis.Prior / total)
        |> Map.ofList

    let private normalizedPrior (hypotheses: Hypothesis list) : Result<Map<string, float>, ExactFault> =
        match findBadPrior hypotheses with
        | Some bad -> Error(ExactFault.InvalidPrior bad.Key)
        | None ->
            let total =
                hypotheses |> List.fold (fun sum hypothesis -> sum + hypothesis.Prior) 0.0

            checkPriorTotal total |> Result.map (fun () -> scalePrior hypotheses total)

    let private checkFactorDependency (factor: Factor) : Result<unit, ExactFault> =
        if String.IsNullOrWhiteSpace factor.DependencyKey then
            Error(ExactFault.BlankDependencyKey factor.SemanticKey)
        else
            Ok()

    let private checkUnknownKey (keys: Set<string>) (factor: Factor) : Result<unit, ExactFault> =
        match
            factor.Likelihoods
            |> Map.toList
            |> List.tryFind (fun (key, _) -> not (Set.contains key keys))
        with
        | Some(key, _) -> Error(ExactFault.UnknownHypothesisKey(factor.SemanticKey, key))
        | None -> Ok()

    let private checkMissingKey (keys: Set<string>) (factor: Factor) : Result<unit, ExactFault> =
        match
            keys
            |> Set.toList
            |> List.tryFind (fun key -> not (Map.containsKey key factor.Likelihoods))
        with
        | Some missing -> Error(ExactFault.IncompleteLikelihood(factor.SemanticKey, missing))
        | None -> Ok()

    let private checkLikelihoodRange (factor: Factor) : Result<unit, ExactFault> =
        match
            factor.Likelihoods
            |> Map.toList
            |> List.tryFind (fun (_, value) -> not (isFiniteNumber value) || value < 0.0 || value > 1.0)
        with
        | Some(key, _) -> Error(ExactFault.InvalidLikelihood(factor.SemanticKey, key))
        | None -> Ok()

    let private checkFactor (keys: Set<string>) (factor: Factor) : Result<unit, ExactFault> =
        result {
            do! checkFactorDependency factor
            do! checkUnknownKey keys factor
            do! checkMissingKey keys factor
            do! checkLikelihoodRange factor
        }

    let private checkFactors (keys: Set<string>) (factors: Factor list) : Result<unit, ExactFault> =
        factors |> List.traverseResultM (checkFactor keys) |> Result.map (fun _ -> ())

    let private posteriorFrom (prior: Map<string, float>) (factors: Factor list) : Result<Posterior, ExactFault> =
        let orderedKeys = prior |> Map.toList |> List.map fst

        let used =
            factors
            |> List.groupBy (fun factor -> factor.DependencyKey)
            |> List.map (fun (_, group) -> group |> List.sortBy (fun factor -> factor.SemanticKey) |> List.head)
            |> List.sortBy (fun factor -> factor.SemanticKey)

        let logMass key =
            used
            |> List.fold
                (fun mass factor -> mass + Math.Log(Map.find key factor.Likelihoods))
                (Math.Log(Map.find key prior))

        let scored = orderedKeys |> List.map (fun key -> key, logMass key)
        let peak = scored |> List.map snd |> List.max

        if peak = Double.NegativeInfinity then
            Error ExactFault.NonPositivePartition
        else
            let normalizer =
                scored |> List.fold (fun sum (_, s) -> sum + Math.Exp(s - peak)) 0.0

            let logPartition = peak + Math.Log normalizer

            Ok
                { Probabilities =
                    scored
                    |> List.map (fun (key, s) -> key, Math.Exp(s - logPartition))
                    |> Map.ofList
                  LogPartition = logPartition
                  UsedFactors = used |> List.map (fun factor -> factor.SemanticKey) }

    let private checkQualified (qualified: Factor list) : Result<unit, ExactFault> =
        if List.isEmpty qualified then
            Error ExactFault.NoQualifiedFactor
        else
            Ok()

    let infer (hypotheses: Hypothesis list) (factors: Factor list) : Result<Posterior, ExactFault> =
        result {
            do! checkHypotheses hypotheses
            let! prior = normalizedPrior hypotheses
            let keys = prior |> Map.toSeq |> Seq.map fst |> Set.ofSeq
            let qualified = factors |> List.filter (fun factor -> factor.Qualified)
            do! checkFactors keys qualified
            do! checkQualified qualified
            return! posteriorFrom prior qualified
        }
