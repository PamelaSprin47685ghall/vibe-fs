namespace Wanxiangshu.Sphinx

module Policy =

    let private observationMatches (request: Request) (observation: Observation) =
        match request, observation with
        | SemanticAssessmentRequest _, SemanticAssessmentObservation _ -> Ok()
        | GenerateCandidatesRequest _, CandidatesObservation _ -> Ok()
        | InvestigateRequest action, InvestigationObservation result when result.ActionKey = action.Id -> Ok()
        | InvestigateRequest action, InvestigationObservation result ->
            Error($"investigation action mismatch: expected {action.Id}, got {result.ActionKey}")
        | SynthesizeRequest _, SynthesisObservation _ -> Ok()
        | SemanticAssessmentRequest _, _ -> Error "expected SemanticAssessment observation"
        | GenerateCandidatesRequest _, _ -> Error "expected Candidates observation"
        | InvestigateRequest _, _ -> Error "expected Investigation observation"
        | SynthesizeRequest _, _ -> Error "expected Synthesis observation"

    let private probabilisticContractWeight (root: RootContract) =
        (root.ContractBelief
         |> Map.tryFind AnswerContract.Judgment
         |> Option.defaultValue 0.0)
        + (root.ContractBelief
           |> Map.tryFind AnswerContract.Credence
           |> Option.defaultValue 0.0)
        |> min 1.0

    let private ungroundedFindings (state: EpistemicState) =
        state.Findings
        |> Map.toList
        |> List.choose (fun (key, finding) ->
            if
                finding.EvidenceKeys
                |> List.exists (fun evidenceKey -> State.hasEvidenceSemanticKey evidenceKey state)
            then
                None
            else
                Some("ungrounded-finding:" + key))

    let private probabilisticUncertainty (root: RootContract) (state: EpistemicState) =
        if probabilisticContractWeight root > 0.2 && state.Bayesian.IsNone then
            [ "numeric-credence-unqualified" ]
        else
            []

    let canonicalAnswer reason (state: EpistemicState) =
        match state.RootContract with
        | None ->
            { Question = state.RootQuestion
              Contract =
                { FormBelief = Map.empty
                  ContractBelief = Map.empty
                  Facets = Map.empty
                  Targets = []
                  Intents = [] }
              Findings = []
              Evidence = []
              Hypotheses = []
              Synthesis = None
              Bayesian = None
              Uncertainties = [ "root-contract-unresolved" ]
              StopReason = reason
              Revision = state.Revision }
        | Some root ->
            let synthesisUncertainty =
                state.Synthesis
                |> Option.map (fun synthesis -> synthesis.Uncertainties)
                |> Option.defaultValue []

            { Question = state.RootQuestion
              Contract = root
              Findings = state.Findings |> Map.toList |> List.map snd
              Evidence = state.Evidence |> Map.toList |> List.map snd
              Hypotheses = state.Hypotheses |> Map.toList |> List.map snd
              Synthesis = state.Synthesis
              Bayesian = state.Bayesian
              Uncertainties =
                List.distinct (
                    ungroundedFindings state
                    @ probabilisticUncertainty root state
                    @ synthesisUncertainty
                )
              StopReason = reason
              Revision = state.Revision }

    let private markSelected (action: CognitiveAction) (state: EpistemicState) =
        { state with
            Actions =
                state.Actions
                |> Map.add
                    action.Id
                    { action with
                        Status = ActionStatus.Selected } }

    let private yieldRequest (request: Request) (state: EpistemicState) =
        let next = State.withYield request state
        next, InquiryResult.Yield request

    let private exhaustedReason (state: EpistemicState) =
        if Map.isEmpty state.Findings && Map.isEmpty state.Evidence then
            "policy-exhausted"
        elif state.Synthesis.IsSome then
            "stop-dominates"
        else
            "marginal-value-exhausted"

    let private synthesizeSelected (selected: EpistemicState) =
        match selected.RootContract with
        | Some root ->
            let keys = selected.Findings |> Map.toList |> List.map fst
            yieldRequest (SynthesizeRequest(keys, root)) selected
        | None -> selected, InquiryResult.Error "root contract missing"

    let private selectAndYield (action: CognitiveAction) (state: EpistemicState) =
        let selected = markSelected action state

        match action.Kind with
        | ActionKind.Investigate -> yieldRequest (InvestigateRequest action) selected
        | ActionKind.Synthesize -> synthesizeSelected selected

    let private decideOpenAction (state: EpistemicState) =
        match Value.bestOpenAction state with
        | Some action when not (Value.stopDominates state) -> selectAndYield action state
        | _ -> state, InquiryResult.Answered(canonicalAnswer (exhaustedReason state) state)

    let private decideWithRoot (root: RootContract) (state: EpistemicState) =
        if state.NeedsGeneration then
            yieldRequest (GenerateCandidatesRequest(Methodology.generationMethods state, root)) state
        else
            decideOpenAction state

    let private decideWithinBudget (state: EpistemicState) =
        match state.RootContract with
        | None -> yieldRequest (SemanticAssessmentRequest state.RootQuestion) state
        | Some root -> decideWithRoot root state

    let decide (state: EpistemicState) =
        if not (State.withinBudget state) then
            state, InquiryResult.Answered(canonicalAnswer "budget" state)
        else
            decideWithinBudget state

    let start (question: string) =
        let text = if isNull question then "" else question.Trim()

        if text = "" then
            State.create "", InquiryResult.Error "question required"
        else
            State.create text |> decide

    let private resumeMatched (state: EpistemicState) (observation: Observation) =
        Closure.absorbAndClose state observation |> decide

    let private resumeWithRequest (state: EpistemicState) (request: Request) (observation: Observation) =
        match observationMatches request observation with
        | Error error -> state, InquiryResult.Error error
        | Ok() -> resumeMatched state observation

    let resume (state: EpistemicState) (observation: Observation) =
        match state.PendingRequest with
        | None -> state, InquiryResult.Error "no pending kernel request"
        | Some request -> resumeWithRequest state request observation
