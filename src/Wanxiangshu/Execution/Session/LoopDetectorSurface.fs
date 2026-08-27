namespace Wanxiangshu.Execution.Session

open Wanxiangshu.Foundation.Identity

/// JavaScript boundary for the degeneration detector and text-delta semantics.
[<RequireQualifiedAccess>]
module LoopDetectorSurface =
    type private DetectorHandle(detector: LoopDetector.Detector) =
        // DSL-MUTABLE: resource — JS boundary holder for opaque detector state
        let mutable current = detector
        member _.Detector = current
        member _.Replace(d: LoopDetector.Detector) = current <- d

    let private stateName (state: LoopDetector.State) =
        match state with
        | LoopDetector.State.Normal -> "Normal"
        | LoopDetector.State.TooRepetitive -> "TooRepetitive"
        | LoopDetector.State.TooRandom -> "TooRandom"

    let private evaluationView (evaluation: LoopDetector.Evaluation) : obj =
        box
            {| state = stateName evaluation.State
               isAnomalous = evaluation.IsAnomalous
               weightedDistinctTokens = evaluation.WeightedDistinctTokenCount
               step = evaluation.Step |}

    let vocabularySize = LoopDetector.TokenVocabularySize
    let halfLife = LoopDetector.HalfLife
    let lambda = LoopDetector.Lambda
    let normalWeightedDistinctCount = LoopDetector.NormalWeightedDistinctCount
    let centralProbability = LoopDetector.CentralProbability
    let lowerQuantileProbability = LoopDetector.LowerQuantileProbability
    let upperQuantileProbability = LoopDetector.UpperQuantileProbability
    let minimumWeightedDistinctCount = LoopDetector.MinimumWeightedDistinctCount
    let maximumWeightedDistinctCount = LoopDetector.MaximumWeightedDistinctCount

    let constants () : obj =
        box
            {| vocabularySize = LoopDetector.TokenVocabularySize
               halfLife = LoopDetector.HalfLife
               lambda = LoopDetector.Lambda
               normalWeightedDistinctCount = LoopDetector.NormalWeightedDistinctCount
               centralProbability = LoopDetector.CentralProbability
               lowerQuantileProbability = LoopDetector.LowerQuantileProbability
               upperQuantileProbability = LoopDetector.UpperQuantileProbability
               minimumWeightedDistinctCount = LoopDetector.MinimumWeightedDistinctCount
               maximumWeightedDistinctCount = LoopDetector.MaximumWeightedDistinctCount |}

    let classify (weightedDistinctTokens: float) : string =
        LoopDetector.classify weightedDistinctTokens |> stateName

    let create () : obj =
        box (DetectorHandle(LoopDetector.create ()))

    let trackedTokenCount (handle: obj) : int =
        (handle :?> DetectorHandle).Detector.LastSeenTokenStep.Count

    let evaluate (handle: obj) : obj =
        let detector = (handle :?> DetectorHandle).Detector
        LoopDetector.evaluate detector |> evaluationView

    let pushText (handle: obj) (text: string) : obj =
        let handle = (handle :?> DetectorHandle)
        let updated, evaluation = LoopDetector.pushText handle.Detector text
        handle.Replace updated
        evaluationView evaluation

    let isLoopTextDelta (raw: obj) : bool =
        Wanxiangshu.OpenCode.LoopEventCodec.isLoopTextDelta raw

    let tryDecodeTextDelta (raw: obj) : obj =
        match Wanxiangshu.OpenCode.LoopEventCodec.tryDecodeTextDelta raw with
        | None -> null
        | Some(delta: Wanxiangshu.OpenCode.LoopEventCodec.TextDelta) ->
            box
                {| sessionId = SessionId.value delta.SessionId
                   messageId = delta.MessageId
                   partId = delta.PartId
                   field = delta.Field
                   delta = delta.Delta |}
