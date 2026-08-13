namespace Wanxiangshu.Sphinx

module Representation =

    let classKey (action: CognitiveAction) =
        match action.EquivalenceKey with
        | Some key when key <> "" -> "eq:" + key
        | _ ->
            let dependency = action.DependencyKey |> Option.defaultValue "independent"
            "semantic:" + action.SemanticKey + "|dependency:" + dependency

    let private provenanceStrength action =
        action.Provenance |> List.distinct |> List.length

    let private dominatesWith
        (left: CognitiveAction)
        leftStrength
        (right: CognitiveAction)
        rightStrength
        =
        left.ExpectedRootGain >= right.ExpectedRootGain
        && left.GatewayGain >= right.GatewayGain
        && left.Value >= right.Value
        && left.Cost <= right.Cost
        && leftStrength >= rightStrength
        && (left.ExpectedRootGain > right.ExpectedRootGain
            || left.GatewayGain > right.GatewayGain
            || left.Value > right.Value
            || left.Cost < right.Cost
            || leftStrength > rightStrength)

    let dominates (left: CognitiveAction) (right: CognitiveAction) =
        dominatesWith left (provenanceStrength left) right (provenanceStrength right)

    let paretoFrontier actions =
        let scored =
            actions
            |> List.map (fun action -> action, provenanceStrength action)

        let ordered =
            scored
            |> List.sortBy (fun (action, strength) ->
                -action.ExpectedRootGain,
                -action.GatewayGain,
                -action.Value,
                action.Cost,
                -strength,
                action.Id)

        ordered
        |> List.fold
            (fun frontier ((candidate, candidateStrength) as item) ->
                let dominated =
                    frontier
                    |> List.exists (fun (other, otherStrength) ->
                        other.Id <> candidate.Id
                        && dominatesWith other otherStrength candidate candidateStrength)

                if dominated then
                    frontier
                else
                    item :: frontier)
            []
        |> List.sortBy (fun (action, strength) -> -action.Value, action.Cost, -strength, action.Id)
        |> List.map fst

    let representative actions =
        paretoFrontier actions
        |> List.sortBy (fun action -> action.Cost - action.Value, -provenanceStrength action, action.Id)
        |> List.tryHead

    let optimize (state: EpistemicState) =
        let classes = state.Actions |> Map.toList |> List.map snd |> List.groupBy classKey

        let classMap =
            classes
            |> List.map (fun (key, actions) -> key, (actions |> List.map (fun action -> action.Id) |> List.sort))
            |> Map.ofList

        let frontiers =
            classes
            |> List.map (fun (key, actions) -> key, (paretoFrontier actions |> List.map (fun action -> action.Id)))
            |> Map.ofList

        let representatives =
            classes
            |> List.choose (fun (key, actions) -> representative actions |> Option.map (fun action -> key, action.Id))
            |> Map.ofList

        let survivorIds = frontiers |> Map.toSeq |> Seq.collect snd |> Set.ofSeq

        let actions =
            state.Actions |> Map.filter (fun key _ -> Set.contains key survivorIds)

        let futureCost =
            actions
            |> Map.toSeq
            |> Seq.map snd
            |> Seq.filter (fun action -> action.Status <> ActionStatus.Resolved)
            |> Seq.sumBy (fun action -> max 0.0 action.Cost)

        { state with
            Actions = actions
            Representation =
                { EquivalenceClasses = classMap
                  ParetoFrontiers = frontiers
                  Representatives = representatives
                  EstimatedFutureCost = futureCost } }
