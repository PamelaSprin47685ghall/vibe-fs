// Meditator DSL — 生成/评估边界。语法化：演算 P1–P3 + 各 H 规则的 Validated/Constructed 出口。
// Proposal/Accepted/Validated/Constructed 构造函数私有；VerifierWitness/Provenance 是 opaque record；
// EvaluatorAuthority 构造私有，且 Verifiers.*/issue/fromFields/create 全部 internal（P0-1）——
// 权柄不出程序集：外部代码只能提交待验证材料给封闭验证操作（Ledger.Verification），
// 不能拿到权柄本身；"trust-me" 伪造在类型与可见性上双重不可表达。
// TestHarness 程序集经 InternalsVisibleTo 获得测试特权（测试是可信装配代码，非生产调用面）。
// Fable 警示：私有性是编译期约束，JS 输出无运行时防护；
// 运行时防线 = 静态门禁（裸 Proposal(/Accepted(/Validated(/Constructed(/VerifierWitness( 出界即红）
// + 第 1 层行为测试（T1）。
// 编译顺序：2（无依赖）。
module Meditator.Boundary

open System.Runtime.CompilerServices

// [安全-中]：IVT 仅限 Debug 构建（测试特权）——Release 生产程序集不含 IVT，
// 任何同名 TestHarness 程序集都无法获得 internal 权柄（权柄不出生产程序集）。
#if DEBUG
[<assembly: InternalsVisibleTo("TestHarness")>]
#endif
do ()

/// 出处：谁、什么协议、何时产出。
/// ProducedAt 是外部注入的时间戳（环境时钟），不参与 canonical identity（演算 §41.5）；
/// 禁止以 transcript digest 冒充时间——字段语义是时间，不是内容摘要。
type Provenance =
    private
        { OriginId: string
          Protocol: string
          ProducedAt: string }

module Provenance =
    let create (producedAt: string) (originId: string) (protocol: string) : Provenance =
        { OriginId = originId
          Protocol = protocol
          ProducedAt = producedAt }

    let originId (p: Provenance) : string = p.OriginId
    let protocol (p: Provenance) : string = p.Protocol
    let producedAt (p: Provenance) : string = p.ProducedAt

/// 验证权柄：唯一能签发 VerifierWitness 与 c! 项的令牌（演算 §4 P2 权柄条件）。
/// 构造私有——不能凭空铸造，只能具名持有 Verifiers.* 固定常量。
/// 每个权柄一个版本化 verifier 身份；随 policyVersion 发布（P0 固定裁决 1）。
type EvaluatorAuthority = private EvaluatorAuthority of verifierId: string

module Verifiers =
    /// internal（P0-1）：权柄不出程序集。外部只能经 Ledger.Verification 的封闭操作提交材料。
    let internal schema = EvaluatorAuthority "schema/v1"
    let internal source = EvaluatorAuthority "source/v1"
    let internal inference = EvaluatorAuthority "inference/v1"
    let internal observation = EvaluatorAuthority "observation/v1"
    let internal deterministicCheck = EvaluatorAuthority "deterministic-check/v1"

/// 分层 verifier：每层证明不同的事情，没有任何一层单独授予"现实真理"（§12.4）。
type VerifierKind =
    | Schema // 结构合法
    | Source // 引用确实存在
    | Inference // 结论确实由前提推出
    | Observation // 观测协议确实执行

type VerifierWitness =
    private
        { Kind: VerifierKind
          VerifierId: string
          Digest: string }

module VerifierWitness =
    /// 唯一签发点：VerifierId 由权柄派生，调用方不能书写（P2 权柄条件）。
    /// internal（P0-1）：新签发只能发生在程序集内（verifier 实现）；
    /// 外部调用方只能经 Ledger.Verification 的封闭操作提交待验证材料。
    let internal issue (EvaluatorAuthority verifierId) (kind: VerifierKind) (digest: string) : VerifierWitness =
        { Kind = kind
          VerifierId = verifierId
          Digest = digest }

    /// 恢复路径：从 canonical 字段还原已签发的 witness（重放历史事实，不是新签发）。
    /// internal（P0-1）：只允许 EventCodec 的重放路径调用；外部不得经此伪造任意 verifier ID。
    let internal fromFields (kind: VerifierKind) (verifierId: string) (digest: string) : VerifierWitness =
        { Kind = kind
          VerifierId = verifierId
          Digest = digest }

    let kind (w: VerifierWitness) : VerifierKind = w.Kind
    let verifierId (w: VerifierWitness) : string = w.VerifierId
    let digest (w: VerifierWitness) : string = w.Digest

