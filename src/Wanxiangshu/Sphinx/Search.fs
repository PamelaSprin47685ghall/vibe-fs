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

    let private reconstruct (goal: string) (parents: Map<string, string>) : string list =
        let rec loop node path =
            match Map.tryFind node parents with
            | None -> node :: path
            | Some parent -> loop parent (node :: path)

        loop goal []

    let private updateNeighbor
        (node: string)
        (g: float)
        (heuristic: string -> float)
        ((queue, costs, lineage, sealedNodes):
            (string * float * float) list * Map<string, float> * Map<string, string> * Set<string>)
        (edge: GraphEdge)
        : (string * float * float) list * Map<string, float> * Map<string, string> * Set<string> =
        let tentative = g + edge.Cost

        let previous =
            costs
            |> Map.tryFind edge.ToNode
            |> Option.defaultValue System.Double.PositiveInfinity

        if tentative < previous then
            let next = edge.ToNode
            let priority = tentative + heuristic next

            ((next, tentative, priority) :: queue,
             costs |> Map.add next tentative,
             lineage |> Map.add next node,
             Set.remove next sealedNodes)
        else
            queue, costs, lineage, sealedNodes

    let private solveValidGraph (problem: AStarProblem) : AStarResult option =
        let neighborsOf =
            problem.Edges |> List.groupBy (fun edge -> edge.FromNode) |> Map.ofList

        let neighbors (node: string) : GraphEdge list =
            neighborsOf |> Map.tryFind node |> Option.defaultValue []

        let heuristic (node: string) : float =
            problem.Heuristic |> Map.tryFind node |> Option.defaultValue 0.0

        let rec search
            (openSet: (string * float * float) list)
            (bestG: Map<string, float>)
            (parents: Map<string, string>)
            (closed: Set<string>)
            (expanded: string list)
            : AStarResult option =
            match openSet |> List.sortBy (fun (node, g, f) -> f, g, node) with
            | [] -> None
            | (node, g, _) :: rest when Set.contains node closed -> search rest bestG parents closed expanded
            | (node, g, _) :: rest when node = problem.Goal ->
                Some
                    { Path = reconstruct node parents
                      Cost = g
                      Expanded = List.rev (node :: expanded) }
            | (node, g, _) :: rest ->
                let closed' = Set.add node closed

                let open', bestG', parents', closed'' =
                    neighbors node
                    |> List.fold
                        (fun accumulated edge -> updateNeighbor node g heuristic accumulated edge)
                        (rest, bestG, parents, closed')

                search open' bestG' parents' closed'' (node :: expanded)

        search
            [ problem.Start, 0.0, heuristic problem.Start ]
            (Map.ofList [ problem.Start, 0.0 ])
            Map.empty
            Set.empty
            []

    let solveGraph (problem: AStarProblem) : AStarResult option =
        if problem.Edges |> List.exists (fun edge -> edge.Cost < 0.0) then
            None
        else
            solveValidGraph problem

    let epistemicPriority (action: CognitiveAction) =
        let cost = max 0.000001 action.Cost
        action.Value / cost

    let syncEpistemicFrontier (state: EpistemicState) =
        if state.SolverMode <> SolverMode.BestFirst then
            { state with Search = Map.empty }
        else
            let nodes =
                state.Actions
                |> Map.map (fun _ action ->
                    { SemanticKey = action.SemanticKey
                      PathCost = action.Cost
                      HeuristicCost = max 0.0 (1.0 - action.ExpectedRootGain - action.GatewayGain)
                      Priority = epistemicPriority action
                      Closed = action.Status = ActionStatus.Resolved })

            { state with Search = nodes }
