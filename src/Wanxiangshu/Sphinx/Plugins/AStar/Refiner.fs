// WHAT[EPI-010]: A* refiner expands minimum g+h, reopens on improved g, and bounds the optimum globally.

namespace Wanxiangshu.Sphinx.Plugins.AStar

open System
open FsToolkit.ErrorHandling
open Wanxiangshu.Sphinx.Core

module Refiner =

    type Edge =
        { FromNode: string
          ToNode: string
          Cost: float }

    type Problem =
        { Start: string
          Goal: string
          Edges: Edge list
          Heuristic: Map<string, float> }

    // H-2: optimality is conditional on an admissible (never-overestimating) heuristic,
    // which validate checks only for finiteness/non-negativity. Every proof therefore
    // records the assumption explicitly; consumers must not read Optimal as proven.
    type PathProof =
        { Path: string list
          Cost: float
          Expanded: string list
          Expansions: int
          LowerBound: float
          UpperBound: float
          Assumptions: Set<string> }

    type Unreachable =
        { Expanded: string list
          Expansions: int
          LowerBound: float
          UpperBound: float }

    [<RequireQualifiedAccess>]
    type Outcome =
        | Optimal of PathProof
        | Unreachable of Unreachable

    [<RequireQualifiedAccess>]
    type GraphFault =
        | NegativeCost of fromNode: string * toNode: string
        | NonFiniteCost of fromNode: string * toNode: string
        | NegativeHeuristic of node: string
        | NonFiniteHeuristic of node: string

    let code (fault: GraphFault) : string =
        match fault with
        | GraphFault.NegativeCost _ -> "negative-cost"
        | GraphFault.NonFiniteCost _ -> "non-finite-cost"
        | GraphFault.NegativeHeuristic _ -> "negative-heuristic"
        | GraphFault.NonFiniteHeuristic _ -> "non-finite-heuristic"

    let message (fault: GraphFault) : string =
        match fault with
        | GraphFault.NegativeCost(fromNode, toNode) ->
            sprintf "edge %s -> %s carries a negative cost; costs must be nonnegative" fromNode toNode
        | GraphFault.NonFiniteCost(fromNode, toNode) ->
            sprintf "edge %s -> %s carries a non-finite cost; costs must be finite" fromNode toNode
        | GraphFault.NegativeHeuristic node ->
            sprintf "heuristic for node %s is negative; estimates must be nonnegative" node
        | GraphFault.NonFiniteHeuristic node ->
            sprintf "heuristic for node %s is non-finite; estimates must be finite" node

    let toCoreError (fault: GraphFault) : CoreError =
        { Code = code fault
          Message = message fault }

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private checkNonFiniteCost (problem: Problem) : Result<unit, GraphFault> =
        match problem.Edges |> List.tryFind (fun edge -> not (isFiniteNumber edge.Cost)) with
        | Some edge -> Error(GraphFault.NonFiniteCost(edge.FromNode, edge.ToNode))
        | None -> Ok()

    let private checkNegativeCost (problem: Problem) : Result<unit, GraphFault> =
        match problem.Edges |> List.tryFind (fun edge -> edge.Cost < 0.0) with
        | Some edge -> Error(GraphFault.NegativeCost(edge.FromNode, edge.ToNode))
        | None -> Ok()

    let private checkNonFiniteHeuristic (problem: Problem) : Result<unit, GraphFault> =
        match
            problem.Heuristic
            |> Map.toList
            |> List.tryFind (fun (_, estimate) -> not (isFiniteNumber estimate))
        with
        | Some(node, _) -> Error(GraphFault.NonFiniteHeuristic node)
        | None -> Ok()

    let private checkNegativeHeuristic (problem: Problem) : Result<unit, GraphFault> =
        match
            problem.Heuristic
            |> Map.toList
            |> List.tryFind (fun (_, estimate) -> estimate < 0.0)
        with
        | Some(node, _) -> Error(GraphFault.NegativeHeuristic node)
        | None -> Ok()

    let private validate (problem: Problem) : Result<unit, GraphFault> =
        result {
            do! checkNonFiniteCost problem
            do! checkNegativeCost problem
            do! checkNonFiniteHeuristic problem
            do! checkNegativeHeuristic problem
        }

    type private FrontierEntry = { Node: string; G: float; F: float }

    let private estimateOf (problem: Problem) (node: string) : float =
        problem.Heuristic |> Map.tryFind node |> Option.defaultValue 0.0

    let private adjacencyOf (problem: Problem) : Map<string, Edge list> =
        problem.Edges |> List.groupBy (fun edge -> edge.FromNode) |> Map.ofList

    let rec private rebuild (parents: Map<string, string>) (node: string) (path: string list) : string list =
        match Map.tryFind node parents with
        | None -> node :: path
        | Some parent -> rebuild parents parent (node :: path)

    let private isStaleEntry (bestG: Map<string, float>) (closed: Set<string>) (entry: FrontierEntry) : bool =
        let recorded =
            bestG |> Map.tryFind entry.Node |> Option.defaultValue Double.PositiveInfinity

        entry.G > recorded || Set.contains entry.Node closed

    let private relaxEdge
        (estimate: string -> float)
        (entryG: float)
        (entryNode: string)
        (queue: FrontierEntry list, costs: Map<string, float>, lineage: Map<string, string>, sealedNodes: Set<string>)
        (edge: Edge)
        : FrontierEntry list * Map<string, float> * Map<string, string> * Set<string> =
        let tentative = entryG + edge.Cost

        let previous =
            costs |> Map.tryFind edge.ToNode |> Option.defaultValue Double.PositiveInfinity

        if tentative < previous then
            (({ Node = edge.ToNode
                G = tentative
                F = tentative + estimate edge.ToNode }
             : FrontierEntry)
             :: queue,
             Map.add edge.ToNode tentative costs,
             Map.add edge.ToNode entryNode lineage,
             Set.remove edge.ToNode sealedNodes)
        else
            queue, costs, lineage, sealedNodes

    let private goalProof (parents: Map<string, string>) (entry: FrontierEntry) (expandedRev: string list) : PathProof =
        { Path = rebuild parents entry.Node []
          Cost = entry.G
          Expanded = List.rev (entry.Node :: expandedRev)
          Expansions = List.length expandedRev + 1
          LowerBound = entry.F
          UpperBound = entry.G
          Assumptions =
            Set.ofList
                [ "admissible-heuristic-assumed-unverified"
                  "global-bound-at-goal-pop"
                  "open-closed-parent-witnessed" ] }

    let rec private searchLoop
        (problem: Problem)
        (estimate: string -> float)
        (adjacency: Map<string, Edge list>)
        (openSet: FrontierEntry list)
        (bestG: Map<string, float>)
        (parents: Map<string, string>)
        (closed: Set<string>)
        (expandedRev: string list)
        : Outcome =
        match openSet |> List.sortBy (fun entry -> entry.F, entry.G, entry.Node) with
        | [] ->
            Outcome.Unreachable
                { Expanded = List.rev expandedRev
                  Expansions = List.length expandedRev
                  LowerBound = Double.PositiveInfinity
                  UpperBound = Double.PositiveInfinity }
        | entry :: rest when isStaleEntry bestG closed entry ->
            searchLoop problem estimate adjacency rest bestG parents closed expandedRev
        | entry :: rest when entry.Node = problem.Goal -> Outcome.Optimal(goalProof parents entry expandedRev)
        | entry :: rest ->
            let closedNow = Set.add entry.Node closed

            let nextOpen, nextBest, nextParents, nextClosed =
                adjacency
                |> Map.tryFind entry.Node
                |> Option.defaultValue []
                |> List.fold (relaxEdge estimate entry.G entry.Node) (rest, bestG, parents, closedNow)

            searchLoop problem estimate adjacency nextOpen nextBest nextParents nextClosed (entry.Node :: expandedRev)

    let solve (problem: Problem) : Result<Outcome, GraphFault> =
        result {
            do! validate problem
            let estimate = estimateOf problem
            let adjacency = adjacencyOf problem

            return
                searchLoop
                    problem
                    estimate
                    adjacency
                    [ ({ Node = problem.Start
                         G = 0.0
                         F = estimate problem.Start }
                      : FrontierEntry) ]
                    (Map.ofList [ problem.Start, 0.0 ])
                    Map.empty
                    Set.empty
                    []
        }
