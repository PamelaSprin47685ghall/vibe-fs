namespace Wanxiangshu.Domain

/// LOOP-003…005: low 4-gram-diversity loop detector.
///
/// Drop space/tab/CR/LF, then sliding 4-grams + three slow exponential kernels
/// + normal-code prior (innocent until proven looping). Fixed hash buckets keep
/// memory O(1). Physical threshold lives in N_eff space.
///
/// Pure: no Host, no Journal, no side effects. One fresh detector per provider
/// attempt (LOOP-005 lifecycle).
[<RequireQualifiedAccess>]
module LoopDetector =

    let K = 3
    let NgramSize = 4
    let HashBuckets = 4096

    /// Half-lives 8 / 64 / 512 (filtered 4-grams): λ = 2^(-1/half_life).
    let Lambda = [| 0.9170040432; 0.9892280132; 0.9986471129 |]
    let Coef = [| 0.15; 0.25; 0.60 |]

    /// Normal-code prior after ignoring whitespace: N_eff = 256 ⇒ HHI = 1/256.
    let NormalEffectiveCount = 256.0
    let NormalHhi = 1.0 / NormalEffectiveCount

    /// Typical short-loop garbage baseline: ~24 non-whitespace chars.
    let GarbageEffectiveCount = 24.0

    /// Half-garbage midpoint in N_eff space: (256 + 24) / 2.
    let LoopEffectiveThreshold = (NormalEffectiveCount + GarbageEffectiveCount) / 2.0
    let LoopHhi = 1.0 / LoopEffectiveThreshold

    let Epsilon = 1e-300

    let isIgnored (ch: char) =
        ch = ' ' || ch = '\t' || ch = '\r' || ch = '\n'

    [<RequireQualifiedAccess>]
    type State =
        | Normal
        | Loop

    type Evaluation =
        {
            State: State
            IsLoop: bool
            /// N_eff of the mixed 4-gram distribution.
            EffectiveCharacterCount: float
            /// HHI = 1 / N_eff.
            Hhi: float
            /// Number of 4-grams processed (not raw characters).
            Step: int
        }

    type Detector =
        { mutable Step: int
          Prefix: char[]
          mutable PrefixLength: int
          Value: float[][]
          LastStep: int[]
          Total: float[]
          Cross: float[][] }

    let private steadyTotals () : float[] =
        Array.init K (fun j -> 1.0 / (1.0 - Lambda.[j]))

    let private priorCross (totals: float[]) : float[][] =
        Array.init K (fun j -> Array.init K (fun k -> NormalHhi * totals.[j] * totals.[k]))

    let create () : Detector =
        let totals = steadyTotals ()

        { Step = 0
          Prefix = Array.zeroCreate NgramSize
          PrefixLength = 0
          Value = Array.init HashBuckets (fun _ -> Array.zeroCreate K)
          LastStep = Array.zeroCreate HashBuckets
          Total = totals
          Cross = priorCross totals }

    /// Stable bucket for one 4-gram (four Unicode code units).
    let bucketOfGram (a: char) (b: char) (c: char) (d: char) : int =
        let mutable h = 5381u
        h <- ((h <<< 5) + h) ^^^ uint32 (int a)
        h <- ((h <<< 5) + h) ^^^ uint32 (int b)
        h <- ((h <<< 5) + h) ^^^ uint32 (int c)
        h <- ((h <<< 5) + h) ^^^ uint32 (int d)
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
        let mutable totalWeight = 0.0

        for j = 0 to K - 1 do
            totalWeight <- totalWeight + Coef.[j] * detector.Total.[j]

        let mutable squared = 0.0

        for j = 0 to K - 1 do
            for k = 0 to K - 1 do
                squared <- squared + Coef.[j] * Coef.[k] * detector.Cross.[j].[k]

        let z2 = totalWeight * totalWeight

        let hhi =
            if z2 <= Epsilon then
                NormalHhi
            else
                max (squared / z2) Epsilon

        let effective = 1.0 / hhi
        // Physical gate is N_eff (LOOP-003); HHI form is the reciprocal rewrite.
        let isLoop = effective <= LoopEffectiveThreshold

        { State = if isLoop then State.Loop else State.Normal
          IsLoop = isLoop
          EffectiveCharacterCount = effective
          Hhi = hhi
          Step = detector.Step }

    let private updateGram (detector: Detector) (bucket: int) : unit =
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

    let pushCharacter (detector: Detector) (ch: char) : Evaluation =
        if isIgnored ch then
            evaluate detector
        else
            if detector.PrefixLength < NgramSize then
                detector.Prefix.[detector.PrefixLength] <- ch
                detector.PrefixLength <- detector.PrefixLength + 1
            else
                // Slide: drop oldest, shift left, append.
                for i = 0 to NgramSize - 2 do
                    detector.Prefix.[i] <- detector.Prefix.[i + 1]

                detector.Prefix.[NgramSize - 1] <- ch

            if detector.PrefixLength < NgramSize then
                // Innocent prior until the first filtered 4-gram exists.
                { State = State.Normal
                  IsLoop = false
                  EffectiveCharacterCount = NormalEffectiveCount
                  Hhi = NormalHhi
                  Step = detector.Step }
            else
                let bucket =
                    bucketOfGram detector.Prefix.[0] detector.Prefix.[1] detector.Prefix.[2] detector.Prefix.[3]

                updateGram detector bucket
                evaluate detector

    /// Push UTF-16 code units. Space/tab/CR/LF are ignored (LOOP-003/004).
    let pushText (detector: Detector) (text: string) : Evaluation =
        if isNull text || text.Length = 0 then
            evaluate detector
        else
            let mutable latest = evaluate detector

            for c in text do
                latest <- pushCharacter detector c

            latest
