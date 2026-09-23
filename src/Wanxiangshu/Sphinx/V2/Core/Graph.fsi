namespace Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type GraphRole =
    | Epistemic
    | WorkDependency
    | PlanTree
    | RefinerState

type GraphNode =
    {
        Id: NodeId
        Role: GraphRole
        /// Core compares this by identity only; it never reads the semantic kind.
        Kind: string
        Payload: JsonEnvelope
        Revision: Revision
        ContentHash: string
    }

type HyperEdge =
    {
        Id: EdgeId
        Tails: Set<NodeId>
        Heads: Set<NodeId>
        /// Core compares this by identity only; it never reads the relation.
        Relation: string
        Payload: JsonEnvelope option
        Revision: Revision
    }

type GraphPatch =
    { UpsertNodes: GraphNode list
      RemoveNodes: NodeId list
      UpsertEdges: HyperEdge list
      RemoveEdges: EdgeId list }

type GraphError = { Code: string; Message: string }

module GraphRole =
    val name: GraphRole -> string

module Graph =
    val applyPatch:
        Map<NodeId, GraphNode> ->
        Map<EdgeId, HyperEdge> ->
        GraphPatch ->
            Result<Map<NodeId, GraphNode> * Map<EdgeId, HyperEdge>, GraphError>
