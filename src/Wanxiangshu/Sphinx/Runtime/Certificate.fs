namespace Wanxiangshu.Sphinx.Runtime

open System
open Wanxiangshu.Sphinx.Core

type CertificateError = { Code: string; Message: string }

type CertificatePatchRequest =
    { Slot: string
      Value: obj option
      Lower: float option
      Upper: float option
      Summary: obj option
      Constraints: obj option
      Posterior: obj option
      ResidualValue: float option
      GuaranteeKind: string option
      Level: float option
      Error: float option
      Assumptions: string[] option
      Scope: string option
      Witnesses: EventId list
      Derivations: EventId list }

module Certificate =

    let private error code message =
        Error { Code = code; Message = message }

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private exactSchema: SchemaRef =
        { Id = "sphinx.certificate/exact@1"
          Hash = "sphinx-certificate-exact-v1" }

    let private boundSchema: SchemaRef =
        { Id = "sphinx.certificate/bound@1"
          Hash = "sphinx-certificate-bound-v1" }

    let private sampleSchema: SchemaRef =
        { Id = "sphinx.certificate/sample@1"
          Hash = "sphinx-certificate-sample-v1" }

    let private ordinalSchema: SchemaRef =
        { Id = "sphinx.certificate/ordinal@1"
          Hash = "sphinx-certificate-ordinal-v1" }

    let private latentSchema: SchemaRef =
        { Id = "sphinx.certificate/latent@1"
          Hash = "sphinx-certificate-latent-v1" }

    let private residualSchema: SchemaRef =
        { Id = "sphinx.certificate/residual@1"
          Hash = "sphinx-certificate-residual-v1" }

    let private addUnique existing additions =
        existing
        @ (additions |> List.filter (fun item -> not (List.contains item existing)))

    let private assumptionSet names =
        match names with
        | None -> Set.empty
        | Some values -> values |> Array.filter (not << String.IsNullOrWhiteSpace) |> Set.ofArray

    let private envelope schema payload =
        match JsonEnvelope.create schema payload with
        | Ok entry -> Ok entry
        | Error _ -> error "invalid-patch" "certificate payload is not encodable"

    let private scopeOr scope fallback =
        match scope with
        | Some name when not (String.IsNullOrWhiteSpace name) -> name
        | _ -> fallback

    let private requireInclusionGuarantee (patch: CertificatePatchRequest) (message: string) =
        match patch.GuaranteeKind with
        | Some kind when kind = "inclusion" -> Ok(assumptionSet patch.Assumptions)
        | _ -> error "missing-guarantee" message

    let private requireCoverageKind (patch: CertificatePatchRequest) (message: string) =
        match patch.GuaranteeKind with
        | Some kind when kind = "coverage" -> Ok()
        | _ -> error "missing-coverage" message

    let private requireOrdinalKind (patch: CertificatePatchRequest) (message: string) =
        match patch.GuaranteeKind with
        | Some kind when kind = "ordinal" -> Ok()
        | _ -> error "missing-guarantee" message

    let private validateBoundRange (patch: CertificatePatchRequest) =
        match patch.Lower, patch.Upper with
        | Some lower, Some upper when isFiniteNumber lower && isFiniteNumber upper && lower <= upper -> Ok(lower, upper)
        | _ -> error "invalid-bound" "bound patch requires finite lower and upper with lower <= upper"

    let private requireCoverageLevels (patch: CertificatePatchRequest) (message: string) =
        let assumptions = assumptionSet patch.Assumptions

        match patch.Level, patch.Error with
        | Some level, Some margin when
            isFiniteNumber level
            && level > 0.0
            && level < 1.0
            && isFiniteNumber margin
            && margin >= 0.0
            && Set.count assumptions > 0
            ->
            Ok(level, margin, assumptions)
        | _ -> error "missing-coverage" message

    let private appendOrdinalConstraint (prior: JsonEnvelope list) (entry: JsonEnvelope) =
        if List.contains entry prior then
            prior
        else
            prior @ [ entry ]

    let private applyExact
        (certificate: ValueCertificate)
        (patch: CertificatePatchRequest)
        (advance: ValueCertificate -> ValueCertificate)
        =
        match patch.Value with
        | None -> error "invalid-patch" "exact patch requires a value"
        | Some value ->
            requireInclusionGuarantee patch "exact slot requires a deterministic inclusion guarantee"
            |> Result.bind (fun assumptions ->
                envelope exactSchema value
                |> Result.map (fun (entry: JsonEnvelope) ->
                    advance
                        { certificate with
                            Exact = Some entry
                            Guarantees =
                                certificate.Guarantees |> Map.add "exact" (DeterministicInclusion assumptions) }))

    let private applyBound
        (certificate: ValueCertificate)
        (patch: CertificatePatchRequest)
        (advance: ValueCertificate -> ValueCertificate)
        =
        validateBoundRange patch
        |> Result.bind (fun (lower, upper) ->
            requireInclusionGuarantee patch "bound slot requires a deterministic inclusion guarantee"
            |> Result.bind (fun assumptions ->
                envelope boundSchema (box lower)
                |> Result.bind (fun (lowerEntry: JsonEnvelope) ->
                    envelope boundSchema (box upper)
                    |> Result.map (fun (upperEntry: JsonEnvelope) ->
                        advance
                            { certificate with
                                LowerEnvelope = Some lowerEntry
                                UpperEnvelope = Some upperEntry
                                Guarantees =
                                    certificate.Guarantees |> Map.add "bound" (DeterministicInclusion assumptions) }))))

    let private applySample
        (certificate: ValueCertificate)
        (patch: CertificatePatchRequest)
        (advance: ValueCertificate -> ValueCertificate)
        =
        match patch.Summary with
        | None -> error "invalid-patch" "sample patch requires a summary"
        | Some summary ->
            requireCoverageKind patch "sample slot requires a probabilistic coverage guarantee"
            |> Result.bind (fun () ->
                requireCoverageLevels patch "sample slot requires explicit level, error, assumptions and scope"
                |> Result.bind (fun (level, margin, assumptions) ->
                    envelope sampleSchema summary
                    |> Result.map (fun (entry: JsonEnvelope) ->
                        advance
                            { certificate with
                                SampleSummary = Some entry
                                Guarantees =
                                    certificate.Guarantees
                                    |> Map.add
                                        "sample"
                                        (ProbabilisticCoverage(
                                            level,
                                            margin,
                                            assumptions,
                                            scopeOr patch.Scope "sample"
                                        )) })))

    let private applyOrdinal
        (certificate: ValueCertificate)
        (patch: CertificatePatchRequest)
        (advance: ValueCertificate -> ValueCertificate)
        =
        match patch.Constraints with
        | None -> error "invalid-patch" "ordinal patch requires constraints"
        | Some constraints ->
            requireOrdinalKind patch "ordinal slot requires an ordinal guarantee"
            |> Result.bind (fun () ->
                envelope ordinalSchema constraints
                |> Result.map (fun (entry: JsonEnvelope) ->
                    advance
                        { certificate with
                            OrdinalConstraints = appendOrdinalConstraint certificate.OrdinalConstraints entry
                            Guarantees =
                                certificate.Guarantees
                                |> Map.add "ordinal" (OrdinalModel(assumptionSet patch.Assumptions)) }))

    let private applyLatent
        (certificate: ValueCertificate)
        (patch: CertificatePatchRequest)
        (advance: ValueCertificate -> ValueCertificate)
        =
        match patch.Posterior with
        | None -> error "invalid-patch" "latent patch requires a posterior"
        | Some posterior ->
            requireCoverageKind patch "latent slot requires a probabilistic coverage guarantee"
            |> Result.bind (fun () ->
                requireCoverageLevels patch "latent slot requires explicit level, error, assumptions and scope"
                |> Result.bind (fun (level, margin, assumptions) ->
                    envelope latentSchema posterior
                    |> Result.map (fun (entry: JsonEnvelope) ->
                        advance
                            { certificate with
                                LatentPosterior = Some entry
                                Guarantees =
                                    certificate.Guarantees
                                    |> Map.add
                                        "latent"
                                        (ProbabilisticCoverage(
                                            level,
                                            margin,
                                            assumptions,
                                            scopeOr patch.Scope "latent"
                                        )) })))

    let private applyResidual
        (certificate: ValueCertificate)
        (patch: CertificatePatchRequest)
        (advance: ValueCertificate -> ValueCertificate)
        =
        match patch.ResidualValue with
        | Some value when isFiniteNumber value ->
            envelope residualSchema (box value)
            |> Result.map (fun (entry: JsonEnvelope) ->
                advance
                    { certificate with
                        Residual = Some entry
                        Guarantees = certificate.Guarantees |> Map.add "residual" ResidualOnly })
        | _ -> error "invalid-patch" "residual patch requires a finite value"

    let empty nodeId =
        { NodeId = nodeId
          Semantics = None
          Exact = None
          LowerEnvelope = None
          UpperEnvelope = None
          SampleSummary = None
          OrdinalConstraints = []
          LatentPosterior = None
          Residual = None
          Guarantees = Map.empty
          WitnessEvents = []
          DerivationEvents = []
          Revision = 0L }

    let apply (certificate: ValueCertificate) (patch: CertificatePatchRequest) =
        let witnesses = addUnique certificate.WitnessEvents patch.Witnesses
        let derivations = addUnique certificate.DerivationEvents patch.Derivations

        let advance (current: ValueCertificate) =
            { current with
                WitnessEvents = witnesses
                DerivationEvents = derivations
                Revision = certificate.Revision + 1L }

        match patch.Slot with
        | "exact" -> applyExact certificate patch advance
        | "bound" -> applyBound certificate patch advance
        | "sample" -> applySample certificate patch advance
        | "ordinal" -> applyOrdinal certificate patch advance
        | "latent" -> applyLatent certificate patch advance
        | "residual" -> applyResidual certificate patch advance
        | "witness" -> Ok(advance certificate)
        | _ -> error "invalid-patch" "certificate patch names an unknown slot"
