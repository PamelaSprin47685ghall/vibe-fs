namespace Wanxiangshu.Sphinx.V2.Plugins

/// The JS-native conformance surface for the MCTS refiner.
module Surface =

    let listOfItems (items: string list) : string list = items |> List.ofSeq

    let nodeStats
        (visits: int)
        (valueSum: float)
        (valueSumSquares: float)
        (modelRef: string)
        (horizon: int)
        : NodeStats =
        { Visits = visits
          ValueSum = valueSum
          ValueSumSquares = valueSumSquares
          ModelRef = modelRef
          Horizon = horizon }

    let uctOf
        (parentVisits: int)
        (exploration: float)
        (rewardLow: float)
        (rewardHigh: float)
        (node: NodeStats)
        : float =
        Mcts.uct parentVisits exploration rewardLow rewardHigh node

    let meanOf (node: NodeStats) : float option = Mcts.mean node

    let varianceOf (node: NodeStats) : float option = Mcts.variance node

    let stateKeyOf (modelRef: string) (history: string list) (horizon: int) : string =
        Mcts.stateKey modelRef (history |> List.ofSeq) horizon

    let allChildrenSeen (stats: Map<string, NodeStats>) (actions: string list) : bool =
        Mcts.allChildrenSeen stats (actions |> List.ofSeq)
