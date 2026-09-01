namespace Wanxiangshu.Sphinx

module MonteCarlo =
    type Model =
        { Root: string
          Children: Map<string, string list>
          TerminalReward: Map<string, float>
          Prior: Map<string, float> }

    type Result =
        { BestAction: string option
          Nodes: Map<string, MonteCarloNode>
          Iterations: int }

    val uct: parentVisits: int -> exploration: float -> node: MonteCarloNode -> float
    val puct: parentVisits: int -> exploration: float -> node: MonteCarloNode -> float
    val run: iterations: int -> model: Model -> Result
    val syncEpistemicNodes: state: EpistemicState -> EpistemicState
