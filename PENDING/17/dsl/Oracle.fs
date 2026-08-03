// Meditator DSL — oracle 边界。语法化：演算 P1 的语法（端口族）+ T5 的缓存身份（invocationKey）。
// 禁止统一 ask（§37.1）；端口任务专用，返回类型上够不到 Accepted——端口产不出已检查项（T1）。
// 编译顺序：6（依赖 Boundary、Ledger——EventCodec 长度前缀编码，138 版）。
module Meditator.Oracle

open System
open System.Security.Cryptography
open System.Text
open System.Threading
open System.Threading.Tasks
open Meditator.Ledger

/// 调用身份（§8.2）：同 key 只许一个 accepted transcript——T5 的侧条件，journal 执行。
type InvocationKey = InvocationKey of string

type OracleInvocation =
    { MethodId: string
      MethodVersion: string
      PromptTemplateVersion: string
      CanonicalInput: string // canonical bytes 投影
      EvidenceSnapshotHash: string
      ModelProfile: string
      PolicyVersion: string }

module OracleInvocation =
    let private sha256Hex (s: string) : string =
        use sha = SHA256.Create()
        sha.ComputeHash(Encoding.UTF8.GetBytes s) |> Convert.ToHexString

    /// key = hash(methodId, methodVersion, templateVersion, canonicalInput,
    ///           evidenceSnapshotHash, modelProfile, policyVersion)（§8.2 分量全序）。
    /// 138 版：长度前缀编码（不再 \u001F 拼接——分量内出现 \u001F 时 ["a␟b","c"] 与
    /// ["a","b␟c"] 曾得同一 key；长度前缀消除分隔符碰撞）。
    let key (inv: OracleInvocation) : InvocationKey =
        EventCodec.sha256Hex (
            EventCodec.field "m" inv.MethodId
            + EventCodec.field "mv" inv.MethodVersion
            + EventCodec.field "pt" inv.PromptTemplateVersion
            + EventCodec.field "ci" inv.CanonicalInput
            + EventCodec.field "ev" inv.EvidenceSnapshotHash
            + EventCodec.field "mp" inv.ModelProfile
            + EventCodec.field "pv" inv.PolicyVersion
        )
        |> InvocationKey

// ── 端口族（§37.1）：prompt 与 proposal 是各端口的专用类型。
// 每个端口一个语义任务；同一方法内部的不同语义也可以使用不同端口。

type AbductionPrompt =
    { Surprise: string
      ContextDigest: string }

type AbductiveHypothesis =
    { Statement: string
      ExplainsObservationIds: string list
      PredictionsIfTrue: string list
      DiscriminatingTests: string list
      AlternativeTo: string list }

/// §5.3：生成阶段不得同时把新候选标记为已证实——类型里没有"verified"字段。
type AbductionProposal =
    { Hypotheses: AbductiveHypothesis list
      ResidualUnknown: string }

type AbductionOracle =
    abstract GenerateCompetingExplanations: AbductionPrompt -> CancellationToken -> Task<AbductionProposal>

type CounterexamplePrompt =
    { Claim: string
      FailureConditions: string list }

type CounterexampleProposal =
    { Counterexamples: string list
      SearchedScopes: string list }

type CounterexampleOracle =
    abstract GenerateCounterexamples: CounterexamplePrompt -> CancellationToken -> Task<CounterexampleProposal>

type ConceptDisambiguationPrompt =
    { Concept: string
      UsageDigests: string list }

type ConceptDisambiguationProposal =
    { Senses: string list
      Boundaries: string list }

type ConceptOracle =
    abstract Disambiguate: ConceptDisambiguationPrompt -> CancellationToken -> Task<ConceptDisambiguationProposal>

type RelationPrompt =
    { LeftId: string
      RightId: string
      ContextDigest: string }

type RelationProposal = { Relation: string; Direction: string }

type RelationOracle =
    abstract JudgeRelation: RelationPrompt -> CancellationToken -> Task<RelationProposal>

type IntentClarificationPrompt =
    { Utterance: string
      ContextDigest: string }

type IntentClarificationProposal =
    { Interpretations: string list
      AssumedIntent: string
      DisambiguatingQuestions: string list }

type IntentOracle =
    abstract Clarify: IntentClarificationPrompt -> CancellationToken -> Task<IntentClarificationProposal>

/// 验证链（§37.4）的出口：parse → schema → semantic → method-specific → canonicalize。
/// 每步失败都是 RejectionReason，不是异常——可预见失败走 Result（宝典铁律）。
/// TranscriptDigest：冻结 transcript 的摘要（轨迹确定性的事实；事件 OracleInvocationAccepted
/// 携带 (key, digest) 供审计，完整 transcript 存 IAcceptedTranscriptStore——P0-2 不再
/// 把事件行塞进本类型）。
type ValidatedAnswer<'a> = { Value: 'a; TranscriptDigest: string }
