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

        [<Import("vocabularySize", "./GptTokens.js")>]
        let vocabularySize: int = jsNative

    let TokenVocabularySize = TokenEncoding.vocabularySize
    let HalfLife = 64.0
    let Lambda = Math.Pow(2.0, -1.0 / HalfLife)

    let NormalWeightedDistinctCount = 19.260533967298734
    let TheoreticalLoopWeightedDistinctCount = 1.0

    let LoopWeightedDistinctThreshold =
        (NormalWeightedDistinctCount + TheoreticalLoopWeightedDistinctCount) / 2.0

    [<RequireQualifiedAccess>]
    type State =
        | Normal
        | Loop

    type Evaluation =
        { State: State
          IsLoop: bool
          WeightedDistinctTokenCount: float
          Step: int }

    /// DSL-state-combination: physical — Step and WeightedDistinctTokenCount are attempt-local transient detector counters.
    type Detector =
        { mutable Step: int
          mutable WeightedDistinctTokenCount: float
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

    let private pushToken (detector: Detector) (token: int) =
        let step = detector.Step + 1

        let replacement =
            match detector.LastSeenTokenStep.TryGetValue token with
            | true, previousStep -> 1.0 - Math.Pow(Lambda, float (step - previousStep))
            | false, _ -> 1.0

        detector.WeightedDistinctTokenCount <- Lambda * detector.WeightedDistinctTokenCount + replacement

        detector.Step <- step
        detector.LastSeenTokenStep.[token] <- step

    let pushText (detector: Detector) (text: string) : Evaluation =
        if not (String.IsNullOrEmpty text) then
            for token in TokenEncoding.encode text do
                pushToken detector token

        evaluate detector
