namespace Wanxiangshu.Sphinx.V2.Plugins

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
    val initialize: AStarProblem -> Result<SearchSnapshot, AStarFault>

    /// One expansion. A cheaper g for an already-CLOSED node reopens it.
    val step: AStarProblem -> SearchSnapshot -> Result<SearchSnapshot, AStarFault>

    /// The global lower bound: incumbent when OPEN is empty, else the smallest frontier f.
    val globalBound: SearchSnapshot -> float option
    val isComplete: SearchSnapshot -> bool
    val isUnreachable: SearchSnapshot -> bool
    val pathOf: AStarProblem -> SearchSnapshot -> string list option
