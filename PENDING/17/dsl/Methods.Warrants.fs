// ② 真值保持与反驳 + ③ 证据与现实检查（设计目录）：Accepted 的唯二来源（W3 推导 + W1 观察）。
// 编译顺序：13（依赖 Boundary、Ledger、Meditation、Oracle、Ensure）。
module Meditator.Methods.Warrants

open Meditator.Boundary
open Meditator.Ledger
open Meditator.Meditation

/// W3 的输入：前提必须已是 Accepted——类型即门禁（演算 T2）。
type AcceptedPremises = AcceptedPremises of (Accepted<Claim> * Warrant) list

/// 可验证推理链（P0-2）：构造 private——只能由 RuleEngine.verify 产生。
/// 调用方无法自由构造"步骤列表"冒充推理；规则由版本化 F# 代码定义（P0 固定裁决 1）。
type ValidInferenceChain = private ValidInferenceChain of ruleId: string * steps: string list

/// 版本化推理规则表（P0-2 137 版）：P0 **禁用 deduction**——规则表为空。
/// 原因：Claim 是自然语言字符串，无命题 AST，任何"步骤计数/字符串检查"都无法机器验证
/// "P→Q、P 前提、Q 结论"的真实推理（评审：仅检查步骤数量不能签发 Inference witness）。
/// 待命题 AST（结构化前提/结论）落地后按规则注册；未知规则一律 fail closed。
type InferenceRule =
    { RuleId: string
      Validate: string list -> bool }

module RuleEngine =
    let private rules: InferenceRule list = []

    /// 验证推理链：P0 无注册规则 → 恒 Error（deduction 不可用，诚实禁用而非伪验证）。
    let verify (ruleId: string) (steps: string list) : Result<ValidInferenceChain, string> =
        match rules |> List.tryFind (fun r -> r.RuleId = ruleId) with
        | None -> Error $"RuleEngine: unknown inference rule {ruleId} (P0: deduction disabled)"
        | Some r ->
            if r.Validate steps then
                Ok(ValidInferenceChain(ruleId, steps))
            else
                Error $"RuleEngine: steps do not satisfy rule {ruleId}"

/// W3 的 grade 语义（A3 锚定）：推导结论的 strength = 最弱前提的 strength
/// （grade 各维 ≤ 前提逐维 meet 由 deduce 在规则可用后保证；P0 禁用期间以函数级测试锚定）。
let derivationStrength (premises: Warrant list) : SupportStrength =
    premises
    |> List.minBy (fun w ->
        match Warrant.strength w with
        | Weak -> 0
        | Moderate -> 1
        | Strong -> 2)
    |> Warrant.strength

type FalsificationResult<'a> =
    | Survives of scope: string
    | Refuted of counterexampleWarrant: Warrant
// 无 ProvenTrue（§12.6）：幸存只表示"在此 scope 内未被此轮反例击中"。

