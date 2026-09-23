namespace Wanxiangshu.Sphinx.V2.Core

open System

/// The shared versioned graph store.
///
/// WHAT[sphinx-v2-005]: one physical graph form serves four logical roles, and the
/// role is carried by the node's `Role` field, not by four separate stores that would
/// have to be kept in sync. The epistemic role may contain cycles (circular support,
/// mutually conditioned hypotheses); the work-dependency role must be acyclic; the
/// plan role may branch; a mathematical refiner follows its own model conditions.
/// Nothing in the reducer enforces acyclicity on the epistemic role, because deleting
/// a semantic cycle to satisfy a scheduler would destroy exactly what the inquiry
/// discovered.

[<RequireQualifiedAccess>]
type GraphRole =
    /// Semantic inquiry graph: interpretations, conditions, relations, candidates.
    | Epistemic
    /// Work dependency DAG: must be acyclic.
    | WorkDependency
    /// Plan tree: may contain outcome branches.
    | PlanTree
    /// A mathematical refiner's own state graph.
    | RefinerState

type GraphNode =
    {
        Id: NodeId
        Role: GraphRole
        /// Core compares this by identity only; it never reads the semantic kind.
        Kind: string
        Payload: JsonEnvelope
        Revision: Revision
        /// Content hash of the payload as first accepted; a revision keeps its own.
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
    let name (role: GraphRole) : string =
        match role with
        | GraphRole.Epistemic -> "epistemic"
        | GraphRole.WorkDependency -> "work-dependency"
        | GraphRole.PlanTree -> "plan-tree"
        | GraphRole.RefinerState -> "refiner-state"

module Graph =

    /// Core checks the endpoints exist, the revision is valid, the schema is present
    /// and the reference is reachable. It does not judge whether a `supports` relation
    /// is warranted — that is the producing plugin's semantic responsibility.
    let private checkNode (node: GraphNode) : Result<unit, GraphError> =
        if System.String.IsNullOrWhiteSpace node.Kind then
            Error
                { Code = "invalid-node"
                  Message = "graph node kind must not be blank" }
        elif System.String.IsNullOrWhiteSpace node.ContentHash then
            Error
                { Code = "invalid-node"
                  Message = "graph node content hash must not be blank" }
        else
            Ok()

    let applyPatch
        (nodes: Map<NodeId, GraphNode>)
        (edges: Map<EdgeId, HyperEdge>)
        (patch: GraphPatch)
        : Result<Map<NodeId, GraphNode> * Map<EdgeId, HyperEdge>, GraphError> =
        let nodesAfterRemoval =
            patch.RemoveNodes
            |> List.fold (fun graph nodeId -> Map.remove nodeId graph) nodes

        let nodesAfterUpsert =
            patch.UpsertNodes
            |> List.fold (fun graph node -> Map.add node.Id node graph) nodesAfterRemoval

        let removedNodes = patch.RemoveNodes |> Set.ofList

        let edgesAfterRemoval =
            edges
            |> Map.filter (fun edgeId edge ->
                not (List.contains edgeId patch.RemoveEdges)
                && Set.intersect removedNodes edge.Tails |> Set.isEmpty
                && Set.intersect removedNodes edge.Heads |> Set.isEmpty)

        let edgesAfterUpsert =
            patch.UpsertEdges
            |> List.fold (fun graph edge -> Map.add edge.Id edge graph) edgesAfterRemoval

        let nodeChecks =
            patch.UpsertNodes
            |> List.fold (fun result node -> result |> Result.bind (fun () -> checkNode node)) (Ok())

        // A dangling endpoint is a real defect in the producing plugin, never something
        // to paper over with a placeholder. The caller must emit the node first.
        let dangling =
            edgesAfterUpsert
            |> Map.toList
            |> List.collect (fun (_, edge) ->
                [ Set.union edge.Tails edge.Heads ]
                |> List.collect Set.toList
                |> List.filter (fun nodeId -> not (Map.containsKey nodeId nodesAfterUpsert)))

        if not (List.isEmpty dangling) then
            Error
                { Code = "dangling-edge"
                  Message =
                    sprintf
                        "hyperedge endpoints must exist: %s"
                        (String.concat ", " (dangling |> List.truncate 4 |> List.map NodeId.value)) }
        else
            nodeChecks |> Result.map (fun () -> nodesAfterUpsert, edgesAfterUpsert)
