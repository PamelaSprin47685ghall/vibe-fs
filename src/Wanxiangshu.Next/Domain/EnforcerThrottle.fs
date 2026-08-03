namespace Wanxiangshu.Next.Domain

/// spec/15 ENFORCER-080…090：Enforcement Throttle。
///
/// 对每个 (MainSessionId, PrefixEpochId, NudgeKey) 独立计算。唯一时间尺度
/// 参数 τ = ThrottleTauObservations = 4.0（EnforcementObservationOrdinal 单位）。
/// 公式（ENFORCER-083/084）：
///   x_n = s_n / 9
///   ρ = e^{-1/τ}
///   E_n = ρ·E_{n-1} + x_n
///   P_n = (1 + t_n/τ) · E_n          （t_n = 距上次消费的 observation 数）
///   trigger ⟺ P_n ≥ 1
///
/// 触发被 NudgeConsumed 后：E ← 0，n_last ← n（ENFORCER-085）。
module EnforcerThrottle =

    /// ENFORCER-081：每 (NudgeKey) 的状态。
    type ThrottleState =
        { Evidence: float
          EvidenceOrdinal: int64
          LastTriggerOrdinal: int64 }

    /// ENFORCER-082：唯一策略参数（τ = 4）。
    /// 普通 let 而非 [<Literal>]：Fable 会把 [<Literal>] 内联掉而不导出，
    /// facade 与测试需要按名字读取它（与 FALLBACK-005 的预算同一手法）。
    let ThrottleTauObservations = 4.0

    /// ENFORCER-083：ρ = e^{-1/τ}。
    let decay (tau: float) : float = exp (-1.0 / tau)

    /// ENFORCER-083：归一化观测值 x = s/9。
    let normalizedObservation (score: byte) : float = float score / 9.0

    /// ENFORCER-081：epoch 起点 = 一次"零证据虚拟触发"（无需 NeverIssued 特例）。
    let epochStart (epochStartOrdinal: int64) : ThrottleState =
        { Evidence = 0.0
          EvidenceOrdinal = epochStartOrdinal
          LastTriggerOrdinal = epochStartOrdinal }

    /// ENFORCER-083/084：收到一份评分 s_n（当前 ordinal n）后的状态与压力。
    ///
    /// 返回 (更新后的 Evidence, 平滑压力 P_n)。压力 = (1 + t/τ)·E。
    /// t_n = n - LastTriggerOrdinal。
    let observe (tau: float) (state: ThrottleState) (score: byte) (observationOrdinal: int64) : ThrottleState * float =
        let rho = decay tau
        let x = normalizedObservation score
        let evidence = rho * state.Evidence + x

        let sinceConsumed = float (observationOrdinal - state.LastTriggerOrdinal)
        let pressure = (1.0 + sinceConsumed / tau) * evidence

        { state with
            Evidence = evidence
            EvidenceOrdinal = observationOrdinal },
        pressure

    /// ENFORCER-084：触发判据。
    let shouldTrigger (pressure: float) : bool = pressure >= 1.0

    /// ENFORCER-085：NudgeConsumed 后重置（唯一重置条件）。
    let consume (state: ThrottleState) (observationOrdinal: int64) : ThrottleState =
        { state with
            Evidence = 0.0
            LastTriggerOrdinal = observationOrdinal }

    /// ENFORCER-086：对固定证据状态，压力关于距上次消费的时间单调递增。
    let pressureAt (tau: float) (evidence: float) (sinceConsumed: float) : float =
        (1.0 + sinceConsumed / tau) * evidence

    /// ENFORCER-087：持续固定 s>0 时 E → s/9 / (1-ρ) > 0，必然触发。
    let steadyEvidence (tau: float) (score: byte) : float =
        let rho = decay tau
        normalizedObservation score / (1.0 - rho)

    /// ENFORCER-088：孤立旧报告不会自行复活。
    /// 单次低分后每轮为零：P(t) = C·(1 + t/τ)·e^{-t/τ}，不随陈旧增长。
    let isolatedPressure (tau: float) (initialEvidence: float) (elapsed: float) : float =
        let rho = decay tau
        // evidence 衰减到 rho^elapsed · initial（每 ordinal 一次 e^{-1/τ} 乘性衰减）
        let decayed = rho ** elapsed * initialEvidence
        (1.0 + elapsed / tau) * decayed
