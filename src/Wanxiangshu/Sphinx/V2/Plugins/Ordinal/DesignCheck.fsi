namespace Wanxiangshu.Sphinx.V2.Plugins

type ConnectivityReport =
    { Components: string list list
      Connected: bool
      /// Candidates that never appear in any ballot.
      Isolated: string list }

type DesignRankReport =
    { /// Number of identifiable contrasts in the free coordinates.
      Rank: int
      ExpectedRank: int
      Sufficient: bool
      /// True when the design can identify a position effect at all.
      PositionIdentifiable: bool
      /// True when one candidate always wins or always loses.
      SeparationDetected: bool }

module DesignCheck =
    val connectivity: Ballot list -> string list -> ConnectivityReport
    val designRank: Ballot list -> string list -> DesignRankReport
