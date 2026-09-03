namespace Wanxiangshu.Sphinx.Runtime

open System
open Wanxiangshu.Sphinx.Core

type CertificateError =
    { Code: string
      Message: string }

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

    let private error code message = Error { Code = code; Message = message }

    let private isFiniteNumber (value: float) : bool = not (Double.IsNaN value || Double.IsInfinity value)

    let private exactSchema : SchemaRef =
        { Id = "sphinx.certificate/exact@1"
          Hash = "sphinx-certificate-exact-v1" }

    let private boundSchema : SchemaRef =
        { Id = "sphinx.certificate/bound@1"
          Hash = "sphinx-certificate-bound-v1" }

    let private sampleSchema : SchemaRef =
        { Id = "sphinx.certificate/sample@1"
          Hash = "sphinx-certificate-sample-v1" }

    let private ordinalSchema : SchemaRef =
        { Id = "sphinx.certificate/ordinal@1"
          Hash = "sphinx-certificate-ordinal-v1" }

    let private latentSchema : SchemaRef =
        { Id = "sphinx.certificate/latent@1"
          Hash = "sphinx-certificate-latent-v1" }

    let private residualSchema : SchemaRef =
        { Id = "sphinx.certificate/residual@1"
          Hash = "sphinx-certificate-residual-v1" }

    let private addUnique existing additions =
        existing @ (additions |> List.filter (fun item -> not (List.contains item existing)))

    let private assumptionSet names =
        match names with
        | None -> Set.empty
        | Some values ->
            values |> Array.filter (not << String.IsNullOrWhiteSpace) |> Set.ofArray

    let private envelope schema payload =
        match JsonEnvelope.create schema payload with
        | Ok entry -> Ok entry
        | Error _ -> error "invalid-patch" "certificate payload is not encodable"

    let private scopeOr scope fallback =
        match scope with
        | Some name when not (String.IsNullOrWhiteSpace name) -> name
        | _ -> fallback

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

    let apply certificate patch =
        let witnesses = addUnique certificate.WitnessEvents patch.Witnesses
        let derivations = addUnique certificate.DerivationEvents patch.Derivations

        let advance current =
            { current with
                WitnessEvents = witnesses
                DerivationEvents = derivations
                Revision = certificate.Revision + 1L }

        match patch.Slot with
        | "exact" ->
            match patch.Value with
            | None -> error "invalid-patch" "exact patch requires a value"
            | Some value ->
                match patch.GuaranteeKind with
                | Some kind when kind = "inclusion" ->
                    envelope exactSchema value
                    |> Result.map (fun entry ->
                        advance
                            { certificate with
                                Exact = Some entry
                                Guarantees =
                                    certificate.Guarantees
                                    |> Map.add "exact" (DeterministicInclusion(assumptionSet patch.Assumptions)) })
                | _ -> error "missing-guarantee" "exact slot requires a deterministic inclusion guarantee"
        | "bound" ->
            match patch.Lower, patch.Upper with
            | Some lower, Some upper when isFiniteNumber lower && isFiniteNumber upper && lower <= upper ->
                match patch.GuaranteeKind with
                | Some kind when kind = "inclusion" ->
                    envelope boundSchema (box lower)
                    |> Result.bind (fun lowerEntry ->
                        envelope boundSchema (box upper)
                        |> Result.map (fun upperEntry ->
                            advance
                                { certificate with
                                    LowerEnvelope = Some lowerEntry
                                    UpperEnvelope = Some upperEntry
                                    Guarantees =
                                        certificate.Guarantees
                                        |> Map.add "bound" (DeterministicInclusion(assumptionSet patch.Assumptions)) }))
                | _ -> error "missing-guarantee" "bound slot requires a deterministic inclusion guarantee"
            | _ -> error "invalid-bound" "bound patch requires finite lower and upper with lower <= upper"
        | "sample" ->
            match patch.Summary with
            | None -> error "invalid-patch" "sample patch requires a summary"
            | Some summary ->
                match patch.GuaranteeKind with
                | Some kind when kind = "coverage" ->
                    let assumptions = assumptionSet patch.Assumptions

                    match patch.Level, patch.Error with
                    | Some level, Some margin
                        when isFiniteNumber level
                             && level > 0.0
                             && level < 1.0
                             && isFiniteNumber margin
                             && margin >= 0.0
                             && Set.count assumptions > 0 ->
                        envelope sampleSchema summary
                        |> Result.map (fun entry ->
                            advance
                                { certificate with
                                    SampleSummary = Some entry
                                    Guarantees =
                                        certificate.Guarantees
                                        |> Map.add
                                            "sample"
                                            (ProbabilisticCoverage(level, margin, assumptions, scopeOr patch.Scope "sample")) })
                    | _ ->
                        error
                            "missing-coverage"
                            "sample slot requires explicit level, error, assumptions and scope"
                | _ -> error "missing-coverage" "sample slot requires a probabilistic coverage guarantee"
        | "ordinal" ->
            match patch.Constraints with
            | None -> error "invalid-patch" "ordinal patch requires constraints"
            | Some constraints ->
                match patch.GuaranteeKind with
                | Some kind when kind = "ordinal" ->
                    envelope ordinalSchema constraints
                    |> Result.map (fun entry ->
                        let prior = certificate.OrdinalConstraints

                        let grown =
                            if List.contains entry prior then
                                prior
                            else
                                prior @ [ entry ]

                        advance
                            { certificate with
                                OrdinalConstraints = grown
                                Guarantees =
                                    certificate.Guarantees
                                    |> Map.add "ordinal" (OrdinalModel(assumptionSet patch.Assumptions)) })
                | _ -> error "missing-guarantee" "ordinal slot requires an ordinal guarantee"
        | "latent" ->
            match patch.Posterior with
            | None -> error "invalid-patch" "latent patch requires a posterior"
            | Some posterior ->
                match patch.GuaranteeKind with
                | Some kind when kind = "coverage" ->
                    let assumptions = assumptionSet patch.Assumptions

                    match patch.Level, patch.Error with
                    | Some level, Some margin
                        when isFiniteNumber level
                             && level > 0.0
                             && level < 1.0
                             && isFiniteNumber margin
                             && margin >= 0.0
                             && Set.count assumptions > 0 ->
                        envelope latentSchema posterior
                        |> Result.map (fun entry ->
                            advance
                                { certificate with
                                    LatentPosterior = Some entry
                                    Guarantees =
                                        certificate.Guarantees
                                        |> Map.add
                                            "latent"
                                            (ProbabilisticCoverage(level, margin, assumptions, scopeOr patch.Scope "latent")) })
                    | _ ->
                        error
                            "missing-coverage"
                            "latent slot requires explicit level, error, assumptions and scope"
                | _ -> error "missing-coverage" "latent slot requires a probabilistic coverage guarantee"
        | "residual" ->
            match patch.ResidualValue with
            | Some value when isFiniteNumber value ->
                envelope residualSchema (box value)
                |> Result.map (fun entry ->
                    advance
                        { certificate with
                            Residual = Some entry
                            Guarantees = certificate.Guarantees |> Map.add "residual" ResidualOnly })
            | _ -> error "invalid-patch" "residual patch requires a finite value"
        | "witness" -> Ok(advance certificate)
        | _ -> error "invalid-patch" "certificate patch names an unknown slot"
