namespace Wanxiangshu.Sphinx

module Closure =

    let private ensureSynthesisAction (state: EpistemicState) =
        if state.Synthesis.IsSome || not (Methodology.synthesisAvailable state) then
            state
        elif Map.containsKey "synthesis" state.Actions then
            let existing = state.Actions["synthesis"]

            { state with
                Actions =
                    state.Actions
                    |> Map.add
                        "synthesis"
                        { existing with
                            Status = ActionStatus.Open } }
        else
            let action =
                { Id = "synthesis"
                  Kind = ActionKind.Synthesize
                  Method = "Synthesis"
                  Question = "Compose the current findings into the root answer contract."
                  SemanticKey = "synthesis:root"
                  EquivalenceKey = Some "synthesis:root"
                  DependencyKey = None
                  ExpectedRootGain = min 0.9 (0.18 * float state.Findings.Count)
                  GatewayGain = 0.0
                  Cost = 0.35
                  Value = 0.0
                  Status = ActionStatus.Open
                  Provenance = [ "kernel:method:Synthesis" ] }

            { state with
                Actions = state.Actions |> Map.add action.Id action }

    let private synchronize (state: EpistemicState) =
        let withSynthesis = ensureSynthesisAction state

        let withBayes =
            { withSynthesis with
                Bayesian = Bayes.update withSynthesis }

        let valued = Value.revalueActions withBayes
        let represented = Representation.optimize valued |> Value.revalueActions
        let searched = Search.syncEpistemicFrontier represented
        MonteCarlo.syncEpistemicNodes searched

    let close (state: EpistemicState) =
        let rec fixedPoint remaining current =
            if remaining <= 0 then
                current
            else
                fixedPointStep remaining current (synchronize current)

        and fixedPointStep remaining current next =
            if next = current then
                current
            else
                fixedPoint (remaining - 1) next

        fixedPoint 16 state

    let absorbAndClose state observation = Absorb.apply state observation |> close
