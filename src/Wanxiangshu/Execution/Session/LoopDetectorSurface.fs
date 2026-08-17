namespace Wanxiangshu.Execution.Session

open Wanxiangshu.Foundation.Identity

/// JavaScript boundary for loop-detector and text-delta semantics.
/// Detector state remains an opaque production capability; observations are plain records.
[<RequireQualifiedAccess>]
module LoopDetectorSurface =
    type private DetectorHandle(detector: LoopDetector.Detector) =
        member _.Detector = detector

    let private stateName (state: LoopDetector.State) =
        match state with
        | LoopDetector.State.Normal -> "Normal"
        | LoopDetector.State.Loop -> "Loop"

    let private evaluationView (evaluation: LoopDetector.Evaluation) : obj =
        box
            {| state = stateName evaluation.State
               isLoop = evaluation.IsLoop
               weightedDistinctTokens = evaluation.WeightedDistinctTokenCount
               step = evaluation.Step |}

    let vocabularySize = LoopDetector.TokenVocabularySize
    let halfLife = LoopDetector.HalfLife
    let lambda = LoopDetector.Lambda
    let normalWeightedDistinctCount = LoopDetector.NormalWeightedDistinctCount

    let theoreticalLoopWeightedDistinctCount =
        LoopDetector.TheoreticalLoopWeightedDistinctCount

    let loopWeightedDistinctThreshold = LoopDetector.LoopWeightedDistinctThreshold

    let constants () : obj =
        box
            {| vocabularySize = LoopDetector.TokenVocabularySize
               halfLife = LoopDetector.HalfLife
               lambda = LoopDetector.Lambda
               normalWeightedDistinctCount = LoopDetector.NormalWeightedDistinctCount
               theoreticalLoopWeightedDistinctCount = LoopDetector.TheoreticalLoopWeightedDistinctCount
               loopWeightedDistinctThreshold = LoopDetector.LoopWeightedDistinctThreshold |}

    let create () : obj =
        box (DetectorHandle(LoopDetector.create ()))

    let trackedTokenCount (handle: obj) : int =
        (handle :?> DetectorHandle).Detector.LastSeenTokenStep.Count

    let evaluate (handle: obj) : obj =
        let detector = (handle :?> DetectorHandle).Detector
        LoopDetector.evaluate detector |> evaluationView

    let pushText (handle: obj) (text: string) : obj =
        let detector = (handle :?> DetectorHandle).Detector
        LoopDetector.pushText detector text |> evaluationView

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
