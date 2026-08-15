namespace Wanxiangshu.Sphinx

open System
open FsToolkit.ErrorHandling

module ObservationCodec =

    open DecodePrimitives

    let private decodeAssessment raw =
        result {
            let! forms = required "forms" formMap raw
            let! facets = optional "facets" stringMap Map.empty raw
            let! targets = optional "targets" stringList [] raw
            let! intents = optional "intents" stringList [] raw

            return
                { Forms = forms
                  Facets = facets
                  Targets = targets
                  Intents = intents }
        }

    let private decodeSemanticAssessment raw =
        decodeAssessment raw |> Result.map SemanticAssessmentObservation

    let private decodeCandidate raw =
        result {
            let! methodName = required "method" asString raw
            let! question = required "question" asString raw
            let! semanticKey = required "semanticKey" asString raw
            let! dependencyKey = optional "dependencyKey" asString "" raw
            let! rootGain = optional "expectedRootGain" asFloat 0.0 raw
            let! gatewayGain = optional "gatewayGain" asFloat 0.0 raw
            let! cost = optional "cost" asFloat 1.0 raw
            let! provenance = optional "provenance" stringList [] raw

            return
                { Method = methodName
                  Question = question
                  SemanticKey = semanticKey
                  DependencyKey =
                    if String.IsNullOrWhiteSpace dependencyKey then
                        None
                    else
                        Some dependencyKey
                  ExpectedRootGain = rootGain
                  GatewayGain = gatewayGain
                  Cost = cost
                  Provenance = provenance }
        }

    let private decodeFinding raw =
        result {
            let! semanticKey = required "semanticKey" asString raw
            let! text = required "text" asString raw
            let! supports = optional "supports" stringList [] raw
            let! refutes = optional "refutes" stringList [] raw
            let! evidenceKeys = optional "evidenceKeys" stringList [] raw
            let! confidence = optional "confidence" asFloat Double.NaN raw
            let! provenance = optional "provenance" stringList [] raw

            return
                { SemanticKey = semanticKey
                  Text = text
                  Supports = supports
                  Refutes = refutes
                  EvidenceKeys = evidenceKeys
                  Confidence = if Double.IsNaN confidence then None else Some confidence
                  Provenance = provenance }
        }

    let private decodeSource raw =
        result {
            let! id = required "id" asString raw
            let! kind = optional "kind" asString "other" raw
            let! label = optional "label" asString "" raw

            return
                { Id = id
                  Kind = parseEvidenceKind (kind.Trim().ToLowerInvariant())
                  Label = if String.IsNullOrWhiteSpace label then None else Some label }
        }

    let private decodeEvidence raw =
        result {
            let! semanticKey = required "semanticKey" asString raw
            let! proposition = required "proposition" asString raw
            let! source = required "source" decodeSource raw
            let! dependencyKey = required "dependencyKey" asString raw
            let! likelihoods = optional "likelihoods" stringMap Map.empty raw
            let! reliability = optional "reliability" asFloat Double.NaN raw
            let! numericQualified = optional "numericQualified" asBool false raw
            let! provenance = optional "provenance" stringList [] raw

            return
                { SemanticKey = semanticKey
                  Proposition = proposition
                  Source = source
                  DependencyKey = dependencyKey
                  Likelihoods = likelihoods
                  Reliability = if Double.IsNaN reliability then None else Some reliability
                  NumericQualified = numericQualified
                  Provenance = provenance }
        }

    let private decodeHypothesis raw =
        result {
            let! semanticKey = required "semanticKey" asString raw
            let! label = required "label" asString raw
            let! prior = optional "prior" asFloat Double.NaN raw

            return
                { SemanticKey = semanticKey
                  Label = label
                  Prior = if Double.IsNaN prior then None else Some prior }
        }

    let private decodeCandidates raw =
        result {
            let! items = required "items" (asArray decodeCandidate) raw
            return CandidatesObservation items
        }

    let private decodeInvestigation raw =
        result {
            let! actionKey = required "actionKey" asString raw
            let! semanticAssessment = optional "semanticAssessment" (decodeAssessment >> Result.map Some) None raw
            let! findings = optional "findings" (asArray decodeFinding) [] raw
            let! evidence = optional "evidence" (asArray decodeEvidence) [] raw
            let! hypotheses = optional "hypotheses" (asArray decodeHypothesis) [] raw
            let! candidates = optional "candidates" (asArray decodeCandidate) [] raw

            return
                InvestigationObservation
                    { ActionKey = actionKey
                      SemanticAssessment = semanticAssessment
                      Findings = findings
                      Evidence = evidence
                      Hypotheses = hypotheses
                      Candidates = candidates }
        }

    let private decodeSynthesis raw =
        result {
            let! text = required "text" asString raw
            let! findingKeys = optional "findingKeys" stringList [] raw
            let! uncertainties = optional "uncertainties" stringList [] raw

            return
                SynthesisObservation
                    { Text = text
                      FindingKeys = findingKeys
                      Uncertainties = uncertainties }
        }

    let decode raw =
        if isNullish raw || jsType raw <> "object" || isArray raw then
            Error "observation must be object"
        else
            match required "type" asString raw with
            | Error error -> Error error
            | Ok "SemanticAssessment" -> decodeSemanticAssessment raw
            | Ok "Candidates" -> decodeCandidates raw
            | Ok "Investigation" -> decodeInvestigation raw
            | Ok "Synthesis" -> decodeSynthesis raw
            | Ok kind -> Error($"unknown observation.type: {kind}")
