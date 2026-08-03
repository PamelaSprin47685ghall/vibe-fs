// ① 宪法门禁（设计目录）：产出 frame/sense/spec/system。
// 抢占语义在控制层义务排序（Obligation.fs），不在此层签名——方法是纯生产者。
// 编译顺序：11（依赖 Meditation、Oracle、Ensure）。
module Meditator.Methods.Gates

open System.Threading.Tasks
open Meditator.Meditation
open Meditator.Oracle
open Meditator.Ensure

type UserUtterance = UserUtterance of string

type IntentFrame =
    { Goal: string
      ScopeDigest: string
      AssumedIntent: string
      ContractDigest: string }

/// clarifyIntent：唯一可直达 Blocked 的方法（§14.1）。
/// 形状即所有 oracle 方法的形状：oracle 端口 → ensure 冻结 → 领域解释。
/// 装配处以部分应用固定 oracle/encode/validate，调用方只见后两个参数。
/// 消歧纪律：只有唯一解释（Interpretations 恰一项）才采用 AssumedIntent；
/// 多解释或有消歧问题 = 尚未澄清，Blocked 并交出问题（评审：多解释时直接 AssumedIntent 是假澄清）。
let clarifyIntent
    (oracle: IntentOracle)
    (encode: IntentClarificationProposal -> string)
    (validate: string -> Result<ValidatedAnswer<IntentClarificationProposal>, string>)
    (invocation: OracleInvocation)
    (utterance: UserUtterance)
    : Meditation<IntentFrame> =
    meditation {
        let (UserUtterance text) = utterance

        let! answer =
            ensureOracleAnswer
                (fun ct ->
                    task {
                        let! proposal =
                            oracle.Clarify
                                { Utterance = text
                                  ContextDigest = invocation.EvidenceSnapshotHash }
                                ct

                        return encode proposal
                    })
                validate
                invocation

        match answer.Value.Interpretations with
        | [] ->
            return!
                halt (
                    Blocked
                        [ { What = "disambiguating question"
                            WhyNeeded = String.concat "; " answer.Value.DisambiguatingQuestions } ]
                )
        | [ single ] ->
            return
                { Goal = single
                  ScopeDigest = invocation.EvidenceSnapshotHash
                  AssumedIntent = single
                  ContractDigest = invocation.CanonicalInput }
        | multiple ->
            // 多解释仍未澄清：不采用 AssumedIntent，交回消歧问题。
            let interpretations = String.concat "; " multiple

            let questions =
                if List.isEmpty answer.Value.DisambiguatingQuestions then
                    ""
                else
                    "; questions: " + String.concat "; " answer.Value.DisambiguatingQuestions

            return!
                halt (
                    Blocked
                        [ { What = "disambiguating question"
                            WhyNeeded =
                              $"multiple interpretations ({List.length multiple}): {interpretations}{questions}" } ]
                )
    }

// 其余四个签名（设计目录 ①，形态与 clarifyIntent 相同：oracle → ensure → Proposal 包装或 frame 产出）：
//
// val disambiguateConcept : ConceptOracle -> ... -> ConfusedConcept -> Meditation<SenseTable>
//     命中多义 → 义务 ClarifyScope 抢占概率与因果判断（Obligation.fs 排序表达）
//
// val operationalize : OperationalismOracle -> ... -> VagueTerm -> Meditation<OperationSpec>
//     不可操作化 → 抢占证据与数值编译
//
// val axiomatize : PrimitiveTerms -> AllowedOps -> Invariant list -> Meditation<Result<FormalSystem, Inconsistency>>
//     欠债 consistencyCheck（派生轨）；原始术语不稳定时禁止进入（§13.2①）
//
// val stripToAtoms : FirstPrinciplesOracle -> ... -> ProblemStatement -> Meditation<AtomicBasis>
//     剥离假设账本 → 原子事实 + 重建链
