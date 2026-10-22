namespace Wanxiangshu.Execution.Session

open System
open Fable.Core
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
module LoopDetector =

    module private RepositoryEnvelope =
        [<Import("encode", "#wanxiangshu-loop-detector-envelope")>]
        let encode (_text: string) : int[] = jsNative

        [<Import("vocabularySize", "#wanxiangshu-loop-detector-envelope")>]
        let vocabularySize: int = jsNative

        [<Import("halfLife", "#wanxiangshu-loop-detector-envelope")>]
        let halfLife: float = jsNative

        [<Import("lambda", "#wanxiangshu-loop-detector-envelope")>]
        let lambda: float = jsNative

        [<Import("normalWeightedDistinctCount", "#wanxiangshu-loop-detector-envelope")>]
        let normalWeightedDistinctCount: float = jsNative

        [<Import("centralProbability", "#wanxiangshu-loop-detector-envelope")>]
        let centralProbability: float = jsNative

        [<Import("lowerQuantileProbability", "#wanxiangshu-loop-detector-envelope")>]
        let lowerQuantileProbability: float = jsNative

        [<Import("upperQuantileProbability", "#wanxiangshu-loop-detector-envelope")>]
        let upperQuantileProbability: float = jsNative

        [<Import("minimumWeightedDistinctCount", "#wanxiangshu-loop-detector-envelope")>]
        let minimumWeightedDistinctCount: float = jsNative

        [<Import("maximumWeightedDistinctCount", "#wanxiangshu-loop-detector-envelope")>]
        let maximumWeightedDistinctCount: float = jsNative

    let TokenVocabularySize = RepositoryEnvelope.vocabularySize
    let HalfLife = RepositoryEnvelope.halfLife
    let Lambda = RepositoryEnvelope.lambda
    let NormalWeightedDistinctCount = RepositoryEnvelope.normalWeightedDistinctCount
    let CentralProbability = RepositoryEnvelope.centralProbability
    let LowerQuantileProbability = RepositoryEnvelope.lowerQuantileProbability
    let UpperQuantileProbability = RepositoryEnvelope.upperQuantileProbability
    let MinimumWeightedDistinctCount = RepositoryEnvelope.minimumWeightedDistinctCount
    let MaximumWeightedDistinctCount = RepositoryEnvelope.maximumWeightedDistinctCount

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

    type DegenerationEnvelope =
        {| vocabularySize: int
           lambda: float
           normalWeightedDistinctCount: float
           minimumWeightedDistinctCount: float
           maximumWeightedDistinctCount: float |}

    [<AllowNullLiteral>]
    type DegenerationEvaluation =
        abstract state: string
        abstract isAnomalous: bool
        abstract weightedDistinctTokens: float
        abstract step: int

    [<AllowNullLiteral>]
    type DegenerationDetector =
        abstract push: tokens: int[] -> DegenerationEvaluation
        abstract evaluate: unit -> DegenerationEvaluation
        abstract trackedTokenCount: unit -> int

    [<AllowNullLiteral>]
    type DegenerationMessages =
        abstract detector: unit -> DegenerationDetector
        abstract classify: weightedDistinctTokens: float -> string

    [<Import("Degeneration", "wanxiangli/degeneration")>]
    let private createDegeneration: DegenerationEnvelope -> DegenerationMessages =
        jsNative

    let private degeneration =
        createDegeneration
            {| vocabularySize = TokenVocabularySize
               lambda = Lambda
               normalWeightedDistinctCount = NormalWeightedDistinctCount
               minimumWeightedDistinctCount = MinimumWeightedDistinctCount
               maximumWeightedDistinctCount = MaximumWeightedDistinctCount |}

    let private toState (state: string) : State =
        match state with
        | "TooRepetitive" -> State.TooRepetitive
        | "TooRandom" -> State.TooRandom
        | _ -> State.Normal

    let private toEvaluation (evaluation: DegenerationEvaluation) : Evaluation =
        { State = toState evaluation.state
          IsAnomalous = evaluation.isAnomalous
          WeightedDistinctTokenCount = evaluation.weightedDistinctTokens
          Step = evaluation.step }

    /// DSL-state-combination: physical — one wanxiangli/degeneration detector
    /// (bounded token-decay scratch) plus the evaluation observed when this
    /// record was produced. A Detector stored by LoopSensor is one physical
    /// per-attempt cache entry and is never durable business state.
    type Detector =
        { Engine: DegenerationDetector
          Observed: Evaluation }

    /// The library rejects non-finite counts; the host surface keeps its
    /// historical answers for them (NaN compares false both ways, infinities
    /// fall outside the matching bound) so JS callers see no new exception.
    let classify (weightedDistinctTokenCount: float) : State =
        if Double.IsNaN weightedDistinctTokenCount then
            State.Normal
        elif Double.IsPositiveInfinity weightedDistinctTokenCount then
            State.TooRandom
        elif Double.IsNegativeInfinity weightedDistinctTokenCount then
            State.TooRepetitive
        else
            toState (degeneration.classify weightedDistinctTokenCount)

    let create () : Detector =
        let engine = degeneration.detector ()

        { Engine = engine
          Observed = toEvaluation (engine.evaluate ()) }

    let evaluate (detector: Detector) : Evaluation = detector.Observed

    let trackedTokenCount (detector: Detector) = detector.Engine.trackedTokenCount ()

    let pushText (detector: Detector) (text: string) : Detector * Evaluation =
        if String.IsNullOrEmpty text then
            detector, detector.Observed
        else
            let observed = toEvaluation (detector.Engine.push (RepositoryEnvelope.encode text))
            let updated = { detector with Observed = observed }
            updated, observed
