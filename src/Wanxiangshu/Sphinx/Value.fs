namespace Wanxiangshu.Sphinx

module Value =

    let private groundedFindingCount (state: EpistemicState) =
        state.Findings
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.filter (fun finding ->
            finding.EvidenceKeys
            |> List.exists (fun key -> Map.containsKey key state.Evidence))
        |> Seq.length

    let currentAnswerLoss (state: EpistemicState) =
        match state.RootContract with
        | None -> 1.0
        | Some root ->
            let grounded = float (groundedFindingCount state)
            let structural = 1.0 / (1.0 + grounded)
            let synthesisFactor = if state.Synthesis.IsSome then 0.72 else 1.0

            let judgmentWeight =
                root.ContractBelief
                |> Map.tryFind AnswerContract.Judgment
                |> Option.defaultValue 0.0

            let credenceWeight =
                root.ContractBelief
                |> Map.tryFind AnswerContract.Credence
                |> Option.defaultValue 0.0

            let probabilisticWeight = min 1.0 (judgmentWeight + credenceWeight)

            let probabilisticLoss =
                match state.Bayesian with
                | Some belief -> belief.BayesRisk
                | None -> 0.65

            (1.0 - probabilisticWeight) * structural * synthesisFactor
            + probabilisticWeight * probabilisticLoss
            |> max 0.0
            |> min 1.0

    let stopUtility state = -currentAnswerLoss state

    let private dependencyDiscount (state: EpistemicState) (action: CognitiveAction) =
        match action.DependencyKey with
        | None -> 1.0
        | Some dependency ->
            match Map.tryFind dependency state.Dependencies with
            | None -> 1.0
            | Some facts when Set.isEmpty facts -> 1.0
            | Some _ -> 0.35

    let actionDelta (state: EpistemicState) (action: CognitiveAction) =
        if action.Status = ActionStatus.Resolved then
            System.Double.NegativeInfinity
        else
            match action.Kind with
            | ActionKind.Synthesize ->
                if state.Synthesis.IsSome || Map.isEmpty state.Findings then
                    System.Double.NegativeInfinity
                else
                    let coverage = min 1.0 (0.22 * float state.Findings.Count)
                    0.28 + coverage - max 0.0 action.Cost
            | ActionKind.Investigate ->
                let rootGain = max 0.0 action.ExpectedRootGain
                let gateway = max 0.0 action.GatewayGain

                dependencyDiscount state action * (rootGain + 0.65 * gateway)
                - max 0.0 action.Cost

    let actionUtility state action =
        stopUtility state + actionDelta state action

    let revalueActions state =
        { state with
            Actions =
                state.Actions
                |> Map.map (fun _ action ->
                    { action with
                        Value = actionDelta state action }) }

    let bestOpenAction state =
        state.Actions
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.filter (fun action -> action.Status = ActionStatus.Open)
        |> Seq.sortBy (fun action -> -actionUtility state action, action.Cost, action.Id)
        |> Seq.tryHead

    let stopDominates state =
        match bestOpenAction state with
        | None -> true
        | Some action -> stopUtility state >= actionUtility state action
