namespace Wanxiangshu.Sphinx

module Search =
    type GraphEdge =
        { FromNode: string
          ToNode: string
          Cost: float }

    type AStarProblem =
        { Start: string
          Goal: string
          Edges: GraphEdge list
          Heuristic: Map<string, float> }

    type AStarResult =
        { Path: string list
          Cost: float
          Expanded: string list }

    val solveGraph: problem: AStarProblem -> AStarResult option
    val epistemicPriority: action: CognitiveAction -> float
    val syncEpistemicFrontier: state: EpistemicState -> EpistemicState
