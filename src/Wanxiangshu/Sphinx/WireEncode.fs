namespace Wanxiangshu.Sphinx

open Fable.Core.JsInterop

module WireEncode =

    let private formName =
        function
        | QuestionForm.Why -> "Why"
        | QuestionForm.How -> "How"
        | QuestionForm.What -> "What"
        | QuestionForm.Who -> "Who"
        | QuestionForm.Where -> "Where"
        | QuestionForm.When -> "When"
        | QuestionForm.Which -> "Which"
        | QuestionForm.Polar -> "Polar"
        | QuestionForm.Other -> "Other"

    let private contractName =
        function
        | AnswerContract.Explanation -> "Explanation"
        | AnswerContract.Plan -> "Plan"
        | AnswerContract.Direct -> "Direct"
        | AnswerContract.Ranking -> "Ranking"
        | AnswerContract.Judgment -> "Judgment"
        | AnswerContract.Credence -> "Credence"

    let private actionKindName =
        function
        | ActionKind.Investigate -> "investigate"
        | ActionKind.Synthesize -> "synthesize"

    let private mapObject keyName distribution =
        distribution
        |> Map.toList
        |> List.map (fun (key, value) -> keyName key ==> value)
        |> createObj

    let private stringMapObject distribution =
        distribution
        |> Map.toList
        |> List.map (fun (key, value) -> key ==> value)
        |> createObj

    let private rootObject (root: RootContract) =
        createObj
            [ "formBelief" ==> mapObject formName root.FormBelief
              "contractBelief" ==> mapObject contractName root.ContractBelief
              "facets" ==> stringMapObject root.Facets
              "targets" ==> List.toArray root.Targets
              "intents" ==> List.toArray root.Intents ]

    let private actionObject (action: CognitiveAction) =
        createObj
            [ "id" ==> action.Id
              "kind" ==> actionKindName action.Kind
              "method" ==> action.Method
              "question" ==> action.Question
              "semanticKey" ==> action.SemanticKey
              "expectedRootGain" ==> action.ExpectedRootGain
              "gatewayGain" ==> action.GatewayGain
              "cost" ==> action.Cost
              "value" ==> action.Value ]

    let requestObject (request: Request) =
        match request with
        | SemanticAssessmentRequest question ->
            createObj [ "type" ==> "SemanticAssessmentRequest"; "question" ==> question ]
        | GenerateCandidatesRequest(methods, root) ->
            createObj
                [ "type" ==> "GenerateCandidatesRequest"
                  "methods" ==> List.toArray methods
                  "contract" ==> rootObject root ]
        | InvestigateRequest action -> createObj [ "type" ==> "InvestigateRequest"; "action" ==> actionObject action ]
        | SynthesizeRequest(keys, root) ->
            createObj
                [ "type" ==> "SynthesizeRequest"
                  "findingKeys" ==> List.toArray keys
                  "contract" ==> rootObject root ]

    let private findingObject (finding: Finding) =
        createObj
            [ "semanticKey" ==> finding.SemanticKey
              "text" ==> finding.Text
              "supports" ==> List.toArray finding.Supports
              "refutes" ==> List.toArray finding.Refutes
              "evidenceKeys" ==> List.toArray finding.EvidenceKeys
              "confidence"
              ==> (finding.Confidence |> Option.map box |> Option.defaultValue null)
              "provenance" ==> List.toArray finding.Provenance ]

    let private evidenceKindName =
        function
        | EvidenceKind.Document -> "document"
        | EvidenceKind.Tool -> "tool"
        | EvidenceKind.UserSupplied -> "user"
        | EvidenceKind.Measurement -> "measurement"
        | EvidenceKind.Dataset -> "dataset"
        | EvidenceKind.Other -> "other"

    let private evidenceObject (evidence: Evidence) =
        createObj
            [ "semanticKey" ==> evidence.SemanticKey
              "proposition" ==> evidence.Proposition
              "source"
              ==> createObj
                      [ "id" ==> evidence.Source.Id
                        "kind" ==> evidenceKindName evidence.Source.Kind
                        "label"
                        ==> (evidence.Source.Label |> Option.map box |> Option.defaultValue null) ]
              "dependencyKey" ==> evidence.DependencyKey
              "likelihoods" ==> stringMapObject evidence.Likelihoods
              "reliability"
              ==> (evidence.Reliability |> Option.map box |> Option.defaultValue null)
              "numericQualified" ==> evidence.NumericQualified
              "provenance" ==> List.toArray evidence.Provenance ]

    let private hypothesisObject (hypothesis: Hypothesis) =
        createObj
            [ "semanticKey" ==> hypothesis.SemanticKey
              "label" ==> hypothesis.Label
              "prior" ==> (hypothesis.Prior |> Option.map box |> Option.defaultValue null) ]

    let private synthesisObject (synthesis: SynthesisProposal) =
        createObj
            [ "text" ==> synthesis.Text
              "findingKeys" ==> List.toArray synthesis.FindingKeys
              "uncertainties" ==> List.toArray synthesis.Uncertainties ]

    let private bayesianObject (belief: BayesianBelief) =
        createObj
            [ "posterior" ==> stringMapObject belief.Posterior
              "entropy" ==> belief.Entropy
              "bayesRisk" ==> belief.BayesRisk ]

    let answerObject (answer: CanonicalAnswer) =
        createObj
            [ "question" ==> answer.Question
              "contract" ==> rootObject answer.Contract
              "epistemicBasis"
              ==> createObj
                      [ "findings" ==> (answer.Findings |> List.map findingObject |> List.toArray)
                        "evidence" ==> (answer.Evidence |> List.map evidenceObject |> List.toArray)
                        "hypotheses"
                        ==> (answer.Hypotheses |> List.map hypothesisObject |> List.toArray) ]
              "synthesis"
              ==> (answer.Synthesis
                   |> Option.map (synthesisObject >> box)
                   |> Option.defaultValue null)
              "bayesian"
              ==> (answer.Bayesian
                   |> Option.map (bayesianObject >> box)
                   |> Option.defaultValue null)
              "uncertainties" ==> List.toArray answer.Uncertainties
              "stopReason" ==> answer.StopReason
              "revision" ==> answer.Revision ]
