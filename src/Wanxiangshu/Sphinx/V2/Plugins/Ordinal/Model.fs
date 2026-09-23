namespace Wanxiangshu.Sphinx.V2.Plugins

/// The observation model declarations.
///
/// WHAT[sphinx-v2-025]: every likelihood is stated with its numeric form and its
/// assumptions. The old code had a working BTL gradient but no stable log-space
/// evaluation, a `1/sqrt(N)` stand-in for standard error, and no tie handling at all.
/// Here the model, the gauge and the tie mechanism are explicit, and the covariance is
/// a real covariance.
/// How a comparison record came to be. The position term `o` is only estimable when the
/// design genuinely produced both positions.
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
        /// Present only when the family uses one.
        TieKappaPriorMean: float
        TieKappaPriorVariance: float
        OrderL2: float
    }

module OrdinalModel =

    let defaultModel: ObservationModel =
        { ModelRef = "ordinal.pairwise-btl@2"
          Family = "pairwise-btl"
          Gauge = "zero-sum"
          ThetaL2 = 1.0
          TieKappaPriorMean = 0.0
          TieKappaPriorVariance = 4.0
          OrderL2 = 1.0 }

    let tieAwareModel: ObservationModel =
        { defaultModel with
            ModelRef = "ordinal.pairwise-tie-aware@2"
            Family = "pairwise-tie-aware"
            TieKappaPriorMean = 0.0
            TieKappaPriorVariance = 4.0 }

    /// The cluster a ballot belongs to is the independence unit. Two ballots in the
    /// same cluster are not two samples (WHAT[sphinx-v2-014]).
    let clusterOf (ballot: Ballot) : string = ballot.ClusterId

    /// A design can only estimate the position term if it actually produced both
    /// orders; otherwise the term is fixed at zero and recorded as unestimated.
    let positionTerm (ballot: Ballot) : float =
        match ballot.PositionIdentifiable with
        | true -> float ballot.Position
        | false -> 0.0