/// W3（Deriv）：从已接受前提推出结论。
/// 输出 derivation warrant：DependencyWarrantIds = 全部前提；grade ≤ 前提逐维 meet（G-deriv）。
/// 前提来源不引入领域新知识——UltimateSourceIds 只是前提来源的并。
/// 配对验证：Accepted<Claim> 的值必须与配对的 Warrant 属于同一 claim；
/// 结论 scope 必须与前提统一 scope 一致；WarrantId 含前提、scope 与推理步骤——
/// 两条不同证明（不同前提/scope/步骤）必得不同 ID（评审：deduce 的 ID 过弱）。
/// P0-1：不再接收 EvaluatorAuthority——inference 权柄固定为程序集内 Verifiers.inference，
/// 外部调用方不能伪造推理 witness（只能提供前提与推理步骤）。
let deduce
    (ruleId: string)
    (conclusion: Claim)
    (AcceptedPremises premises)
    (chain: ValidInferenceChain)
    : Result<Warrant, string> =
    match chain with
    | ValidInferenceChain(chainRule, steps) when chainRule <> ruleId ->
        Error "W3: inference chain rule does not match ruleId"
    | ValidInferenceChain(_, steps) ->
        match premises with
        | [] -> Error "W3: at least one accepted premise required"
        | _ ->
            // 配对验证：前提的 Accepted<Claim> 与配对的 Warrant 必须指向同一 claim。
            match
                premises
                |> List.tryFind (fun (a, w) -> (Accepted.value a).Id <> Warrant.claimId w)
            with
            | Some _ -> Error "W3: accepted premise claim does not match its paired warrant"
            | None ->
                let premiseWarrants = premises |> List.map snd

                let inferenceWitness =
                    VerifierWitness.issue Verifiers.inference Inference (String.concat "\u001F" steps)

                let weakestStrength = derivationStrength premiseWarrants

                let conclusionScope =
                    premiseWarrants
                    |> List.map Warrant.scope
                    |> List.distinct
                    |> function
                        | [ scope ] -> Ok scope
                        | scopes -> Error $"W3: premises span incompatible scopes (%d{List.length scopes})"

                conclusionScope
                |> Result.bind (fun scope ->
                    if scope <> conclusion.Scope then
                        Error $"W3: conclusion scope does not match premise scope"
                    else
                        // security_review LOW：WarrantId 必须由 canonical 内容派生（warrantIdOfData）——
                        // 原 "deriv:..." 前缀 ID 与派生规则必然不等（deduce 产物会被 fold 拒）。
                        // 先构造 body，再以 ofData 派生 ID（"deriv:" 语义由 Kind=Derivation 蕴含）。
                        let body =
                            { Id = WarrantId ""
                              ClaimId = conclusion.Id
                              Polarity = Supports
                              Kind = Derivation
                              Rule = ruleId
                              Strength = weakestStrength
                              Scope = scope
                              Origin = Provenance.create "" ruleId "deduction/v1" // 时间由调用方以外部注入时钟填充；不参与 canonical identity
                              VerifierWitnesses =
                                inferenceWitness
                                :: (premises |> List.collect (fun (a, _) -> Accepted.witnesses a))
                              DependencyWarrantIds = premiseWarrants |> List.map Warrant.id
                              UltimateSourceIds = premiseWarrants |> List.collect Warrant.sources |> List.distinct
                              IntroducedBy = "" }

                        Warrant.create
                            { body with
                                Id = EventCodec.warrantIdOfData body })

/// W-refute 骨架：tryFalsify 的三结局（§16.2.6 verdict: survives/refuted/scoped）。
/// 反例经 verifier 后成为 opposing warrant（W1/W2 路径）；未命中只追加 SearchAttempted(NoHit)，
/// 不能把 claim 标记为 proven（O-cov 侧条件）。
// val tryFalsify : CounterexampleOracle -> ... -> Accepted<Claim> -> FailureCondition list
//     -> Meditation<FalsificationResult<Accepted<Claim>>>

// 其余签名（设计目录 ②③）：
//
// ② 推导源：
// val refuteByContradiction : AcceptedPremises -> AssumedNegation -> DerivationToContradiction -> Meditation<Refutation>
//     只能排除假设，不能立论
// val proveInvariant : OperationSet -> CandidateInvariant -> ObservationEvidence list -> Meditation<Accepted<Claim> option>
//     效力限于声明的操作集
// val pigeonhole : CountingArgument -> Meditation<Accepted<Claim>>
//     前提：计数与容量事实已带 warrant
// val necessaryPreconditions : UndeniableFact -> Meditation<NecessaryPremise list>
//     只推必要前提，不推结论本身
//
// ③ 观察源：
// val groundByTest : ExecutableOracle -> ... -> BehaviorClaim -> Meditation<Warrant>   // grade 限测试范围
// val traceFault : FailureSignature -> ReproductionSteps -> Meditation<FaultChain * Warrant>
// val analyzeCause : WhyChain -> Meditation<CausalVerdict>
//     CausalVerdict = Causes of Warrant | AssociatedWith of MissingCondition list（三条件门禁，§13.2③）
// val reviewTrustBoundary : SecurityOracle -> ... -> TrustBoundary -> Asset list -> Meditation<AbusePath list>
// val analyzePerformance : PerformanceOracle -> ... -> WorkloadModel -> Meditation<HotPath list * MeasurementPlan>
// val locatePhaseChange : PerturbationOracle -> ... -> EasyBaseline -> HardCase -> Meditation<PhaseChangePoint>
// val reRank : Hypothesis list -> Warrant list -> Meditation<Ranking>
//     定性重排：不产新 warrant；产出供控制层排调查顺序（签名形态属策略）
// val probeBoundaries : ThoughtOracle -> ... -> ScenarioSetup -> RuleUnderTest
//     -> Meditation<BoundaryCase list * Prediction list>
//     产出只进 Proposal 通道，永不构成观察 warrant（§13.2③）
