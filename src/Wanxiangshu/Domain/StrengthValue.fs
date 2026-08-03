namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel
open Wanxiangshu.Domain.StrengthTypes

/// spec/14 STRENGTH-024/025：价值函数与决策规则。
///
/// V0 = 0；V1/V2 按净价值选择。K 由候选集 + MinimumPositiveDecisionValue 门槛决定。
/// Z_X 无恢复成本项（STRENGTH-014：恢复即丢弃决策）；无独立 Companion。
module StrengthValue =

    /// STRENGTH-024：成本模型输入。主模型与 Replica 的价格配置。
    type CostModel =
        {
            /// 主模型单请求成本（归一化价值）。
            PrimaryRequestValue: float
            /// Replica 单请求成本。
            ReplicaRequestCost: float
            /// 投影字节成本（每 KiB）。
            ProjectedByteCostPerKiB: float
            /// 阻塞延迟成本（每秒）。
            BlockingDelayCostPerSecond: float
            /// 错误调查方向风险损失。
            IncorrectPathLoss: float
        }

    /// 默认成本模型（来自 PolicyConstants，STRENGTH-079）。
    let defaultCostModel (tier: AgentTier) : CostModel =
        match tier with
        | AgentTier.Fast ->
            { PrimaryRequestValue = StrengthPolicy.Strength.PrimaryFastRequestValue
              ReplicaRequestCost = StrengthPolicy.Strength.FastReplicaRequestCost
              ProjectedByteCostPerKiB = StrengthPolicy.Strength.ProjectedByteCostPerKiB
              BlockingDelayCostPerSecond = StrengthPolicy.Strength.BlockingDelayCostPerSecond
              IncorrectPathLoss = StrengthPolicy.Strength.IncorrectPathLossK1 }
        | AgentTier.Deep ->
            { PrimaryRequestValue = StrengthPolicy.Strength.PrimaryDeepRequestValue
              ReplicaRequestCost = StrengthPolicy.Strength.DeepReplicaRequestCost
              ProjectedByteCostPerKiB = StrengthPolicy.Strength.ProjectedByteCostPerKiB
              BlockingDelayCostPerSecond = StrengthPolicy.Strength.BlockingDelayCostPerSecond
              IncorrectPathLoss = StrengthPolicy.Strength.IncorrectPathLossK1 }

    /// STRENGTH-024：V1。
    ///
    /// V1 = P(read1) × SavedPrimaryCost1
    ///     - ReplicaProviderCost1
    ///     - ExpectedProjectedBytesCost1
    ///     - BlockingDelayCost1
    ///     - SteeringRisk1
    let valueK1 (cost: CostModel) (p1: float) (expectedBytes1: int64) (expectedDelay1: float) : float =
        let saved = p1 * cost.PrimaryRequestValue
        let replicaCost = cost.ReplicaRequestCost
        let byteCost = float expectedBytes1 / 1024.0 * cost.ProjectedByteCostPerKiB
        let delayCost = expectedDelay1 * cost.BlockingDelayCostPerSecond
        let steeringRisk = cost.IncorrectPathLoss
        saved - replicaCost - byteCost - delayCost - steeringRisk

    /// STRENGTH-024：V2。
    ///
    /// V2 = P(read1) × SavedPrimaryCost1
    ///     + P(read1∧read2) × SavedPrimaryCost2
    ///     - ExpectedReplicaTotalProviderCost
    ///     - ExpectedProjectedBytesCost1And2
    ///     - BlockingDelayCost1And2
    ///     - SteeringRisk1 - SteeringRisk2
    let valueK2
        (cost: CostModel)
        (p1: float)
        (p2: float)
        (expectedBytes1: int64)
        (expectedBytes2: int64)
        (expectedDelay1: float)
        (expectedDelay2: float)
        : float =
        let saved1 = p1 * cost.PrimaryRequestValue
        let saved2 = p2 * cost.PrimaryRequestValue
        let replicaTotal = cost.ReplicaRequestCost * 2.0

        let byteCost =
            float (expectedBytes1 + expectedBytes2) / 1024.0 * cost.ProjectedByteCostPerKiB

        let delayCost = (expectedDelay1 + expectedDelay2) * cost.BlockingDelayCostPerSecond
        let steeringRisk1 = cost.IncorrectPathLoss
        // STRENGTH-024：Risk2 必须高于单步风险——第二步建立在 Replica 调查方向之上。
        let steeringRisk2 = StrengthPolicy.Strength.IncorrectPathLossK2

        saved1 + saved2
        - replicaTotal
        - byteCost
        - delayCost
        - steeringRisk1
        - steeringRisk2

    /// STRENGTH-024：决策规则。
    ///
    /// 候选集初始 {K0}；V1 ≥ MinimumPositiveDecisionValue → K1 ∈ 候选；
    /// V2 ≥ MinimumPositiveDecisionValue 且 V2-V1 ≥ MinimumK2AdvantageOverK1 → K2 ∈ 候选。
    /// K = 候选集中 V 最大者；并列取较小 K。
    let chooseBudget (v0: float) (v1: float) (v2: float) : StrengthBudget =
        let minPositive = StrengthPolicy.Strength.MinimumPositiveDecisionValue
        let minK2Advantage = StrengthPolicy.Strength.MinimumK2AdvantageOverK1

        let k1Eligible = v1 >= minPositive
        let k2Eligible = v2 >= minPositive && v2 - v1 >= minK2Advantage

        if k2Eligible then
            if v2 >= v1 then StrengthBudget.K2 else StrengthBudget.K1
        elif k1Eligible then
            StrengthBudget.K1
        else
            StrengthBudget.K0

    /// STRENGTH-025：固定输入合同。单批超过上限 → 该批次不得提交。
    let batchWithinByteLimit (batchBytes: int64) : bool =
        batchBytes <= StrengthPolicy.Strength.MaxDelegatedBatchBytes

    /// STRENGTH-025：一次决策全部批次字节 ≤ 决策上限。
    let decisionWithinByteLimit (totalBytes: int64) : bool =
        totalBytes <= StrengthPolicy.Strength.MaxDelegatedDecisionBytes
