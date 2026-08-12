namespace Wanxiangshu.Sphinx

type Request =
    | SemanticAssessmentRequest of question: string
    | GenerateCandidatesRequest of methods: string list * root: RootContract
    | InvestigateRequest of action: CognitiveAction
    | SynthesizeRequest of findingKeys: string list * root: RootContract

type Observation =
    | SemanticAssessmentObservation of SemanticAssessment
    | CandidatesObservation of CandidateProposal list
    | InvestigationObservation of InvestigationResult
    | SynthesisObservation of SynthesisProposal

type BayesianBelief =
    { Posterior: Map<string, float>
      Entropy: float
      BayesRisk: float }

type SearchNode =
    { SemanticKey: string
      PathCost: float
      HeuristicCost: float
      Priority: float
      Closed: bool }

type MonteCarloNode =
    { SemanticKey: string
      Visits: int
      ValueSum: float
      Prior: float }

type RepresentationState =
    { EquivalenceClasses: Map<string, string list>
      ParetoFrontiers: Map<string, string list>
      Representatives: Map<string, string>
      EstimatedFutureCost: float }

type Budget =
    { MaxYields: int
      UsedYields: int
      MaxCost: float
      UsedCost: float }

type EpistemicState =
    { RootQuestion: string
      RootContract: RootContract option
      Findings: Map<string, Finding>
      Evidence: Map<string, Evidence>
      Hypotheses: Map<string, Hypothesis>
      Dependencies: Map<string, Set<string>>
      Actions: Map<string, CognitiveAction>
      Budget: Budget
      PendingRequest: Request option
      Synthesis: SynthesisProposal option
      Bayesian: BayesianBelief option
      Search: Map<string, SearchNode>
      MonteCarlo: Map<string, MonteCarloNode>
      Representation: RepresentationState
      SolverMode: SolverMode
      NeedsGeneration: bool
      Revision: int }

type CanonicalAnswer =
    { Question: string
      Contract: RootContract
      Findings: Finding list
      Evidence: Evidence list
      Hypotheses: Hypothesis list
      Synthesis: SynthesisProposal option
      Bayesian: BayesianBelief option
      Uncertainties: string list
      StopReason: string
      Revision: int }

[<RequireQualifiedAccess>]
type InquiryResult =
    | Yield of Request
    | Answered of CanonicalAnswer
    | Error of string