/// 候选（P1 引入的项）。
type Proposal<'a> = private Proposal of 'a * Provenance

/// 已检查项（P2 check 引入的项）。
type Accepted<'a> = private Accepted of 'a * VerifierWitness list

/// 经确定性复核的项（H-relax/H-sample/H-anneal/H-swarm 的出口）。
type Validated<'a> = private Validated of 'a * VerifierWitness list

/// 存在性构造的项（H-exist，∃-intro）。
type Constructed<'a> = private Constructed of 'a * VerifierWitness list

module Proposal =
    /// P1：唯一引入规则。普通业务代码没有 oracle provenance——
    /// 伪造成本 = 伪造整条 provenance 链。
    let fromOracle (provenance: Provenance) (value: 'a) : Proposal<'a> = Proposal(value, provenance)

    /// 消除视图：只读。值要成为证据，唯一通道是 P2 check（演算 §4：没有第三条消除规则）。
    let value (Proposal(v, _)) : 'a = v
    let provenance (Proposal(_, p)) : Provenance = p

module Accepted =
    /// P2：唯一引入规则。需权柄 κ；witness 非空且全部由 κ 签发（VerifierId = κ）。
    /// internal（P0-1）：程序集内 verifier 实现专用；外部经封闭验证操作。
    let internal create
        (EvaluatorAuthority vid)
        (witnesses: VerifierWitness list)
        (value: 'a)
        : Result<Accepted<'a>, string> =
        match witnesses with
        | [] -> Error "P2 side condition: at least one verifier witness required"
        | ws when ws |> List.exists (fun w -> VerifierWitness.verifierId w <> vid) ->
            Error $"P2 authority mismatch: witness issued by verifier other than {vid}"
        | ws -> Ok(Accepted(value, ws))

    let value (Accepted(v, _)) : 'a = v
    /// internal（P0-2）：witness 不出程序集——外部拿不到 witness 就无法把它
    /// 挪到别的 Claim/Warrant 上（洗白链第一步被切断）。
    let internal witnesses (Accepted(_, ws)) : VerifierWitness list = ws

module Validated =
    /// internal（P0-1）：同 Accepted.create。
    let internal create
        (EvaluatorAuthority vid)
        (witnesses: VerifierWitness list)
        (value: 'a)
        : Result<Validated<'a>, string> =
        match witnesses with
        | [] -> Error "H side condition: deterministic-check witness required"
        | ws when ws |> List.exists (fun w -> VerifierWitness.verifierId w <> vid) ->
            Error $"H authority mismatch: witness issued by verifier other than {vid}"
        | ws -> Ok(Validated(value, ws))

    let value (Validated(v, _)) : 'a = v
    /// internal（P0-2）：witness 不出程序集。
    let internal witnesses (Validated(_, ws)) : VerifierWitness list = ws

module Constructed =
    /// internal（P0-1）：同 Accepted.create。
    let internal create
        (EvaluatorAuthority vid)
        (witnesses: VerifierWitness list)
        (value: 'a)
        : Result<Constructed<'a>, string> =
        match witnesses with
        | [] -> Error "H-exist side condition: witness required"
        | ws when ws |> List.exists (fun w -> VerifierWitness.verifierId w <> vid) ->
            Error $"H-exist authority mismatch: witness issued by verifier other than {vid}"
        | ws -> Ok(Constructed(value, ws))

    let value (Constructed(v, _)) : 'a = v

/// 评估四结局（P2/P3 + §12.4）。
/// case 加 Eval 前缀：避免与边界类型 Accepted 同名导致的消歧噪音；语义与 §12.4 一致。
type RejectionReason =
    | SchemaViolation of string
    | SourceMissing of string
    | InferenceInvalid of string
    | ProtocolNotExecuted of string
    | PolicyViolation of string

type Contention =
    { SubjectId: string
      Sides: (string * Provenance) list }

type EvidenceRequest = { What: string; ForClaimId: string }

type Evaluation<'a> =
    | EvalAccepted of Accepted<'a>
    | EvalRejected of RejectionReason
    | EvalContested of Contention
    | EvalNeedsEvidence of EvidenceRequest
