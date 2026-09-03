// WHAT[EPI-010]: A* refiner expands minimum g+h, reopens on improved g, and bounds the optimum globally.

namespace Wanxiangshu.Sphinx.Plugins.AStar

open System
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
        | GraphFault.NegativeCost (fromNode, toNode) ->
            sprintf "edge %s -> %s carries a negative cost; costs must be nonnegative" fromNode toNode
        | GraphFault.NonFiniteCost (fromNode, toNode) ->
            sprintf "edge %s -> %s carries a non-finite cost; costs must be finite" fromNode toNode
        | GraphFault.NegativeHeuristic node -> sprintf "heuristic for node %s is negative; estimates must be nonnegative" node
        | GraphFault.NonFiniteHeuristic node ->
            sprintf "heuristic for node %s is non-finite; estimates must be finite" node

    let toCoreError (fault: GraphFault) : CoreError =
        { Code = code fault
          Message = message fault }

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private validate (problem: Problem) : Result<unit, GraphFault> =
        match problem.Edges |> List.tryFind (fun edge -> not (isFiniteNumber edge.Cost)) with
        | Some edge -> Error(GraphFault.NonFiniteCost(edge.FromNode, edge.ToNode))
        | None ->
            match problem.Edges |> List.tryFind (fun edge -> edge.Cost < 0.0) with
            | Some edge -> Error(GraphFault.NegativeCost(edge.FromNode, edge.ToNode))
            | None ->
                let ordered = problem.Heuristic |> Map.toList

                match ordered |> List.tryFind (fun (_, estimate) -> not (isFiniteNumber estimate)) with
                | Some (node, _) -> Error(GraphFault.NonFiniteHeuristic node)
                | None ->
                    match ordered |> List.tryFind (fun (_, estimate) -> estimate < 0.0) with
                    | Some (node, _) -> Error(GraphFault.NegativeHeuristic node)
                    | None -> Ok()

    type private FrontierEntry = { Node: string; G: float; F: float }

    let solve (problem: Problem) : Result<Outcome, GraphFault> =
        match validate problem with
        | Error fault -> Error fault
        | Ok () ->
            let estimate node =
                problem.Heuristic |> Map.tryFind node |> Option.defaultValue 0.0

            let adjacency = problem.Edges |> List.groupBy (fun edge -> edge.FromNode) |> Map.ofList

            let neighbors node =
                adjacency |> Map.tryFind node |> Option.defaultValue []

            let rec rebuild parents node path =
                match Map.tryFind node parents with
                | None -> node :: path
                | Some parent -> rebuild parents parent (node :: path)

            let rec loop openSet bestG parents closed expandedRev =
                match openSet |> List.sortBy (fun entry -> entry.F, entry.G, entry.Node) with
                | [] ->
                    Outcome.Unreachable
                        { Expanded = List.rev expandedRev
                          Expansions = List.length expandedRev
                          LowerBound = Double.PositiveInfinity
                          UpperBound = Double.PositiveInfinity }
                | entry :: rest ->
                    let recorded =
                        bestG |> Map.tryFind entry.Node |> Option.defaultValue Double.PositiveInfinity

                    if entry.G > recorded || Set.contains entry.Node closed then
                        loop rest bestG parents closed expandedRev
                    elif entry.Node = problem.Goal then
                        Outcome.Optimal
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
                    else
                        let closedNow = Set.add entry.Node closed

                        let nextOpen, nextBest, nextParents, nextClosed =
                            neighbors entry.Node
                            |> List.fold
                                (fun (queue, costs, lineage, sealedNodes) edge ->
                                    let tentative = entry.G + edge.Cost

                                    let previous =
                                        costs |> Map.tryFind edge.ToNode |> Option.defaultValue Double.PositiveInfinity

                                    if tentative < previous then
                                        ({ Node = edge.ToNode
                                           G = tentative
                                           F = tentative + estimate edge.ToNode } :: queue,
                                         Map.add edge.ToNode tentative costs,
                                         Map.add edge.ToNode entry.Node lineage,
                                         Set.remove edge.ToNode sealedNodes)
                                    else
                                        queue, costs, lineage, sealedNodes)
                                (rest, bestG, parents, closedNow)

                        loop nextOpen nextBest nextParents nextClosed (entry.Node :: expandedRev)

            Ok(
                loop
                    [ { Node = problem.Start
                        G = 0.0
                        F = estimate problem.Start } ]
                    (Map.ofList [ problem.Start, 0.0 ])
                    Map.empty
                    Set.empty
                    []
            )
