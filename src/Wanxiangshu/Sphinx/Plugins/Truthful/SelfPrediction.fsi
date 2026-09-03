namespace Wanxiangshu.Sphinx.Plugins.Truthful

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

    /// Scored forecast. BrierScore is the loss form sum (f - 1_y)^2 (lower-is-better, M-4),
    /// not the App.J maximized-score form. Assumptions always carries
    /// commit-timing-host-attested: seal binds content, timing is caller-attested.
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

    val simplexTolerance: float
    val predictionErrorCode: PredictionError -> string
    val commit: workId: string -> forecast: Map<string, float> -> Result<Seal, PredictionError>
    val score: input: ScoreInput -> Result<Assessment, PredictionError>
    val assess: input: AssessmentInput -> Result<Assessment, PredictionError>
