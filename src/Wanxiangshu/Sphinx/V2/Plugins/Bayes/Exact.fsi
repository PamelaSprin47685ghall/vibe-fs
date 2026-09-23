namespace Wanxiangshu.Sphinx.V2.Plugins

type Hypothesis = { Key: string; Prior: float }

/// One observation's contribution. `ObservationId` is its identity: the same id
/// delivered twice is one observation, never two.
type Factor =
    { ObservationId: string
      DependencyKey: string
      Likelihoods: Map<string, float>
      Qualified: bool }

type Posterior =
    { Probabilities: Map<string, float>
      LogPartition: float
      UsedObservations: string list
      /// Observations dropped by a declared conservative rule, with the reason.
      Dropped: (string * string) list }

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

module Bayes =
    val infer: Hypothesis list -> Factor list -> Result<Posterior, ExactFault>

    /// A prior-only run is legal and says so: it is not new evidence and not convergence.
    val priorOnly: Hypothesis list -> Result<Posterior, ExactFault>
