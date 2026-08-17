namespace Wanxiangshu.Sphinx

open System
open Fable.Core.JsInterop

/// JS-native owner boundary for the Sphinx epistemic kernel.
/// Session stores remain opaque capabilities. Solver inputs, observations,
/// state views and algorithm results cross as strings, arrays and plain objects;
/// Fable maps, lists and discriminated-union representations never cross.
module SphinxSurface =

    type private StoreHandle(store: SessionStore) =
        member _.Value = store

    let private isUndefined (value: obj) : bool = emitJsExpr value "$0 === undefined"

    let private isNullish (value: obj) = isNull value || isUndefined value

    let private text (value: obj) =
        if isNullish value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNullish value then [||] else unbox<obj array> value

    let private stringArray (value: obj) = arrayOf value |> Array.map text

    let private keysOf (value: obj) : string array = emitJsExpr value "Object.keys($0)"

    let private floatMapOf (value: obj) : Map<string, float> =
        if isNullish value then
            Map.empty
        else
            keysOf value
            |> Array.toList
            |> List.map (fun key -> key, float (emitJsExpr (value, key) "$0[$1]"))
            |> Map.ofList

    let private stringListMapOf (value: obj) : Map<string, string list> =
        if isNullish value then
            Map.empty
        else
            keysOf value
            |> Array.toList
            |> List.map (fun key -> key, emitJsExpr (value, key) "$0[$1]" |> stringArray |> Array.toList)
            |> Map.ofList

    let private optionalText (value: obj) =
        if isNullish value then None else Some(text value)

    let private storeOf (value: obj) = (unbox<StoreHandle> value).Value

    let createStore () : obj = StoreHandle(SessionStore()) :> obj

    let start (store: obj) (question: string) : obj =
        storeOf store |> fun value -> value.Start question

    let resume (store: obj) (handle: string) (observation: obj) : obj =
        storeOf store |> fun value -> value.Resume(handle, observation)

    let private findingView (finding: Finding) : obj =
        box
            {| semanticKey = finding.SemanticKey
               text = finding.Text
               supports = finding.Supports |> List.toArray
               refutes = finding.Refutes |> List.toArray
               evidenceKeys = finding.EvidenceKeys |> List.toArray
               confidence = finding.Confidence |> Option.map box |> Option.toObj
               provenance = finding.Provenance |> List.toArray |}

    let private evidenceKindName =
        function
        | EvidenceKind.Document -> "document"
        | EvidenceKind.Tool -> "tool"
        | EvidenceKind.UserSupplied -> "user"
        | EvidenceKind.Measurement -> "measurement"
        | EvidenceKind.Dataset -> "dataset"
        | EvidenceKind.Other -> "other"

    let private evidenceView (evidence: Evidence) : obj =
        box
            {| semanticKey = evidence.SemanticKey
               proposition = evidence.Proposition
               source =
                box
                    {| id = evidence.Source.Id
                       kind = evidenceKindName evidence.Source.Kind
                       label = evidence.Source.Label |> Option.map box |> Option.toObj |}
               dependencyKey = evidence.DependencyKey
               likelihoods =
                evidence.Likelihoods
                |> Map.toList
                |> List.map (fun (key, value) -> key ==> value)
                |> createObj
               reliability = evidence.Reliability |> Option.map box |> Option.toObj
               numericQualified = evidence.NumericQualified
               provenance = evidence.Provenance |> List.toArray |}

    let private actionKindName =
        function
        | ActionKind.Investigate -> "investigate"
        | ActionKind.Synthesize -> "synthesize"

    let private actionStatusName =
        function
        | ActionStatus.Open -> "open"
        | ActionStatus.Selected -> "selected"
        | ActionStatus.Resolved -> "resolved"

    let private actionView (action: CognitiveAction) : obj =
        box
            {| id = action.Id
               kind = actionKindName action.Kind
               methodName = action.Method
               question = action.Question
               semanticKey = action.SemanticKey
               equivalenceKey = action.EquivalenceKey |> Option.map box |> Option.toObj
               dependencyKey = action.DependencyKey |> Option.map box |> Option.toObj
               expectedRootGain = action.ExpectedRootGain
               gatewayGain = action.GatewayGain
               cost = action.Cost
               value = action.Value
               status = actionStatusName action.Status
               provenance = action.Provenance |> List.toArray |}

    let private rootView (root: RootContract) : obj =
        let formName =
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

        let contractName =
            function
            | AnswerContract.Explanation -> "Explanation"
            | AnswerContract.Plan -> "Plan"
            | AnswerContract.Direct -> "Direct"
            | AnswerContract.Ranking -> "Ranking"
            | AnswerContract.Judgment -> "Judgment"
            | AnswerContract.Credence -> "Credence"

        box
            {| formBelief =
                root.FormBelief
                |> Map.toList
                |> List.map (fun (key, value) -> formName key ==> value)
                |> createObj
               contractBelief =
                root.ContractBelief
                |> Map.toList
                |> List.map (fun (key, value) -> contractName key ==> value)
                |> createObj
               facets =
                root.Facets
                |> Map.toList
                |> List.map (fun (key, value) -> key ==> value)
                |> createObj
               targets = root.Targets |> List.toArray
               intents = root.Intents |> List.toArray |}

    let private bayesianView (belief: BayesianBelief) : obj =
        box
            {| posterior =
                belief.Posterior
                |> Map.toList
                |> List.map (fun (key, value) -> key ==> value)
                |> createObj
               entropy = belief.Entropy
               bayesRisk = belief.BayesRisk |}

    let private stateView (state: EpistemicState) : obj =
        box
            {| rootQuestion = state.RootQuestion
               rootContract = state.RootContract |> Option.map rootView |> Option.toObj
               findings = state.Findings |> Map.toList |> List.map (snd >> findingView) |> List.toArray
               evidence = state.Evidence |> Map.toList |> List.map (snd >> evidenceView) |> List.toArray
               hypotheses =
                state.Hypotheses
                |> Map.toList
                |> List.map (fun (_, hypothesis) ->
                    box
                        {| semanticKey = hypothesis.SemanticKey
                           label = hypothesis.Label
                           prior = hypothesis.Prior |> Option.map box |> Option.toObj |})
                |> List.toArray
               actions = state.Actions |> Map.toList |> List.map (snd >> actionView) |> List.toArray
               bayesian = state.Bayesian |> Option.map bayesianView |> Option.toObj
               revision = state.Revision |}

    let state (store: obj) (handle: string) : obj =
        storeOf store
        |> fun value -> value.TryState handle |> Option.map stateView |> Option.toObj

    let close (store: obj) (handle: string) : obj =
        storeOf store
        |> fun value -> value.TryState handle |> Option.map (Closure.close >> stateView) |> Option.toObj

    let private lookupError (failure: SessionFailure) (handle: string option) : obj =
        { Handle = handle
          State = None
          Failure = failure }
        |> McpContract.failureView
        |> McpContract.errorObject

    let status (store: obj) (handle: string) : obj =
        match (storeOf store).Status handle with
        | LookupOutcome.Found(foundHandle, sessionStatus) -> McpContract.statusPayload foundHandle sessionStatus
        | LookupOutcome.MissingHandle -> lookupError SessionFailure.MissingHandle None
        | LookupOutcome.UnknownHandle unknownHandle -> lookupError SessionFailure.UnknownHandle (Some unknownHandle)

    let cancel (store: obj) (handle: string) : obj =
        match (storeOf store).Cancel handle with
        | LookupOutcome.Found(foundHandle, ()) -> McpContract.cancelPayload foundHandle
        | LookupOutcome.MissingHandle -> lookupError SessionFailure.MissingHandle None
        | LookupOutcome.UnknownHandle unknownHandle -> lookupError SessionFailure.UnknownHandle (Some unknownHandle)

    let private observationTypeName (observation: Observation) : string =
        match observation with
        | SemanticAssessmentObservation _ -> "SemanticAssessment"
        | CandidatesObservation _ -> "Candidates"
        | InvestigationObservation _ -> "Investigation"
        | SynthesisObservation _ -> "Synthesis"

    let private decodeResult (result: Result<Observation, string>) : obj =
        match result with
        | Ok observation ->
            box
                {| ok = true
                   observationType = observationTypeName observation |}
        | Error error -> box {| ok = false; error = error |}

    let decode (raw: obj) : obj =
        ObservationCodec.decode raw |> decodeResult

    let decodeSemanticAssessmentObservation (raw: obj) : obj =
        ObservationCodec.decodeSemanticAssessment raw |> decodeResult

    let decodeCandidatesObservation (raw: obj) : obj =
        ObservationCodec.decodeCandidates raw |> decodeResult

    let decodeInvestigationObservation (raw: obj) : obj =
        ObservationCodec.decodeInvestigation raw |> decodeResult

    let decodeSynthesisObservation (raw: obj) : obj =
        ObservationCodec.decodeSynthesis raw |> decodeResult

    let mcpServer (store: obj) : obj = McpServer.create (storeOf store)

    let serverName = SphinxMcp.serverName
    let permissionKey = SphinxMcp.permissionKey
    let relativeServerEntry = SphinxMcp.relativeServerEntry
    let isTool (name: string) = SphinxMcp.isTool name
    let localCommand (entryPath: string) = SphinxMcp.localCommand entryPath
    let fixtureCommand (fixturePath: string) = SphinxMcp.fixtureCommand fixturePath

    let libraryNames () : string array =
        Methodology.library
        |> List.map (fun definition -> definition.Name)
        |> List.toArray

    let phase0MethodNames () : string array = Methodology.phase0Names |> Set.toArray

    let private edgeOf (value: obj) : Search.GraphEdge =
        { FromNode = text value?``from``
          ToNode = text value?``to``
          Cost = float value?cost }

    let private problemOf (value: obj) : Search.AStarProblem =
        { Start = text value?start
          Goal = text value?goal
          Edges = arrayOf value?edges |> Array.toList |> List.map edgeOf
          Heuristic = floatMapOf value?heuristic }

    let solveGraph (problem: obj) : obj =
        match Search.solveGraph (problemOf problem) with
        | None -> null
        | Some solved ->
            box
                {| path = solved.Path |> List.toArray
                   cost = solved.Cost
                   expanded = solved.Expanded |> List.toArray |}

    let private modelOf (value: obj) : MonteCarlo.Model =
        { Root = text value?root
          Children = stringListMapOf value?children
          TerminalReward = floatMapOf value?terminalReward
          Prior = floatMapOf value?prior }

    let mctsRun (iterations: int) (model: obj) : obj =
        let result = MonteCarlo.run iterations (modelOf model)

        box
            {| bestAction = result.BestAction |> Option.toObj
               nodes =
                result.Nodes
                |> Map.toList
                |> List.map (fun (_, node) ->
                    box
                        {| semanticKey = node.SemanticKey
                           visits = node.Visits
                           valueSum = node.ValueSum
                           prior = node.Prior |})
                |> List.toArray
               iterations = result.Iterations |}

    let mctsUct (parentVisits: int) (exploration: float) (node: obj) : float =
        MonteCarlo.uct
            parentVisits
            exploration
            { SemanticKey = text node?semanticKey
              Visits = int node?visits
              ValueSum = float node?valueSum
              Prior = float node?prior }

    let private actionOf (value: obj) : CognitiveAction =
        let kind =
            if text value?kind = "synthesize" then
                ActionKind.Synthesize
            else
                ActionKind.Investigate

        let status =
            match text value?status with
            | "selected" -> ActionStatus.Selected
            | "resolved" -> ActionStatus.Resolved
            | _ -> ActionStatus.Open

        { Id = text value?id
          Kind = kind
          Method = text value?methodName
          Question = text value?question
          SemanticKey = text value?semanticKey
          EquivalenceKey = optionalText value?equivalenceKey
          DependencyKey = optionalText value?dependencyKey
          ExpectedRootGain = float value?expectedRootGain
          GatewayGain = float value?gatewayGain
          Cost = float value?cost
          Value = float value?value
          Status = status
          Provenance = stringArray value?provenance |> Array.toList }

    let paretoFrontier (actions: obj array) : obj array =
        actions
        |> Array.toList
        |> List.map actionOf
        |> Representation.paretoFrontier
        |> List.map actionView
        |> List.toArray
