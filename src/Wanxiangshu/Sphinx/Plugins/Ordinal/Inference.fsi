namespace Wanxiangshu.Sphinx.Plugins.Ordinal

module Inference =
    type Ballot =
        { Ranks: string list list }

    type BordaInput =
        { Candidates: string list
          Ballots: Ballot list }

    type BordaError =
        | EmptyCandidates
        | EmptyBallots
        | DuplicateCandidate of string
        | UnknownCandidate of int * string
        | DuplicateRank of int * string
        | EmptyTier of int
        | EmptyBallot of int

    type BordaOutcome =
        { Scores: Map<string, float>
          MeanScores: Map<string, float>
          Ranking: string list
          Exposure: Map<string, int>
          Extension: string
          Complete: bool
          Guarantees: string list
          Assumptions: Set<string> }

    type Contest =
        { First: string
          Second: string
          FirstWins: int
          SecondWins: int }

    type BtlInput =
        { Candidates: string list
          Contests: Contest list
          Regularization: float
          Tolerance: float
          MaxIterations: int }

    /// Newton diagnostics. RequestedMaxIterations echoes the caller cap (L-1): when
    /// Iterations hits the internal cap below the request, Converged=false means capped.
    type BtlDiagnostics =
        { Iterations: int
          Converged: bool
          LogLikelihood: float
          GradientNorm: float
          MaxAbsStrength: float
          Regularization: float
          RequestedMaxIterations: int }

    type BtlUncertainty =
        { StandardErrors: Map<string, float> }

    type BtlOutcome =
        { Strengths: Map<string, float>
          Appearances: Map<string, int>
          Diagnostics: BtlDiagnostics
          Uncertainty: BtlUncertainty
          Assumptions: Set<string> }

    type BtlError =
        | EmptyCandidates
        | DuplicateCandidate of string
        | EmptyContests
        | UnknownCandidate of string
        | SelfContest of string
        | NonPositiveWins of string
        | InvalidRegularization
        | InvalidTolerance
        | InvalidMaxIterations
        | NonFiniteEstimate
        | SingularHessian
        | Unidentifiable of string list list

    val maxNewtonIterations: int
    val borda: input: BordaInput -> Result<BordaOutcome, BordaError>
    val bordaErrorCode: BordaError -> string
    val bradleyTerry: input: BtlInput -> Result<BtlOutcome, BtlError>
    val btlErrorCode: BtlError -> string
