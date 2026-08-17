namespace Wanxiangshu.Sphinx

open System

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

    let private emptyNode key prior =
        { SemanticKey = key
          Visits = 0
          ValueSum = 0.0
          Prior = prior }

    let private mean node =
        if node.Visits = 0 then
            0.0
        else
            node.ValueSum / float node.Visits

    let uct parentVisits exploration node =
        if node.Visits = 0 then
            Double.PositiveInfinity
        else
            mean node
            + exploration * sqrt (log (float (max 1 parentVisits)) / float node.Visits)

    let puct parentVisits exploration node =
        if node.Visits = 0 then
            Double.PositiveInfinity
        else
            mean node
            + exploration * node.Prior * sqrt (float (max 1 parentVisits))
              / float (1 + node.Visits)

    let private nodeFor model key nodes =
        nodes
        |> Map.tryFind key
        |> Option.defaultWith (fun () -> emptyNode key (model.Prior |> Map.tryFind key |> Option.defaultValue 0.5))

    let private chooseChild model parent nodes =
        let children = model.Children |> Map.tryFind parent |> Option.defaultValue []
        let parentVisits = nodeFor model parent nodes |> fun node -> max 1 node.Visits

        children
        |> List.map (fun key -> key, nodeFor model key nodes)
        |> List.sortBy (fun (key, node) -> -puct parentVisits 1.414 node, key)
        |> List.tryHead
        |> Option.map fst

    let private continueSelection model nodes node path depth unvisited recurse =
        match unvisited with
        | child :: _ -> List.rev (child :: node :: path)
        | [] ->
            chooseChild model node nodes
            |> Option.fold
                (fun _ child -> recurse child (node :: path) (depth + 1))
                (List.rev (node :: path))

    let private select model nodes =
        let rec loop node path depth =
            if depth >= 64 || Map.containsKey node model.TerminalReward then
                List.rev (node :: path)
            else
                let children = model.Children |> Map.tryFind node |> Option.defaultValue []

                let unvisited =
                    children
                    |> List.filter (fun key -> (nodeFor model key nodes).Visits = 0)
                    |> List.sort

                continueSelection model nodes node path depth unvisited loop

        loop model.Root [] 0

    let private continueRollout model node depth recurse =
        match model.Children |> Map.tryFind node |> Option.defaultValue [] with
        | [] -> 0.0
        | children ->
            children
            |> List.sortBy (fun key -> -(model.Prior |> Map.tryFind key |> Option.defaultValue 0.5), key)
            |> List.head
            |> fun child -> recurse child (depth + 1)

    let private rollout model start =
        let rec loop node depth =
            match Map.tryFind node model.TerminalReward with
            | Some reward -> reward
            | None when depth >= 64 -> 0.0
            | None -> continueRollout model node depth loop

        loop start 0

    let private backup model path reward nodes =
        path
        |> List.fold
            (fun current key ->
                let node = nodeFor model key current

                current
                |> Map.add
                    key
                    { node with
                        Visits = node.Visits + 1
                        ValueSum = node.ValueSum + reward })
            nodes

    let run iterations (model: Model) =
        let initial = Map.ofList [ model.Root, emptyNode model.Root 1.0 ]

        let nodes =
            [ 1 .. max 0 iterations ]
            |> List.fold
                (fun current _ ->
                    let path = select model current
                    let leaf = path |> List.last
                    let reward = rollout model leaf
                    backup model path reward current)
                initial

        let best =
            model.Children
            |> Map.tryFind model.Root
            |> Option.defaultValue []
            |> List.map (fun key -> key, nodeFor model key nodes)
            |> List.sortBy (fun (key, node) -> -node.Visits, -mean node, key)
            |> List.tryHead
            |> Option.map fst

        { BestAction = best
          Nodes = nodes
          Iterations = max 0 iterations }

    let syncEpistemicNodes (state: EpistemicState) =
        if state.SolverMode <> SolverMode.MonteCarlo then
            { state with MonteCarlo = Map.empty }
        else
            let nodes =
                state.Actions
                |> Map.map (fun key action ->
                    state.MonteCarlo
                    |> Map.tryFind key
                    |> Option.defaultValue
                        { SemanticKey = action.SemanticKey
                          Visits = 0
                          ValueSum = 0.0
                          Prior = max 0.0 (min 1.0 action.ExpectedRootGain) })

            { state with MonteCarlo = nodes }
