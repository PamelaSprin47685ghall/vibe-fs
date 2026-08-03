// ④ 假设与模型形成（设计目录）：产出只含 Proposal<'a>——类型上够不到 Accepted（演算 T2）。
// 编译顺序：12（依赖 Boundary、Meditation、Oracle、Ensure）。
module Meditator.Methods.Hypothesis

open System.Threading.Tasks
open Meditator.Boundary
open Meditator.Meditation
open Meditator.Oracle
open Meditator.Ensure

type ObservedSurprise =
    { Observation: string
      Expectation: string }

/// §13.2④：Abduction → Hypothesis。返回类型里没有"已证实"字段——
/// 生成与提交权分离（演算 §4：Proposal 没有第三条消除规则）。
/// TranscriptDigest 是冻结 transcript 的摘要（轨迹确定性的事实，与 Provenance.ProducedAt 正交）。
type AbductiveResult =
    { Hypotheses: Proposal<AbductiveHypothesis> list
      ResidualUnknown: string
      DiscriminatingTests: string list
      TranscriptDigest: string }

/// abduce：为惊异观察生成竞争假设。
/// 欠债（派生轨）：调用后 ledger 纯派生 falsify | discriminatingTest 义务（§15.2）——
/// 义务由 Obligation.fs 的谓词发现，不在此记账。
/// ProducedAt 语义归位：只接受环境时钟的时间戳（评审：以 transcript digest 冒充时间是字段语义漂移）；
/// digest 进 TranscriptDigest，不冒充时间。
let abduce
    (oracle: AbductionOracle)
    (encode: AbductionProposal -> string)
    (validate: string -> Result<ValidatedAnswer<AbductionProposal>, string>)
    (invocation: OracleInvocation)
    (surprise: ObservedSurprise)
    : Meditation<AbductiveResult> =
    meditation {
        let! answer =
            ensureOracleAnswer
                (fun ct ->
                    task {
                        let! proposal =
                            oracle.GenerateCompetingExplanations
                                { Surprise = surprise.Observation
                                  ContextDigest = invocation.EvidenceSnapshotHash }
                                ct

                        return encode proposal
                    })
                validate
                invocation

        let! env = ask
        let (InvocationKey key) = OracleInvocation.key invocation

        let provenance =
            Provenance.create (env.Clock()) key $"abduction/{invocation.MethodVersion}"

        return
            { Hypotheses = answer.Value.Hypotheses |> List.map (Proposal.fromOracle provenance)
              ResidualUnknown = answer.Value.ResidualUnknown
              DiscriminatingTests = answer.Value.Hypotheses |> List.collect (fun h -> h.DiscriminatingTests)
              TranscriptDigest = answer.TranscriptDigest }
    }

// 其余 10 个生成器（设计目录 ④；形态与 abduce 相同：oracle → ensure → Proposal 包装，
// 各自返回 §13.2④ 规定的认识类型，各自声明欠债）：
//
// val induce : InductionOracle -> ... -> Case list -> Meditation<GuardedGeneralization>
//     欠债 exceptionSearch → falsify
// val analogize : AnalogyOracle -> ... -> SourceDomain -> TargetDomain -> Meditation<TransferCandidate>
//     欠债 structuralSimilarityCheck + mismatchAudit
// val generalize : GeneralizationOracle -> ... -> LocalSymptom -> Meditation<WiderScopedCandidate>
//     欠债 excludedInstances + counterexampleSearch
// val specialize : SpecializationOracle -> ... -> GeneralProblem -> ConcreteInstance list -> Meditation<InstanceLessons>
// val transferFromTemplate : CanonicalTemplate -> CurrentProblem -> Meditation<TransferSkeleton>
//     假设失败必须显式列出
// val modelSystem : SystemsOracle -> ... -> SystemBoundary -> Meditation<DependencyModel>
// val analyzeDialectic : DialecticOracle -> ... -> Thesis -> Antithesis -> Meditation<TensionAndSynthesisCandidate>
// val deconstruct : DeconstructionOracle -> ... -> TextOrDesign -> Meditation<FramingCritique>
// val stabilizeReading : HermeneuticOracle -> ... -> WholeArtifact -> PartFocus -> Meditation<StabilizedInterpretation>
// val analyzeSymmetry : SymmetryGroup -> EquivalentCase list -> Meditation<SymmetryCandidate>
//
// ④ 全部方法共享的门禁（演算 T2）：返回类型不含 Accepted<'a>；
// 最小反例测试 §17.2：abduction_cannot_conclude / analogy_cannot_raise_evidence_grade。
