namespace Wanxiangshu.Sphinx

open System

module Absorb =

    let private normalizeKey (text: string) = text.Trim().ToLowerInvariant()

    let private actionId (proposal: CandidateProposal) =
        let dependency = proposal.DependencyKey |> Option.defaultValue "independent"
        normalizeKey (proposal.SemanticKey + "|" + dependency)

    let private gainFromMethod state (methodDef: Methodology.MethodDefinition option) =
        match methodDef with
        | Some def ->
            let u = Methodology.utility state def
            max 0.35 (min 0.95 u)
        | None -> 0.60

    let private rootGainForProposal
        state
        (proposal: CandidateProposal)
        (methodDef: Methodology.MethodDefinition option)
        =
        if proposal.ExpectedRootGain > 0.0 then
            proposal.ExpectedRootGain
        else
            gainFromMethod state methodDef

    let private costFromMethod (methodDef: Methodology.MethodDefinition option) =
        match methodDef with
        | Some def -> max 0.10 (min 0.50 (0.15 * def.BaseCost))
        | None -> 0.15

    let private costForProposal (proposal: CandidateProposal) (methodDef: Methodology.MethodDefinition option) =
        if proposal.Cost > 0.0 then
            proposal.Cost
        else
            costFromMethod methodDef

    let private dependentCount (semanticKey: string) (actions: Map<string, CognitiveAction>) =
        actions
        |> Map.toSeq
        |> Seq.filter (fun (_, a) -> a.DependencyKey = Some semanticKey)
        |> Seq.length

    let private gatewayFromCount (count: int) =
        if count > 0 then min 1.0 (0.25 * float count) else 0.0

    let private gatewayForProposal (state: EpistemicState) (proposal: CandidateProposal) =
        if proposal.GatewayGain > 0.0 then
            proposal.GatewayGain
        else
            let normKey = normalizeKey proposal.SemanticKey
            dependentCount normKey state.Actions |> gatewayFromCount

    let private actionFromProposal (state: EpistemicState) (proposal: CandidateProposal) =
        let methodDef =
            Methodology.library
            |> List.tryFind (fun m -> String.Equals(m.Name, proposal.Method, StringComparison.OrdinalIgnoreCase))

        { Id = actionId proposal
          Kind = ActionKind.Investigate
          Method = proposal.Method
          Question = proposal.Question
          SemanticKey = normalizeKey proposal.SemanticKey
          EquivalenceKey = None
          DependencyKey = proposal.DependencyKey |> Option.map normalizeKey
          ExpectedRootGain = rootGainForProposal state proposal methodDef
          GatewayGain = gatewayForProposal state proposal
          Cost = costForProposal proposal methodDef
          Value = 0.0
          Status = ActionStatus.Open
          Provenance = proposal.Provenance |> List.distinct }

    let private betterCandidate (left: CognitiveAction) (right: CognitiveAction) =
        let leftScore = left.ExpectedRootGain + 0.65 * left.GatewayGain - left.Cost
        let rightScore = right.ExpectedRootGain + 0.65 * right.GatewayGain - right.Cost
        let selected = if rightScore > leftScore then right else left

        { selected with
            Provenance = List.distinct (left.Provenance @ right.Provenance) }

    let private addCandidate proposal (state: EpistemicState) =
        let action = actionFromProposal state proposal

        let actions =
            match Map.tryFind action.Id state.Actions with
            | None -> state.Actions |> Map.add action.Id action
            | Some previous -> state.Actions |> Map.add action.Id (betterCandidate previous action)

        { state with Actions = actions }

    let private mergeFinding (existing: Finding) (incoming: Finding) =
        { existing with
            Supports = List.distinct (existing.Supports @ incoming.Supports)
            Refutes = List.distinct (existing.Refutes @ incoming.Refutes)
            EvidenceKeys = List.distinct (existing.EvidenceKeys @ incoming.EvidenceKeys)
            Provenance = List.distinct (existing.Provenance @ incoming.Provenance) }

    let private addFinding (finding: Finding) (state: EpistemicState) =
        let key = normalizeKey finding.SemanticKey

        let normalized =
            { finding with
                SemanticKey = key
                Supports = finding.Supports |> List.map normalizeKey |> List.distinct
                Refutes = finding.Refutes |> List.map normalizeKey |> List.distinct
                EvidenceKeys = finding.EvidenceKeys |> List.map normalizeKey |> List.distinct
                Confidence = None
                Provenance = finding.Provenance |> List.distinct }

        let findings =
            match Map.tryFind key state.Findings with
            | None -> state.Findings |> Map.add key normalized
            | Some existing -> state.Findings |> Map.add key (mergeFinding existing normalized)

        { state with Findings = findings }

    let private addEvidence (evidence: Evidence) (state: EpistemicState) =
        let semanticKey = normalizeKey evidence.SemanticKey
        let dependency = normalizeKey evidence.DependencyKey
        let storageKey = semanticKey + "|dependency:" + dependency

        let normalized =
            { evidence with
                SemanticKey = semanticKey
                DependencyKey = dependency
                Provenance = evidence.Provenance |> List.distinct }

        match Map.tryFind storageKey state.Evidence with
        | Some existing ->
            { state with
                Evidence =
                    state.Evidence
                    |> Map.add
                        storageKey
                        { existing with
                            Provenance = List.distinct (existing.Provenance @ normalized.Provenance) } }
        | None ->
            { state with
                Evidence = state.Evidence |> Map.add storageKey normalized
                Dependencies = State.addDependency dependency storageKey state.Dependencies }

    let private addHypothesis (hypothesis: Hypothesis) (state: EpistemicState) =
        let key = normalizeKey hypothesis.SemanticKey
        let normalized = { hypothesis with SemanticKey = key }

        if Map.containsKey key state.Hypotheses then
            state
        else
            { state with
                Hypotheses = state.Hypotheses |> Map.add key normalized }

    let private sanitizeSynthesis (state: EpistemicState) (synthesis: SynthesisProposal) =
        { synthesis with
            FindingKeys =
                synthesis.FindingKeys
                |> List.map normalizeKey
                |> List.filter (fun key -> Map.containsKey key state.Findings)
                |> List.distinct
            Uncertainties = synthesis.Uncertainties |> List.distinct }

    let private applyInvestigation (baseState: EpistemicState) (result: InvestigationResult) =
        let resolved =
            State.markActionResolved (normalizeKey result.ActionKey) { baseState with Synthesis = None }

        let semanticallyUpdated =
            match result.SemanticAssessment with
            | None -> resolved
            | Some assessment ->
                { resolved with
                    RootContract = Some(State.deriveRootContract assessment) }

        let findings =
            result.Findings
            |> List.fold (fun current item -> addFinding item current) semanticallyUpdated

        let evidence =
            result.Evidence
            |> List.fold (fun current item -> addEvidence item current) findings

        let hypotheses =
            result.Hypotheses
            |> List.fold (fun current item -> addHypothesis item current) evidence

        result.Candidates
        |> List.fold
            (fun current proposal -> addCandidate proposal current)
            { hypotheses with
                NeedsGeneration = true }

    let apply (state: EpistemicState) (observation: Observation) =
        let baseState =
            { state with
                PendingRequest = None
                Revision = state.Revision + 1 }

        match observation with
        | SemanticAssessmentObservation assessment ->
            { baseState with
                RootContract = Some(State.deriveRootContract assessment)
                NeedsGeneration = true }
        | CandidatesObservation proposals ->
            proposals
            |> List.fold
                (fun current proposal -> addCandidate proposal current)
                { baseState with
                    NeedsGeneration = false }
        | InvestigationObservation result -> applyInvestigation baseState result
        | SynthesisObservation synthesis ->
            let state' =
                { baseState with
                    Synthesis = Some(sanitizeSynthesis baseState synthesis) }

            State.markActionResolved "synthesis" state'
