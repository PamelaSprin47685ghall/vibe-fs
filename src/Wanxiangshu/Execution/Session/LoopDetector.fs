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

    module private RepositoryEnvelope =
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
