namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Domain.StrengthTypes

/// SSOT/14 STRENGTH-027/028/030：负反馈控制器。
///
/// 两个独立反馈环（K1 与 K2，STRENGTH-031）。ρ 随预测倾向反向变化（STRENGTH-028）：
/// 预测过多 → ρ 降 → 旁路序列纳入率降 → read 信号少 → 倾向回落。
///
/// 慢更新：预测计数快速更新，纳入概率慢速更新（STRENGTH-030）。
module StrengthController =

    /// STRENGTH-027：控制器状态。`InclusionProbability1/2` 随决策冻结并持久化。
    type StrengthControllerState =
        {
            InclusionProbability1: float
            InclusionProbability2: float

            SmoothedTendency1: float
            SmoothedTendency2: float

            /// 自上次更新以来累计的 eligible 决策数。
            EligibleSinceUpdate: int64

            ControllerVersion: string
        }

    let initialState: StrengthControllerState =
        { InclusionProbability1 = StrengthPolicy.Strength.InitialInclusionProbabilityK1
          InclusionProbability2 = StrengthPolicy.Strength.InitialInclusionProbabilityK2
          SmoothedTendency1 = 0.0
          SmoothedTendency2 = 0.0
          EligibleSinceUpdate = 0L
          ControllerVersion = "strength-controller-v1" }

    /// STRENGTH-027：确定性抽样（替代随机数）。
    ///
    /// u = hashToUnitInterval(decisionId, requestOrdinal, "strength-training-inclusion-v1")
    /// included = u < frozenInclusionProbability
    ///
    /// 不使用进程级随机数或系统时间。确定性保证幂等：重放 Journal 时训练标签不变。
    /// `frozenInclusionProbability` 必须随 StrengthDecision 一起持久化。
    /// `sha256` 由调用方注入（VERIFY-008：纯领域不直接依赖 Host crypto）。
    let hashToUnitInterval (sha256: string -> string) (seed: string) : float =
        let digest = sha256 seed

        // SHA-256 摘要前 8 字节 → [0,1)。纯函数：同一 seed 永远同一结果。
        let bytes =
            [ for i in 0..7 do
                  yield System.Int32.Parse(digest.Substring(i * 2, 2), System.Globalization.NumberStyles.HexNumber) ]

        let asInt64 = bytes |> List.mapi (fun i b -> int64 b <<< (8 * (7 - i))) |> List.sum

        float (asInt64 &&& System.Int64.MaxValue) / float System.Int64.MaxValue

    /// STRENGTH-027：抽样标签。decisionId + requestOrdinal 决定相同结果。
    let includedInTraining
        (sha256: string -> string)
        (decisionId: string)
        (requestOrdinal: int)
        (frozenProbability: float)
        : bool * float =
        let u =
            hashToUnitInterval sha256 (sprintf "%s|%d|strength-training-inclusion-v1" decisionId requestOrdinal)

        u < frozenProbability, u

    /// STRENGTH-028：负反馈方向。z 上升 → ρ 下降；z 下降 → ρ 上升。
    let desiredInclusion (smoothedTendency: float) : float = 1.0 - smoothedTendency

    /// STRENGTH-030：慢更新滤波。
    ///
    /// filtered = (1 - α) * previous + α * desired
    /// next = clamp(min, max) ∘ rateLimit(previous, maxStep)
    let updateProbability
        (alpha: float)
        (minP: float)
        (maxP: float)
        (maxStep: float)
        (previous: float)
        (smoothedTendency: float)
        : float =
        let desired = desiredInclusion smoothedTendency
        let filtered = (1.0 - alpha) * previous + alpha * desired
        let clamped = max minP (min maxP filtered)
        max (previous - maxStep) (min (previous + maxStep) clamped)

    /// STRENGTH-030：EWMA 半衰期 → α。
    /// α = 1 - 0.5^(1/halfLife)
    let ewmaAlpha (halfLife: float) : float = 1.0 - 0.5 ** (1.0 / halfLife)

    /// STRENGTH-030/031：一次 eligible 决策后的控制器更新。
    ///
    /// 每 ControllerUpdateInterval 个决策更新一次概率；倾向用 EWMA 平滑。
    /// K2 环：更低上限、更慢更新、更高风险惩罚（STRENGTH-031）。
    let onEligibleDecision
        (state: StrengthControllerState)
        (rawTendency1: float)
        (rawTendency2: float)
        : StrengthControllerState =
        let alpha1 = ewmaAlpha StrengthPolicy.Strength.ControllerEwmaHalfLife
        // K2 环更慢：半衰期 ×2
        let alpha2 = ewmaAlpha (StrengthPolicy.Strength.ControllerEwmaHalfLife * 2.0)

        let smoothed1 = alpha1 * rawTendency1 + (1.0 - alpha1) * state.SmoothedTendency1
        let smoothed2 = alpha2 * rawTendency2 + (1.0 - alpha2) * state.SmoothedTendency2

        let count = state.EligibleSinceUpdate + 1L

        if count >= StrengthPolicy.Strength.ControllerUpdateInterval then
            let p1 =
                updateProbability
                    alpha1
                    StrengthPolicy.Strength.MinimumInclusionProbabilityK1
                    StrengthPolicy.Strength.MaximumInclusionProbabilityK1
                    StrengthPolicy.Strength.ControllerMaximumProbabilityStep
                    state.InclusionProbability1
                    smoothed1

            let p2 =
                updateProbability
                    alpha2
                    StrengthPolicy.Strength.MinimumInclusionProbabilityK2
                    StrengthPolicy.Strength.MaximumInclusionProbabilityK2
                    StrengthPolicy.Strength.ControllerMaximumProbabilityStep
                    state.InclusionProbability2
                    smoothed2

            { state with
                InclusionProbability1 = p1
                InclusionProbability2 = p2
                SmoothedTendency1 = smoothed1
                SmoothedTendency2 = smoothed2
                EligibleSinceUpdate = 0L }
        else
            { state with
                SmoothedTendency1 = smoothed1
                SmoothedTendency2 = smoothed2
                EligibleSinceUpdate = count }
