namespace Wanxiangshu.Sphinx.V2.Plugins

type BallotKind =
    /// A strict preference between two candidates.
    | Directional of winner: string * loser: string
    /// An explicit tie, with its own mechanism.
    | Tie of left: string * right: string
    /// An abstention: no directional information, but not missing either.
    | Abstention
    /// A conditional outcome pointing at a new investigation.
    | Conditional of conditionRef: string

type Ballot =
    {
        BallotId: string
        ScopeId: string
        ClusterId: string
        Kind: BallotKind
        /// +1 when the canonical left candidate was shown first, -1 otherwise.
        Position: int
        /// True when the design can actually identify the position term.
        PositionIdentifiable: bool
    }

type ObservationModel =
    {
        ModelRef: string
        /// "pairwise-btl" or "pairwise-tie-aware".
        Family: string
        /// "zero-sum" or an orthogonal basis description.
        Gauge: string
        ThetaL2: float
        TieKappaPriorMean: float
        TieKappaPriorVariance: float
        OrderL2: float
    }

module OrdinalModel =
    val defaultModel: ObservationModel
    val tieAwareModel: ObservationModel
    val clusterOf: Ballot -> string
    val positionTerm: Ballot -> float
