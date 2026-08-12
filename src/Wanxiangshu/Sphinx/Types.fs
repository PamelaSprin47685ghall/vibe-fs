namespace Wanxiangshu.Sphinx

[<RequireQualifiedAccess>]
type QuestionForm =
    | Why
    | How
    | What
    | Who
    | Where
    | When
    | Which
    | Polar
    | Other

[<RequireQualifiedAccess>]
type AnswerContract =
    | Explanation
    | Plan
    | Direct
    | Ranking
    | Judgment
    | Credence

[<RequireQualifiedAccess>]
type ActionKind =
    | Investigate
    | Synthesize

[<RequireQualifiedAccess>]
type ActionStatus =
    | Open
    | Selected
    | Resolved

[<RequireQualifiedAccess>]
type SolverMode =
    | Bellman
    | BestFirst
    | MonteCarlo

[<RequireQualifiedAccess>]
type EvidenceKind =
    | Document
    | Tool
    | UserSupplied
    | Measurement
    | Dataset
    | Other

type SemanticAssessment =
    { Forms: Map<QuestionForm, float>
      Facets: Map<string, float>
      Targets: string list
      Intents: string list }

type RootContract =
    { FormBelief: Map<QuestionForm, float>
      ContractBelief: Map<AnswerContract, float>
      Facets: Map<string, float>
      Targets: string list
      Intents: string list }

type EvidenceSource =
    { Id: string
      Kind: EvidenceKind
      Label: string option }

type Evidence =
    { SemanticKey: string
      Proposition: string
      Source: EvidenceSource
      DependencyKey: string
      Likelihoods: Map<string, float>
      Reliability: float option
      NumericQualified: bool
      Provenance: string list }

type Finding =
    { SemanticKey: string
      Text: string
      Supports: string list
      Refutes: string list
      EvidenceKeys: string list
      Confidence: float option
      Provenance: string list }

type Hypothesis =
    { SemanticKey: string
      Label: string
      Prior: float option }

type CandidateProposal =
    { Method: string
      Question: string
      SemanticKey: string
      DependencyKey: string option
      ExpectedRootGain: float
      GatewayGain: float
      Cost: float
      Provenance: string list }

type CognitiveAction =
    { Id: string
      Kind: ActionKind
      Method: string
      Question: string
      SemanticKey: string
      EquivalenceKey: string option
      DependencyKey: string option
      ExpectedRootGain: float
      GatewayGain: float
      Cost: float
      Value: float
      Status: ActionStatus
      Provenance: string list }

type InvestigationResult =
    { ActionKey: string
      SemanticAssessment: SemanticAssessment option
      Findings: Finding list
      Evidence: Evidence list
      Hypotheses: Hypothesis list
      Candidates: CandidateProposal list }

type SynthesisProposal =
    { Text: string
      FindingKeys: string list
      Uncertainties: string list }
