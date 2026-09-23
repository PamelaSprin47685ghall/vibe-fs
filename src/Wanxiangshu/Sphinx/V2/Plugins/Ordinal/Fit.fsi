namespace Wanxiangshu.Sphinx.V2.Plugins

[<RequireQualifiedAccess>]
type FitStatus =
    | Converged
    | NoDirectionalEvidence
    | DisconnectedComponents of components: int
    | SeparationDetected
    | NotConverged of iterations: int
    | LineSearchFailed of iterations: int

type FitResult =
    { Status: FitStatus
      /// Theta in the declared gauge (zero-sum by default).
      Theta: Map<string, float>
      /// Beta, or None when the design cannot identify a position effect.
      Beta: float option
      Kappa: float option
      /// Full covariance in the free coordinates, indexed by candidate id.
      Covariance: Map<string * string, float>
      Iterations: int
      GradientNorm: float
      /// The approximation actually used, named in the result.
      EstimateKind: string
      ModelRef: string
      Assumptions: string list }

type FitError = { Code: string; Message: string }

module Fit =
    val fit:
        ObservationModel ->
        Ballot list ->
        string list ->
        maxIterations: int ->
        gradientTolerance: float ->
            Result<FitResult, FitError>

    /// Sigma_ii + Sigma_jj - 2 Sigma_ij, never 1/sqrt(N).
    val contrastVariance: string -> string -> FitResult -> float option
    val usable: FitResult -> bool
