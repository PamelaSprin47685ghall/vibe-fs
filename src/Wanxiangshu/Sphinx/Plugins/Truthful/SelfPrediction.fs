namespace Wanxiangshu.Sphinx.Plugins.Truthful

open System
open FsToolkit.ErrorHandling
open Wanxiangshu.Sphinx.Core

module SelfPrediction =
    type PredictionError =
        | BlankWorkId
        | EmptyForecast
        | NegativeProbability of string
        | NonFiniteProbability of string
        | SimplexViolation of float
        | InvalidEpsilon
        | UnknownOutcome of string
        | NotCommitted
        | SealMismatch

    type Seal =
        { WorkId: string
          Outcomes: string list
          Digest: string }

    type AssessmentInput =
        { WorkId: string
          Forecast: Map<string, float>
          Outcome: string
          Epsilon: float
          CommittedBeforeStimulus: bool
          HeldOut: bool }

    type CalibrationNote =
        { Predicted: float
          Resolved: float
          HeldOut: bool }

    type Assessment =
        { WorkId: string
          Outcome: string
          LogScore: float
          BrierScore: float
          Epsilon: float
          Sharpness: float
          Calibration: CalibrationNote
          CalibrationUpdateAllowed: bool
          Assumptions: Set<string> }

    type ScoreInput =
        { Seal: Seal
          Forecast: Map<string, float>
          Outcome: string
          Epsilon: float
          HeldOut: bool }

    let simplexTolerance = 1e-9

    let predictionErrorCode =
        function
        | BlankWorkId -> "blank-work-id"
        | EmptyForecast -> "empty-forecast"
        | NegativeProbability _ -> "simplex-negative-probability"
        | NonFiniteProbability _ -> "simplex-non-finite-probability"
        | SimplexViolation _ -> "simplex-sum-violation"
        | InvalidEpsilon -> "invalid-epsilon"
        | UnknownOutcome _ -> "unknown-outcome"
        | NotCommitted -> "not-committed-before-stimulus"
        | SealMismatch -> "seal-mismatch"

    let private finite value =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private checkForecastFinite (forecast: Map<string, float>) : Result<unit, PredictionError> =
        match
            forecast
            |> Map.tryPick (fun outcome probability -> if finite probability then None else Some outcome)
        with
        | Some outcome -> Error(NonFiniteProbability outcome)
        | None -> Ok()

    let private checkForecastNegative (forecast: Map<string, float>) : Result<unit, PredictionError> =
        match
            forecast
            |> Map.tryPick (fun outcome probability -> if probability < 0.0 then Some outcome else None)
        with
        | Some outcome -> Error(NegativeProbability outcome)
        | None -> Ok()

    let private checkForecastTotal (forecast: Map<string, float>) : Result<unit, PredictionError> =
        let total = forecast |> Map.fold (fun sum _ probability -> sum + probability) 0.0

        if abs (total - 1.0) > simplexTolerance then
            Error(SimplexViolation total)
        else
            Ok()

    let private forecastOutcomes (forecast: Map<string, float>) : string list =
        forecast |> Map.toList |> List.map fst |> List.sort

    let private validateForecast (forecast: Map<string, float>) =
        if Map.isEmpty forecast then
            Error EmptyForecast
        else
            result {
                do! checkForecastFinite forecast
                do! checkForecastNegative forecast
                do! checkForecastTotal forecast
                return forecastOutcomes forecast
            }

    let private digest (workId: string) (outcomes: string list) (forecast: Map<string, float>) =
        let parts =
            outcomes |> List.map (fun outcome -> outcome + "=" + string forecast.[outcome])

        CoreHash.sha256Hex (workId + "|" + String.concat "," outcomes + "|" + String.concat "," parts)

    let commit (workId: string) (forecast: Map<string, float>) : Result<Seal, PredictionError> =
        if String.IsNullOrWhiteSpace workId then
            Error BlankWorkId
        else
            validateForecast forecast
            |> Result.map (fun outcomes ->
                ({ WorkId = workId
                   Outcomes = outcomes
                   Digest = digest workId outcomes forecast }
                : Seal))

    // M-4: CommittedBeforeStimulus is caller-attested (the seal binds content, not
    // timing), so record host attestation; Brier is reported in loss form
    // (sum (f - 1_y)^2, lower-is-better), not the App.J maximized-score form.
    let private assessmentAssumptions heldOut =
        [ "commit-before-reveal"
          "commit-timing-host-attested"
          "epsilon-floor-log-score"
          "brier-loss-lower-is-better"
          "raw-score-not-answer"
          "simplex-validated"
          if heldOut then
              "held-out-calibration-update"
          else
              "in-sample-no-calibration-update" ]
        |> Set.ofList

    let private checkEpsilon (input: ScoreInput) : Result<unit, PredictionError> =
        if not (finite input.Epsilon) || input.Epsilon <= 0.0 || input.Epsilon >= 1.0 then
            Error InvalidEpsilon
        else
            Ok()

    let private checkKnownOutcome (input: ScoreInput) : Result<unit, PredictionError> =
        if Map.containsKey input.Outcome input.Forecast then
            Ok()
        else
            Error(UnknownOutcome input.Outcome)

    let private checkSealMatch (input: ScoreInput) (outcomes: string list) : Result<unit, PredictionError> =
        if
            input.Seal.Outcomes <> outcomes
            || input.Seal.Digest <> digest input.Seal.WorkId outcomes input.Forecast
        then
            Error SealMismatch
        else
            Ok()

    let private buildAssessment (input: ScoreInput) (outcomes: string list) : Assessment =
        let probability = Map.find input.Outcome input.Forecast
        let logScore = log (max probability input.Epsilon)

        let brier =
            input.Forecast
            |> Map.fold
                (fun sum outcome forecast ->
                    let miss = forecast - (if outcome = input.Outcome then 1.0 else 0.0)
                    sum + miss * miss)
                0.0

        let sharpness =
            input.Forecast |> Map.fold (fun sum _ forecast -> sum + forecast * forecast) 0.0

        let calibration: CalibrationNote =
            { Predicted = probability
              Resolved = 1.0
              HeldOut = input.HeldOut }

        { WorkId = input.Seal.WorkId
          Outcome = input.Outcome
          LogScore = logScore
          BrierScore = brier
          Epsilon = input.Epsilon
          Sharpness = sharpness
          Calibration = calibration
          CalibrationUpdateAllowed = input.HeldOut
          Assumptions = assessmentAssumptions input.HeldOut }

    let score (input: ScoreInput) =
        result {
            do! checkEpsilon input
            do! checkKnownOutcome input
            let! outcomes = validateForecast input.Forecast
            do! checkSealMatch input outcomes
            return buildAssessment input outcomes
        }

    let private checkCommitted (input: AssessmentInput) : Result<unit, PredictionError> =
        if input.CommittedBeforeStimulus then
            Ok()
        else
            Error NotCommitted

    let assess (input: AssessmentInput) =
        result {
            do! checkCommitted input
            let! seal = commit input.WorkId input.Forecast

            let scoreInput: ScoreInput =
                { Seal = seal
                  Forecast = input.Forecast
                  Outcome = input.Outcome
                  Epsilon = input.Epsilon
                  HeldOut = input.HeldOut }

            return! score scoreInput
        }
