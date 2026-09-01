namespace Wanxiangshu.Execution.Session

[<RequireQualifiedAccess>]
module LoopDetector =
    val TokenVocabularySize: int
    val HalfLife: float
    val Lambda: float
    val NormalWeightedDistinctCount: float
    val CentralProbability: float
    val LowerQuantileProbability: float
    val UpperQuantileProbability: float
    val MinimumWeightedDistinctCount: float
    val MaximumWeightedDistinctCount: float

    [<RequireQualifiedAccess>]
    type State =
        | Normal
        | TooRepetitive
        | TooRandom

    type Evaluation =
        { State: State
          IsAnomalous: bool
          WeightedDistinctTokenCount: float
          Step: int }

    type Detector

    val classify: weightedDistinctTokenCount: float -> State
    val create: unit -> Detector
    val evaluate: detector: Detector -> Evaluation
    val trackedTokenCount: detector: Detector -> int
    val pushText: detector: Detector -> text: string -> Detector * Evaluation
