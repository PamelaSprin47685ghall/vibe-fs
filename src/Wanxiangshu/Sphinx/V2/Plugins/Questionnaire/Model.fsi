namespace Wanxiangshu.Sphinx.V2.Plugins

[<RequireQualifiedAccess>]
type Judgment =
    | PreferLeft
    | PreferRight
    | Tie
    | Abstain of reason: string
    | Conditional of conditionRef: string

type PairwiseResponse =
    {
        Judgment: Judgment
        Rationale: string
        SourceLabels: string list
        /// Local references to plan cards attached to this response.
        ProposedAlternatives: ProposedAlternative list
    }

and ProposedAlternative =
    { LocalId: string
      Description: string
      ExpectedContribution: string }

type RankingResponse =
    { PresentedSet: string list
      RankedTiers: string list list
      Best: string option
      Worst: string option
      Unjudged: string list }

type MaxDiffObservation =
    { PresentedSet: string list
      Best: string
      Worst: string }

type Observation =
    {
        ObservationId: string
        ScopeId: string
        SnapshotId: string
        /// measurement | intervention.
        Purpose: string
        QuestionId: string
        QuestionHash: string
        TemplateRef: string
        ClusterId: string
        /// The exact bytes the model saw.
        VisibleBytesHash: string
        CanonicalResult: string
    }

module QuestionnaireModel =
    val isDirectional: Judgment -> bool
    val isTie: Judgment -> bool
    val isAbstention: Judgment -> bool
    val isConditional: Judgment -> bool
    val labelsWithin: Set<string> -> PairwiseResponse -> Result<unit, string>
    val uniqueLocalIds: PairwiseResponse -> Result<unit, string>
