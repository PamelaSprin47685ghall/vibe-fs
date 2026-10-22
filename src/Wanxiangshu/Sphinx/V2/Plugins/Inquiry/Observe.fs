namespace Wanxiangshu.Sphinx.V2.Plugins

open Wanxiangshu.Sphinx.V2.Core

/// Turning a raw worker response into a typed graph delta.
///
/// WHAT[sphinx-v2-021]: acceptance and interpretation are two separate recoverable
/// stages. The first records the structure-legal result and the work's completion; the
/// second runs this pure function over the stored bytes. A plugin exception therefore
/// never loses the raw answer, and re-interpreting needs no paid call.
///
/// WHAT[sphinx-v2-003]: an `abstain` is recorded as an abstention, never converted into
/// a tie or a half point. The reducer sees it as a result with no direction.

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

    /// A node id derived from the source observation and a local kind, so two
    /// interpretations of the same observation cannot collide by accident.
    let private nodeId (sourceObservation: string) (localId: string) : NodeId =
        NodeId.create (sprintf "n_%s_%s" sourceObservation localId)

    /// An empty delta. Producing one is a legitimate outcome and must not advance the
    /// inquiry revision (WHAT[sphinx-v2-027]).
    let empty (sourceObservation: string) : GraphDelta =
        { Nodes = []
          Edges = []
          CertificatePatches = []
          SourceObservation = sourceObservation
          Empty = true }
