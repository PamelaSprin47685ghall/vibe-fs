namespace Wanxiangshu.Execution.Session

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
module LoopDetector =

    module private TokenEncoding =
        [<Import("encode", "./GptTokens.js")>]
        let encode (_text: string) : int[] = jsNative

    module private Calibration =
        [<Import("vocabularySize", "#wanxiangshu-loop-detector-calibration")>]
        let vocabularySize: int = jsNative

        [<Import("halfLife", "#wanxiangshu-loop-detector-calibration")>]
        let halfLife: float = jsNative

        [<Import("lambda", "#wanxiangshu-loop-detector-calibration")>]
        let lambda: float = jsNative

        [<Import("maxSupport", "#wanxiangshu-loop-detector-calibration")>]
        let maxSupport: float = jsNative

        [<Import("normalWeightedDistinctCount", "#wanxiangshu-loop-detector-calibration")>]
        let normalWeightedDistinctCount: float = jsNative

        [<Import("minimumWeightedDistinctCount", "#wanxiangshu-loop-detector-calibration")>]
        let minimumWeightedDistinctCount: float = jsNative

        [<Import("maximumWeightedDistinctCount", "#wanxiangshu-loop-detector-calibration")>]
        let maximumWeightedDistinctCount: float = jsNative

    let TokenVocabularySize = Calibration.vocabularySize
    let HalfLife = Calibration.halfLife
    let Lambda = Calibration.lambda
    let MaxSupport = Calibration.maxSupport
    let NormalWeightedDistinctCount = Calibration.normalWeightedDistinctCount
    let MinimumWeightedDistinctCount = Calibration.minimumWeightedDistinctCount
    let MaximumWeightedDistinctCount = Calibration.maximumWeightedDistinctCount

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

    /// DSL-state-combination: physical — immutable fold state plus bounded
    /// token-decay scratch. A Detector stored by LoopSensor is one physical
    /// per-attempt cache entry and is never durable business state.
    type Detector =
        { Step: int
          WeightedDistinctTokenCount: float
          LastSeenTokenStep: Dictionary<int, int> }

    let classify (weightedDistinctTokenCount: float) : State =
        if weightedDistinctTokenCount < MinimumWeightedDistinctCount then
            State.TooRepetitive
        elif weightedDistinctTokenCount > MaximumWeightedDistinctCount then
            State.TooRandom
        else
            State.Normal

    let create () : Detector =
        { Step = 0
          WeightedDistinctTokenCount = NormalWeightedDistinctCount
          LastSeenTokenStep = Dictionary<int, int>() }

    let evaluate (detector: Detector) : Evaluation =
        let state = classify detector.WeightedDistinctTokenCount

        { State = state
          IsAnomalous = state <> State.Normal
          WeightedDistinctTokenCount = detector.WeightedDistinctTokenCount
          Step = detector.Step }

    /// Pure fold over one token. Only LastSeenTokenStep is algorithm scratch.
    let private pushToken (detector: Detector) (token: int) : Detector =
        let step = detector.Step + 1

        let replacement =
            match detector.LastSeenTokenStep.TryGetValue token with
            | true, previousStep -> 1.0 - Math.Pow(Lambda, float (step - previousStep))
            | false, _ -> 1.0

        detector.LastSeenTokenStep.[token] <- step

        { Step = step
          WeightedDistinctTokenCount = Lambda * detector.WeightedDistinctTokenCount + replacement
          LastSeenTokenStep = detector.LastSeenTokenStep }

    let private pushTokens (detector: Detector) (text: string) : Detector =
        // DSL-MUTABLE: algorithm-scratch — fold accumulator over token stream
        let mutable acc = detector

        for token in TokenEncoding.encode text do
            acc <- pushToken acc token

        acc

    let pushText (detector: Detector) (text: string) : Detector * Evaluation =
        let updated =
            if String.IsNullOrEmpty text then
                detector
            else
                pushTokens detector text

        updated, evaluate updated
