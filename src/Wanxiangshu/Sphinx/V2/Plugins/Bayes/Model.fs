namespace Wanxiangshu.Sphinx.V2.Plugins

/// The F# vocabulary of the Sphinx v2 exact Bayes posterior. The inference itself
/// (WHAT[sphinx-v2-009/021/025/026]) is owned by `wanxiangli/bayes` (CUT-31 ②);
/// `Surface` translates these records across that boundary.

type Hypothesis = { Key: string; Prior: float }

/// One observation's contribution. `ObservationId` is its identity: the same id
/// delivered twice is one observation, never two.
type Factor =
    { ObservationId: string
      DependencyKey: string
      Likelihoods: Map<string, float>
      Qualified: bool }

type Posterior =
    {
        Probabilities: Map<string, float>
        LogPartition: float
        UsedObservations: string list
        /// Observations dropped by a declared conservative rule, with the reason.
        Dropped: (string * string) list
    }

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
