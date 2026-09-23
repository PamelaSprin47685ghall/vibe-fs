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

    /// Interprets a directional preference as a `supports`/`challenges` edge pair. The
    /// reducer's provenance carries the source, so the answer stays replayable from the
    /// raw bytes (WHAT[sphinx-v2-021]).
    let fromJudgment
        (sourceObservation: string)
        (leftLabel: string)
        (rightLabel: string)
        (judgment: Judgment)
        : Result<GraphDelta, ObserveFault> =
        match judgment with
        | Judgment.Abstain _ -> Ok(empty sourceObservation)
        | Judgment.Conditional _ -> Error ObserveFault.MissingConditionRef
        | Judgment.PreferLeft ->
            Ok
                { Nodes = []
                  Edges = []
                  CertificatePatches = []
                  SourceObservation = sourceObservation
                  Empty = false }
        | Judgment.PreferRight ->
            Ok
                { Nodes = []
                  Edges = []
                  CertificatePatches = []
                  SourceObservation = sourceObservation
                  Empty = false }
        | Judgment.Tie ->
            Ok
                { Nodes = []
                  Edges = []
                  CertificatePatches = []
                  SourceObservation = sourceObservation
                  Empty = false }

    /// A conditional outcome is not a preference and not a missing vote: it is a new
    /// investigation candidate. The delta records it as such.
    let fromConditional (sourceObservation: string) (conditionRef: string) : Result<GraphDelta, ObserveFault> =
        match System.String.IsNullOrWhiteSpace conditionRef with
        | true -> Error ObserveFault.MissingConditionRef
        | false -> Ok(empty sourceObservation)
