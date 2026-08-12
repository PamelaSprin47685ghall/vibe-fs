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
            let ungrounded =
                state.Findings
                |> Map.toList
                |> List.choose (fun (key, finding) ->
                    if
                        finding.EvidenceKeys
                        |> List.exists (fun evidenceKey -> Map.containsKey evidenceKey state.Evidence)
                    then
                        None
                    else
                        Some("ungrounded-finding:" + key))

            let probabilistic =
                if probabilisticContractWeight root > 0.2 && state.Bayesian.IsNone then
                    [ "numeric-credence-unqualified" ]
                else
                    []

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
              Uncertainties = List.distinct (ungrounded @ probabilistic @ synthesisUncertainty)
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

    let decide (state: EpistemicState) =
        if not (State.withinBudget state) then
            state, InquiryResult.Answered(canonicalAnswer "budget" state)
        else
            match state.RootContract with
            | None -> yieldRequest (SemanticAssessmentRequest state.RootQuestion) state
            | Some root when state.GenerationRounds = 0 ->
                yieldRequest (GenerateCandidatesRequest(Methodology.generationMethods state, root)) state
            | Some _ ->
                match Value.bestOpenAction state with
                | Some action when not (Value.stopDominates state) ->
                    let selected = markSelected action state

                    match action.Kind with
                    | ActionKind.Investigate -> yieldRequest (InvestigateRequest action) selected
                    | ActionKind.Synthesize ->
                        match state.RootContract with
                        | Some root ->
                            let keys = state.Findings |> Map.toList |> List.map fst
                            yieldRequest (SynthesizeRequest(keys, root)) selected
                        | None -> selected, InquiryResult.Error "root contract missing"
                | _ ->
                    let reason =
                        if Map.isEmpty state.Findings && Map.isEmpty state.Evidence then
                            "policy-exhausted"
                        elif state.Synthesis.IsSome then
                            "stop-dominates"
                        else
                            "marginal-value-exhausted"

                    state, InquiryResult.Answered(canonicalAnswer reason state)

    let start (question: string) =
        let text = if isNull question then "" else question.Trim()

        if text = "" then
            State.create "", InquiryResult.Error "question required"
        else
            State.create text |> decide

    let resume (state: EpistemicState) (observation: Observation) =
        match state.PendingRequest with
        | None -> state, InquiryResult.Error "no pending kernel request"
        | Some request ->
            match observationMatches request observation with
            | Error error -> state, InquiryResult.Error error
            | Ok() -> Closure.absorbAndClose state observation |> decide
