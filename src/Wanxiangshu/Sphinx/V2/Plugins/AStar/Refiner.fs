namespace Wanxiangshu.Sphinx.V2.Plugins

open System
open System.Collections.Generic

/// A* over a finite, deterministic, nonnegative-cost graph, with an incremental
/// interface.
///
/// WHAT[sphinx-v2-026]: the certificate this plugin can issue is a bound *in its own
/// declared model*. It is never a statement about the quality of an open-ended answer,
/// and a heuristic estimate from a language model is recorded as guidance, not as an
/// admissible bound.
///
/// WHAT[sphinx-v2-027]: the search is resumable. `initialize`, `step`, `snapshot` and
/// `restore` exist because a crash in the middle of a search must not restart it, and
/// because the OPEN/CLOSED frontier is the state a caller needs to see.

type GraphEdge = { FromNode: string; ToNode: string; Cost: float }

type AStarProblem =
    { Start: string
      Goal: string
      Edges: GraphEdge list
      /// Heuristic values. Guidance unless the caller declares them admissible.
      Heuristic: Map<string, float>
      /// True when the caller asserts the heuristic is admissible and consistent.
      HeuristicAdmissible: bool }

type SearchSnapshot =
    { Open: (string * float) list
      Closed: string list
      BestG: Map<string, float>
      Parents: Map<string, string>
      Expanded: string list
      /// The best known complete path cost, if any.
      Incumbent: float option
      ModelRevision: int }

[<RequireQualifiedAccess>]
type AStarFault =
    | BlankNode
    | DuplicateEdge of fromNode: string * toNode: string
    | NegativeCost of fromNode: string * toNode: string
    | NonFiniteCost of fromNode: string * toNode: string
    | UnknownStart
    | UnknownGoal
    | Unreachable

