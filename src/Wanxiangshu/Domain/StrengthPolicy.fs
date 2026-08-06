namespace Wanxiangshu.Domain

open System
open Wanxiangshu.Kernel

/// docs/proposal/strength.md STRENGTH-079：全部策略常量集中于此，禁止在实现文件中散落数字字面量。
///
/// 不新增配置文件、TOML 配置节、环境变量或用户设置。所有值都是 best-guess
/// 第一版常量（取值理由见 docs/proposal/strength.md 第二十二节）。
module StrengthPolicy =

    module Strength =

        // ── Execution ────────────────────────────────────────────────────────
        /// STRENGTH-007B/STRENGTH-101：最大预测深度 = 2 个 provider 请求。
        [<Literal>]
        let MaxDelegatedProviderRequests = 2

        /// STRENGTH-042/078 C-04：全局并发决策上限（有界并发，ARCH-009 类比）。
        [<Literal>]
        let MaxConcurrentReplicaDecisionsGlobal = 8

        /// STRENGTH-034：第一版启用角色。
        let EligibleRoles = set [ Role.Coder; Role.Inspector; Role.DevOps; Role.Meditator ]

        /// STRENGTH-013：第一版允许工具（只读闭集）。
        let AllowedTools = set [ "read"; "glob"; "grep" ]

        // ── Projection size ───────────────────────────────────────────────────
        /// STRENGTH-025：单批 canonical 字节上限。
        [<Literal>]
        let MaxDelegatedBatchBytes = 64L * 1024L

        /// STRENGTH-025：一次决策全部批次字节上限。
        [<Literal>]
        let MaxDelegatedDecisionBytes = 96L * 1024L

        // ── Timing（wall-clock 兜底；挂起与取消责任在插件侧，STRENGTH-135）──────
        let ReplicaProviderRequestDeadline = TimeSpan.FromSeconds 45.0

        let StrengthDecisionDeadline = TimeSpan.FromSeconds 75.0

        let ParkedTransformLifetime = TimeSpan.FromMinutes 10.0

        // ── Predictor ─────────────────────────────────────────────────────────
        /// STRENGTH-022：插值 Kneser-Ney 最大 order。
        [<Literal>]
        let NGramMaximumOrder = 3

        [<Literal>]
        let KneserNeyAbsoluteDiscount = 0.75

        /// STRENGTH-022 冷启动样本门槛：K1 需要 64 个角色级观测。
        [<Literal>]
        let MinimumRoleObservationsForK1 = 64L

        /// K2 需要更多观测（损失更高）。
        [<Literal>]
        let MinimumRoleObservationsForK2 = 256L

        /// STRENGTH-022：计数衰减触发量（每跨过 4096 个符号衰减一次）。
        [<Literal>]
        let CountDecayInterval = 4096L

        /// 衰减因子。
        [<Literal>]
        let CountDecayFactor = 0.5

        // ── Controller ────────────────────────────────────────────────────────
        /// STRENGTH-030：每 128 个 eligible 决策更新一次控制概率。
        [<Literal>]
        let ControllerUpdateInterval = 128L

        /// STRENGTH-030：EWMA 半衰期（单位：eligible 决策）。
        [<Literal>]
        let ControllerEwmaHalfLife = 512.0

        /// 单次最大变化。
        [<Literal>]
        let ControllerMaximumProbabilityStep = 0.01

        /// STRENGTH-027：初始纳入概率。
        [<Literal>]
        let InitialInclusionProbabilityK1 = 0.50

        [<Literal>]
        let InitialInclusionProbabilityK2 = 0.35

        /// STRENGTH-030：概率钳位（ρ 不到达 0 或 1，避免饱和）。
        [<Literal>]
        let MinimumInclusionProbabilityK1 = 0.05

        [<Literal>]
        let MaximumInclusionProbabilityK1 = 0.95

        [<Literal>]
        let MinimumInclusionProbabilityK2 = 0.05

        [<Literal>]
        let MaximumInclusionProbabilityK2 = 0.75

        // ── Normalized utility（STRENGTH-024 成本项）──────────────────────────
        [<Literal>]
        let PrimaryFastRequestValue = 1.00

        [<Literal>]
        let PrimaryDeepRequestValue = 3.00

        [<Literal>]
        let FastReplicaRequestCost = 0.15

        [<Literal>]
        let DeepReplicaRequestCost = 0.30

        [<Literal>]
        let ProjectedByteCostPerKiB = 0.003

        [<Literal>]
        let BlockingDelayCostPerSecond = 0.005

        [<Literal>]
        let IncorrectPathLossK1 = 0.35

        [<Literal>]
        let IncorrectPathLossK2 = 1.00

        /// STRENGTH-024：净价值门槛（低于此值选 K0）。
        [<Literal>]
        let MinimumPositiveDecisionValue = 0.05

        /// STRENGTH-024：K2 相对 K1 的最小独立优势。
        [<Literal>]
        let MinimumK2AdvantageOverK1 = 0.20
