namespace Wanxiangshu.Next.Domain

/// LOOP-003…005: low character-diversity loop detector.
///
/// Three exponential kernels approximate Zipf infinite history. Fixed hash
/// buckets keep memory O(1) relative to stream length. No ring buffer.
///
/// Pure: no Host, no Journal, no side effects. One fresh detector per provider
/// attempt (LOOP-005 lifecycle).
[<RequireQualifiedAccess>]
module LoopDetector =

    let K = 3
    let MinChars = 256
    let LoopThreshold = 6.0
    let HashBuckets = 4096

    let Lambda = [| 0.50000000; 0.84089642; 0.98922801 |]
    let Coef = [| 0.42375922; 0.29672805; 0.27951273 |]

    let Epsilon = 1e-300

    [<RequireQualifiedAccess>]
    type State =
        | WarmingUp
        | Normal
        | Loop

    type Evaluation =
        { State: State
          IsLoop: bool
          EffectiveCharacterCount: float option
          Step: int }

    type Detector =
        { mutable Step: int
          Value: float[][]
          LastStep: int[]
          Total: float[]
          Cross: float[][] }

    let create () : Detector =
        { Step = 0
          Value = Array.init HashBuckets (fun _ -> Array.zeroCreate K)
          LastStep = Array.zeroCreate HashBuckets
          Total = Array.zeroCreate K
          Cross = Array.init K (fun _ -> Array.zeroCreate K) }

    /// Stable bucket for one Unicode code unit. DJB2-style; collisions only make
    /// N_eff slightly lower (more sensitive) — LOOP-005 accepts that bias.
    let bucketOf (c: char) : int =
        let mutable h = 5381u

        h <- ((h <<< 5) + h) ^^^ uint32 (int c)
        int (h % uint32 HashBuckets)

    let private materialize (detector: Detector) (bucket: int) : float[] =
        let elapsed = detector.Step - detector.LastStep.[bucket]

        if elapsed > 0 then
            let values = detector.Value.[bucket]

            for j = 0 to K - 1 do
                values.[j] <- values.[j] * (Lambda.[j] ** float elapsed)

            detector.LastStep.[bucket] <- detector.Step

        detector.Value.[bucket]

    let evaluate (detector: Detector) : Evaluation =
        if detector.Step < MinChars then
            { State = State.WarmingUp
              IsLoop = false
              EffectiveCharacterCount = None
              Step = detector.Step }
        else
            let mutable totalWeight = 0.0

            for j = 0 to K - 1 do
                totalWeight <- totalWeight + Coef.[j] * detector.Total.[j]

            let mutable squared = 0.0

            for j = 0 to K - 1 do
                for k = 0 to K - 1 do
                    squared <- squared + Coef.[j] * Coef.[k] * detector.Cross.[j].[k]

            let q = if squared < Epsilon then Epsilon else squared
            let effective = (totalWeight * totalWeight) / q
            let isLoop = effective < LoopThreshold

            { State = if isLoop then State.Loop else State.Normal
              IsLoop = isLoop
              EffectiveCharacterCount = Some effective
              Step = detector.Step }

    let pushCharacter (detector: Detector) (c: char) : Evaluation =
        let bucket = bucketOf c
        let old = materialize detector bucket |> Array.copy

        for j = 0 to K - 1 do
            for k = 0 to K - 1 do
                detector.Cross.[j].[k] <-
                    Lambda.[j] * Lambda.[k] * detector.Cross.[j].[k]
                    + Lambda.[j] * old.[j]
                    + Lambda.[k] * old.[k]
                    + 1.0

        for j = 0 to K - 1 do
            detector.Total.[j] <- Lambda.[j] * detector.Total.[j] + 1.0
            detector.Value.[bucket].[j] <- Lambda.[j] * old.[j] + 1.0

        detector.Step <- detector.Step + 1
        detector.LastStep.[bucket] <- detector.Step
        evaluate detector

    /// Push every UTF-16 code unit. Whitespace and punctuation count (LOOP-004).
    let pushText (detector: Detector) (text: string) : Evaluation =
        if isNull text || text.Length = 0 then
            evaluate detector
        else
            let mutable latest = evaluate detector

            for c in text do
                latest <- pushCharacter detector c

            latest