module AStar =

    let private isFinite (value: float) : bool =
        not (Double.IsNaN value) && not (Double.IsInfinity value)

    let private adjacencyOf (problem: AStarProblem) : Map<string, (string * float) list> =
        problem.Edges
        |> List.groupBy (fun edge -> edge.FromNode)
        |> List.map (fun (node, edges) -> node, edges |> List.map (fun edge -> edge.ToNode, edge.Cost))
        |> Map.ofList

    /// A repeated (from,to) pair is a defect rather than a tie to break arbitrarily.
    let private duplicateEdge (problem: AStarProblem) : AStarFault option =
        let pairs = problem.Edges |> List.map (fun edge -> edge.FromNode, edge.ToNode)

        match List.length pairs = (pairs |> Set.ofList |> Set.count) with
        | true -> None
        | false ->
            let duplicate =
                problem.Edges
                |> List.groupBy (fun edge -> edge.FromNode, edge.ToNode)
                |> List.find (fun (_, group) -> List.length group > 1)

            let (fromNode, toNode), _ = duplicate
            Some(AStarFault.DuplicateEdge(fromNode, toNode))

    /// The first edge defect, in a fixed order so the reported fault is stable.
    let private edgeDefect
        (problem: AStarProblem)
        (blank: GraphEdge option)
        (badCost: GraphEdge option)
        (negative: GraphEdge option)
        : AStarFault option =
        match blank, badCost, negative with
        | Some _, _, _ -> Some AStarFault.BlankNode
        | None, Some bad, _ -> Some(AStarFault.NonFiniteCost(bad.FromNode, bad.ToNode))
        | None, None, Some bad -> Some(AStarFault.NegativeCost(bad.FromNode, bad.ToNode))
        | None, None, None -> duplicateEdge problem

    /// The endpoints must be real nodes, and the goal must be reachable by some edge.
    let private blankEndpoint (problem: AStarProblem) : bool =
        String.IsNullOrWhiteSpace problem.Start || String.IsNullOrWhiteSpace problem.Goal

    let private endpointFault (problem: AStarProblem) (adjacency: Map<string, (string * float) list>) : AStarFault option =
        let startKnown = Map.containsKey problem.Start adjacency
        let goalKnown = Map.containsKey problem.Goal adjacency

        match blankEndpoint problem, startKnown, goalKnown with
        | true, _, _ -> Some AStarFault.BlankNode
        | false, false, _ -> Some AStarFault.UnknownStart
        | false, true, false -> Some AStarFault.UnknownGoal
        | false, true, true -> None

    /// Node-level validation, run once the edges are known sound.
    let private nodeDefect
        (problem: AStarProblem)
        (adjacency: Map<string, (string * float) list>)
        : Result<Map<string, (string * float) list>, AStarFault> =
        match endpointFault problem adjacency with
        | Some fault -> Error fault
        | None -> Ok adjacency

    let private validate (problem: AStarProblem) : Result<Map<string, (string * float) list>, AStarFault> =
        let blank = problem.Edges |> List.tryFind (fun edge -> String.IsNullOrWhiteSpace edge.FromNode || String.IsNullOrWhiteSpace edge.ToNode)
        let badCost = problem.Edges |> List.tryFind (fun edge -> not (isFinite edge.Cost))
        let negative = problem.Edges |> List.tryFind (fun edge -> isFinite edge.Cost && edge.Cost < 0.0)

        let edgeDefects = edgeDefect problem blank badCost negative

        match edgeDefects with
        | Some fault -> Error fault
        | None ->
            let adjacency = adjacencyOf problem
            nodeDefect problem adjacency

    /// The first edge defect, in a fixed order so the reported fault is stable.
    let private heuristicOf (problem: AStarProblem) (node: string) : float =
        match problem.Heuristic |> Map.tryFind node with
        | Some value when isFinite value && value >= 0.0 -> value
        | _ -> 0.0

    /// The empty search state. `h = 0` is always a legal conservative start, because
    /// costs are nonnegative; it is slower, never wrong.
    let initialize (problem: AStarProblem) : Result<SearchSnapshot, AStarFault> =
        validate problem
        |> Result.map (fun _ ->
            let startG = 0.0
            let startF = startG + heuristicOf problem problem.Start

            { Open = [ (problem.Start, startF) ]
              Closed = []
              BestG = Map.ofList [ (problem.Start, startG) ]
              Parents = Map.empty
              Expanded = []
              Incumbent = None
              ModelRevision = 0 })

    /// One expansion of the frontier. Returns the snapshot after one node is closed.
    ///
    /// Reopening is the part that makes this correct: when a cheaper g arrives for an
    /// already-CLOSED node, that node goes back on the frontier with the better value.
    /// Returning early with the stale cost is the classic A* bug.
    /// Ordering two frontier entries by f, then by node name for determinism.
    let private frontierOrder (left: string * float) (right: string * float) : int =
        let ordering = compare (snd left) (snd right)

        match ordering with
        | 0 -> compare (fst left) (fst right)
        | _ -> ordering

    let private knownCost (bestG: Map<string, float>) (node: string) : float option =
        bestG |> Map.tryFind node

    let private improved (bestG: Map<string, float>) (node: string) (cost: float) : bool =
        match knownCost bestG node with
        | Some known -> cost < known
        | None -> true

    /// The incumbent after closing `node`. Reaching the goal always produces a cost;
    /// a better goal cost replaces a worse one.
    /// Keeps the better of the known incumbent and the cost just reached.
    let private betterCost (known: float option) (cost: float) : float option =
        match known with
        | Some previous -> Some(min previous cost)
        | None -> Some cost

    let private incumbentOf (snapshot: SearchSnapshot) (node: string) (nodeG: float) (goal: string) : float option =
        match node = goal with
        | false -> snapshot.Incumbent
        | true -> betterCost snapshot.Incumbent nodeG

    /// Whether a candidate may stay on or join the frontier: an entry is kept when its
    /// new g is no worse than what the snapshot recorded.
    let private frontierWorthy (snapshot: SearchSnapshot) (improvedG: Map<string, float>) (candidate: string) : bool =
        let nodeG = improvedG |> Map.tryFind candidate |> Option.defaultValue 0.0

        match snapshot.BestG |> Map.tryFind candidate with
        | Some previous -> nodeG <= previous
        | None -> true

    /// A CLOSED node is reopened when its g strictly improved. This is what separates a
    /// correct A* from a breadth-first search that returns a stale cost.
    let private reopened (snapshot: SearchSnapshot) (improvedG: Map<string, float>) (candidate: string) : bool =
        let nodeG = improvedG |> Map.tryFind candidate |> Option.defaultValue 0.0

        match snapshot.BestG |> Map.tryFind candidate with
        | Some previous -> nodeG < previous
        | None -> false

    /// One relaxation step: a strictly better g replaces the recorded cost and reparents
    /// the candidate to `parent`.
    let private relaxNode
        (bestG: Map<string, float>)
        (parents: Map<string, string>)
        (candidate: string)
        (cost: float)
        (parent: string)
        : Map<string, float> * Map<string, string> =
        match improved bestG candidate cost with
        | true -> bestG |> Map.add candidate cost, parents |> Map.add candidate parent
        | false -> bestG, parents

    let private fValues (problem: AStarProblem) (improvedG: Map<string, float>) : (string * float) list =
        improvedG
        |> Map.toList
        |> List.map (fun (node, g) -> node, g + heuristicOf problem node)

    let step (problem: AStarProblem) (snapshot: SearchSnapshot) : Result<SearchSnapshot, AStarFault> =
        validate problem
        |> Result.map (fun adjacency ->
            let closed = snapshot.Closed |> Set.ofList
            let ordered = snapshot.Open |> List.sortWith frontierOrder

            match ordered with
            | [] -> snapshot
            | (node, _) :: rest ->
                let closedNext = closed |> Set.add node
                let nodeG = snapshot.BestG |> Map.tryFind node |> Option.defaultValue 0.0
                let successors = adjacency |> Map.tryFind node |> Option.defaultValue []

                let relax (bestG: Map<string, float>) (parents: Map<string, string>) (candidate: string) (cost: float) =
                    relaxNode bestG parents candidate cost node

                let improvedG, improvedParents =
                    successors
                    |> List.fold
                        (fun (bestG, parents) (candidate, cost) -> relax bestG parents candidate (nodeG + cost))
                        (snapshot.BestG, snapshot.Parents)

                let scored = fValues problem improvedG

                let keptOpen =
                    rest
                    |> List.filter (fun entry -> frontierWorthy snapshot improvedG (fst entry))

                let freshOpen =
                    scored
                    |> List.filter (fun (candidate, _) -> not (Set.contains candidate closedNext))

                let reopenedOpen =
                    scored
                    |> List.filter (fun (candidate, _) ->
                        Set.contains candidate closedNext && reopened snapshot improvedG candidate)

                let openNext =
                    keptOpen @ freshOpen @ reopenedOpen
                    |> List.distinctBy fst
                    |> List.sortWith frontierOrder

                let newIncumbent = incumbentOf snapshot node nodeG problem.Goal

                { Open = openNext
                  Closed = closedNext |> Set.toList
                  BestG = improvedG
                  Parents = improvedParents
                  Expanded = node :: snapshot.Expanded
                  Incumbent = newIncumbent
                  ModelRevision = snapshot.ModelRevision })

    /// The global lower bound: the incumbent when OPEN is empty, otherwise the smallest
    /// f on the frontier. It is never a single node's g+h.
    let globalBound (snapshot: SearchSnapshot) : float option =
        let frontierMinimum () = snapshot.Open |> List.map snd |> List.min |> Some

        match List.isEmpty snapshot.Open, snapshot.Incumbent with
        | true, Some known -> Some known
        | true, None -> None
        | false, _ -> frontierMinimum ()

    /// True when the search has nothing left to expand and a path was found.
    let isComplete (snapshot: SearchSnapshot) : bool =
        List.isEmpty snapshot.Open && snapshot.Incumbent.IsSome

    /// No solution exists. This is a distinct state, not a very large cost.
    let isUnreachable (snapshot: SearchSnapshot) : bool =
        List.isEmpty snapshot.Open && snapshot.Incumbent.IsNone

    /// Reconstructs the path from the parent map.
    let pathOf (problem: AStarProblem) (snapshot: SearchSnapshot) : string list option =
        let rec walk (node: string) (acc: string list) : string list option =
            match snapshot.Parents |> Map.tryFind node with
            | Some parent -> walk parent (node :: acc)
            | None -> if node = problem.Start then Some(node :: acc) else None

        match snapshot.Incumbent with
        | Some _ -> walk problem.Goal []
        | None -> None
