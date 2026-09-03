namespace Wanxiangshu.Sphinx.Plugins.Bayes

open Wanxiangshu.Sphinx.Core

/// WHAT[EPI-009]: exact posterior over finite discrete hypotheses from qualified factors.
module Exact =
    /// One discrete hypothesis with a nonnegative prior weight.
    type Hypothesis = { Key: string; Prior: float }

    /// One conditionally independent observation factor.
    type Factor =
        { SemanticKey: string
          DependencyKey: string
          Likelihoods: Map<string, float>
          Qualified: bool }

    /// Normalized exact posterior with its log partition function.
    type Posterior =
        { Probabilities: Map<string, float>
          LogPartition: float
          UsedFactors: string list }

    /// Typed reason the exact slot must stay empty instead of guessing.
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

    val code: fault: ExactFault -> string
    val message: fault: ExactFault -> string
    val toCoreError: fault: ExactFault -> CoreError
    val infer: hypotheses: Hypothesis list -> factors: Factor list -> Result<Posterior, ExactFault>
