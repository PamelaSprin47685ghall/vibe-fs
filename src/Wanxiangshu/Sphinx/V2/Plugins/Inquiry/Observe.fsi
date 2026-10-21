namespace Wanxiangshu.Sphinx.V2.Plugins

open Wanxiangshu.Sphinx.V2.Core

type GraphDelta =
    {
        Nodes: GraphNode list
        Edges: HyperEdge list
        /// Certificate slots the delta proposes.
        CertificatePatches: CertificateSlotPatch list
        /// Source observation this delta was derived from.
        SourceObservation: string
        /// Nothing new was learned; the inquiry revision must not move.
        Empty: bool
    }

[<RequireQualifiedAccess>]
type ObserveFault =
    | UnknownArtifactRef of artifactRef: string
    | UnsupportedJudgment of judgment: string
    | MissingConditionRef
    | BlankSourceObservation

module Observe =
    /// An empty delta is a legitimate outcome and must not advance the revision.
    val empty: string -> GraphDelta
