namespace Wanxiangshu.Sphinx.V2.Plugins

open Wanxiangshu.Sphinx.V2.Core

type GraphDelta =
    { Nodes: GraphNode list
      Edges: HyperEdge list
      /// Certificate slots the delta proposes.
      CertificatePatches: CertificateSlotPatch list
      /// Source observation this delta was derived from.
      SourceObservation: string
      /// Nothing new was learned; the inquiry revision must not move.
      Empty: bool }

[<RequireQualifiedAccess>]
type ObserveFault =
    | UnknownArtifactRef of artifactRef: string
    | UnsupportedJudgment of judgment: string
    | MissingConditionRef
    | BlankSourceObservation

module Observe =
    /// An empty delta is a legitimate outcome and must not advance the revision.
    val empty: string -> GraphDelta

    /// A directional preference becomes a typed edge; an abstention becomes nothing.
    val fromJudgment: string -> string -> string -> Judgment -> Result<GraphDelta, ObserveFault>

    /// A conditional outcome is a new investigation candidate, not a missing vote.
    val fromConditional: string -> string -> Result<GraphDelta, ObserveFault>
