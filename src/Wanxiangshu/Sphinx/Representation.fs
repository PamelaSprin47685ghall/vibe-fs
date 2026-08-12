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

    let dominates (left: CognitiveAction) (right: CognitiveAction) =
        left.ExpectedRootGain >= right.ExpectedRootGain
        && left.GatewayGain >= right.GatewayGain
        && left.Value >= right.Value
        && left.Cost <= right.Cost
        && provenanceStrength left >= provenanceStrength right
        && (left.ExpectedRootGain > right.ExpectedRootGain
            || left.GatewayGain > right.GatewayGain
            || left.Value > right.Value
            || left.Cost < right.Cost
            || provenanceStrength left > provenanceStrength right)

    let paretoFrontier actions =
        actions
        |> List.filter (fun candidate ->
            actions
            |> List.exists (fun other -> other.Id <> candidate.Id && dominates other candidate)
            |> not)
        |> List.sortBy (fun action -> -action.Value, action.Cost, -provenanceStrength action, action.Id)

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
