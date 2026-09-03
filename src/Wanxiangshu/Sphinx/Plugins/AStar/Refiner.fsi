namespace Wanxiangshu.Sphinx.Plugins.AStar

open Wanxiangshu.Sphinx.Core

/// WHAT[EPI-010]: optimal path search over a finite deterministic graph with global bounds.
module Refiner =
    /// One directed edge with a nonnegative finite traversal cost.
    type Edge =
        { FromNode: string
          ToNode: string
          Cost: float }

    /// Finite graph search problem with a nonnegative heuristic estimate.
    type Problem =
        { Start: string
          Goal: string
          Edges: Edge list
          Heuristic: Map<string, float> }

    /// Optimal path witness with global frontier lower bound and incumbent upper bound.
    /// Optimality is conditional: Assumptions always carries
    /// admissible-heuristic-assumed-unverified (H-2).
    type PathProof =
        { Path: string list
          Cost: float
          Expanded: string list
          Expansions: int
          LowerBound: float
          UpperBound: float
          Assumptions: Set<string> }

    /// Exhaustion witness when no path reaches the goal.
    type Unreachable =
        { Expanded: string list
          Expansions: int
          LowerBound: float
          UpperBound: float }

    /// Search outcome: either an optimal path proof or an exhaustion witness.
    [<RequireQualifiedAccess>]
    type Outcome =
        | Optimal of PathProof
        | Unreachable of Unreachable

    /// Typed reason the graph or heuristic violates the search contract.
    [<RequireQualifiedAccess>]
    type GraphFault =
        | NegativeCost of fromNode: string * toNode: string
        | NonFiniteCost of fromNode: string * toNode: string
        | NegativeHeuristic of node: string
        | NonFiniteHeuristic of node: string

    val code: fault: GraphFault -> string
    val message: fault: GraphFault -> string
    val toCoreError: fault: GraphFault -> CoreError
    val solve: problem: Problem -> Result<Outcome, GraphFault>
