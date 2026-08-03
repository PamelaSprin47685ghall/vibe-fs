// ⑧ 决策与综合（设计目录）：消费 findings 产决策表。组织行动，不替代事实验证（§13.2⑧）。
// 编译顺序：17（依赖 Boundary、Meditation）。
module Meditator.Methods.Synthesis

open Meditator.Boundary
open Meditator.Meditation

/// H-exist（∃-intro）：constructive_method。无 witness 的"构造完成"无法表示。
/// witness 由程序集内权柄签发（P0-1：不接收外部权柄）。
let construct
    (materials: 'm)
    (build: 'm -> Meditation<'o>)
    (witness: 'o -> Meditation<VerifierWitness list>)
    : Meditation<Result<Constructed<'o>, 'o>> =
    meditation {
        let! artifact = build materials
        let! witnesses = witness artifact

        match Constructed.create Verifiers.deterministicCheck witnesses artifact with
        | Ok constructed -> return Ok constructed
        | Error _ -> return Error artifact
    }

/// tradeoff_analysis：选项 × 约束 × 代价。欠债 riskAnalysis（§14.2，派生轨）。
// val compareOptions : Option list -> Constraint list -> CostDimension list -> Meditation<ComparisonMatrix>

/// risk_analysis：失败模式/爆炸半径/缓解。可完成决策报告，不能证明事实（§14.1）。
// val analyzeRisk : RiskOracle -> ... -> ProposedChange -> Meditation<FailureMode list * Mitigation list>

/// working_backwards：从目标推导必要条件链。
// val derivePrerequisites : DesiredEndState -> Meditation<PrerequisiteChain>

/// analysis_synthesis：后向分析 + 前向构造。
// val analyzeThenSynthesize : TargetResult -> KnownFact list -> Meditation<SynthesisPlan>
