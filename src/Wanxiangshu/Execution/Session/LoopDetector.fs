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

        [<Import("distributionMean", "#wanxiangshu-loop-detector-calibration")>]
        let distributionMean: float = jsNative

        [<Import("distributionVariance", "#wanxiangshu-loop-detector-calibration")>]
        let distributionVariance: float = jsNative

        [<Import("distributionStd", "#wanxiangshu-loop-detector-calibration")>]
        let distributionStd: float = jsNative

        [<Import("betaAlpha", "#wanxiangshu-loop-detector-calibration")>]
        let betaAlpha: float = jsNative

        [<Import("betaBeta", "#wanxiangshu-loop-detector-calibration")>]
        let betaBeta: float = jsNative

        [<Import("confidenceLevel", "#wanxiangshu-loop-detector-calibration")>]
        let confidenceLevel: float = jsNative

        [<Import("confidenceQuantile", "#wanxiangshu-loop-detector-calibration")>]
        let confidenceQuantile: float = jsNative

        [<Import("betaQuantileU", "#wanxiangshu-loop-detector-calibration")>]
        let betaQuantileU: float = jsNative

        [<Import("normalWeightedDistinctCount", "#wanxiangshu-loop-detector-calibration")>]
        let normalWeightedDistinctCount: float = jsNative

        [<Import("theoreticalLoopWeightedDistinctCount", "#wanxiangshu-loop-detector-calibration")>]
        let theoreticalLoopWeightedDistinctCount: float = jsNative

        [<Import("loopWeightedDistinctThreshold", "#wanxiangshu-loop-detector-calibration")>]
        let loopWeightedDistinctThreshold: float = jsNative

    let TokenVocabularySize = Calibration.vocabularySize
    let HalfLife = Calibration.halfLife
    let Lambda = Calibration.lambda

    let MaxSupport = Calibration.maxSupport
    let DistributionMean = Calibration.distributionMean
    let DistributionVariance = Calibration.distributionVariance
    let DistributionStd = Calibration.distributionStd
    let BetaAlpha = Calibration.betaAlpha
    let BetaBeta = Calibration.betaBeta
    let ConfidenceLevel = Calibration.confidenceLevel
    let ConfidenceQuantile = Calibration.confidenceQuantile
    let BetaQuantileU = Calibration.betaQuantileU

    let NormalWeightedDistinctCount = Calibration.normalWeightedDistinctCount

    let TheoreticalLoopWeightedDistinctCount =
        Calibration.theoreticalLoopWeightedDistinctCount

    let LoopWeightedDistinctThreshold = Calibration.loopWeightedDistinctThreshold

    [<RequireQualifiedAccess>]
    type State =
        | Normal
        | Loop

    type Evaluation =
        { State: State
          IsLoop: bool
          WeightedDistinctTokenCount: float
          Step: int }

    /// DSL-state-combination: physical — Detector is pure data: Step and
    /// WeightedDistinctTokenCount are immutable fold state threaded through
    /// pushText. LastSeenTokenStep is algorithm scratch (token decay memory,
    /// mutated in place as the only physical resource). A Detector stored in
    /// the LoopSensor detectors Dictionary is a physical cache entry.
    type Detector =
        { Step: int
          WeightedDistinctTokenCount: float
          LastSeenTokenStep: Dictionary<int, int> }

    let create () : Detector =
        { Step = 0
          WeightedDistinctTokenCount = NormalWeightedDistinctCount
          LastSeenTokenStep = Dictionary<int, int>() }

    let evaluate (detector: Detector) : Evaluation =
        let isLoop = detector.WeightedDistinctTokenCount <= LoopWeightedDistinctThreshold

        { State = if isLoop then State.Loop else State.Normal
          IsLoop = isLoop
          WeightedDistinctTokenCount = detector.WeightedDistinctTokenCount
          Step = detector.Step }

    /// Pure fold over one token. Returns the next Detector; only LastSeenTokenStep
    /// (algorithm scratch) is mutated in place.
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

    /// Pure fold: returns the updated Detector and its Evaluation without
    /// in-place mutation of Step or WeightedDistinctTokenCount.
    let pushText (detector: Detector) (text: string) : Detector * Evaluation =
        let updated =
            if String.IsNullOrEmpty text then
                detector
            else
                pushTokens detector text

        updated, evaluate updated
