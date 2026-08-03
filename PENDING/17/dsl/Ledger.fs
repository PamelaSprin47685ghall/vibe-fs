// Meditator DSL — 认识账本。语法化：演算 F1–F2（形成）、W1–W5（warrant 引入）、
// POL（极性演算）、G（grade 代数）、C1（fold β 规则）、S1–S3（结构规则的账本侧）、
// §9.1（envelope codec）、§9.2（账本字段 ↔ 事件）、§9.3（canonical codec 唯一 owner）、
// §11（attempt key 与 NoProgress）、§41.3（依赖簇）。
// 只存认识事实；程序位置（Stage/Phase/NextAction/CurrentMethod）不可表示（P3）。
// 编译顺序：4（依赖 Boundary）。
module Meditator.Ledger

open System
open System.Security.Cryptography
open System.Text
open Meditator.Boundary

// ── 稳定身份：由规范化内容的 canonical bytes 派生（F1 Leibniz 同一性）。
// 禁止 GUID、进程 hash、当前时间参与 identity（P0 固定裁决 6）。
type ClaimId = ClaimId of string
type WarrantId = WarrantId of string
type SourceId = SourceId of string
type UnknownId = UnknownId of string
type EpisodeId = EpisodeId of string

/// 事件幂等身份（S2）：由负载 + schema/policy/reducer 版本派生（EventCodec.eventId）。
type EventId = EventId of string

let claimIdText (ClaimId id) = id
let warrantIdText (WarrantId id) = id
let unknownIdText (UnknownId id) = id
let eventIdText (EventId id) = id

/// 适用边界：参与命题身份与每次查询（F2）。
type Scope =
    { Content: string option
      Time: string option
      Modality: string option
      Population: string option }

/// 命题角色：创建后不变。
type PropositionRole =
    | Definition
    | Assumption
    | Hypothesis
    | Assertion
    | Preference

/// 提案来源：创建后不变。OracleProposal 本身不产生 warrant——它不是任何 W 规则的前提（演算 §5）。
/// case 加 By 前缀：与 WarrantKind 同名 case 消歧。
type ProposalSource =
    | ByObservation
    | BySourceSpan
    | ByDerivation
    | ByUserStipulation
    | ByOracleProposal

/// F1：良形命题。
/// IntroducedBy：引入事件（ClaimFramed）的 payload digest；未提交（调用方内存）为空串，
/// fold 提交时填充——停证可独立重放的依据（与 WarrantData.IntroducedBy 对称）。
type Claim =
    { Id: ClaimId
      Statement: string
      Role: PropositionRole
      Source: ProposalSource
      Scope: Scope
      IntroducedBy: string }

type Polarity =
    | Supports
    | Opposes

/// 支持强度：与四值序正交，与 grade 正交（§7.2）。
type SupportStrength =
    | Weak
    | Moderate
    | Strong

/// Warrant 引入规则的五种 kind（W1–W5）。
type WarrantKind =
    | Observation
    | SourceSpan
    | Derivation
    | UserStipulation
    | Elicitation

/// 证据项（proof term）的载荷。Rule = WarrantRuleId：由版本化 F# 代码定义，随 policyVersion 发布；
/// LLM 可提议新规则，不能批准或执行（P0 固定裁决 1）。
/// Warrant 本身 opaque（构造经 Warrant.create 检查 §5 统一侧条件）；WarrantData 是公开载荷，
/// 但任意填充的载荷过不了 create 的侧条件检查。
type WarrantData =
    { Id: WarrantId
      ClaimId: ClaimId
      Polarity: Polarity
      Kind: WarrantKind
      Rule: string
      Strength: SupportStrength
      Scope: Scope
      Origin: Provenance
      VerifierWitnesses: VerifierWitness list // 非空：W 规则统一侧条件
      DependencyWarrantIds: WarrantId list
      UltimateSourceIds: SourceId list // 非空：独立性派生的原料（§41.3）
      IntroducedBy: string } // 引入事件（ContributionAccepted）的 payload digest；
// 未提交（调用方内存）为空串，fold 提交时填充——停证可独立重放的依据

type Warrant = private Warrant of WarrantData

// ── POL：极性四值。由 scope-compatible warrant 集合纯派生，不存储（S1 的推论：派生不占事实位）。
type PolarityState =
    | Unknown
    | SupportedOnly
    | RefutedOnly
    | Contested

let polarityState (supports: 'w list) (opposes: 'w list) : PolarityState =
    match List.isEmpty supports, List.isEmpty opposes with
    | true, true -> Unknown
    | false, true -> SupportedOnly
    | true, false -> RefutedOnly
    | false, false -> Contested

module Warrant =
    /// P0-9（137 版）说明：WarrantId 由 canonical 主体派生（Leibniz 同一性，与 ClaimId 对称）。
    /// 本模块在 EventCodec 之前编译（codec 依赖本模块），无法引用 EventCodec.warrantIdOfData——
    /// ID 校验由两层兜底：EventCodec.decodePayload（恢复路径）与 fold（账本路径），
    /// 均在 warrantIdOfData 定义之后；create 不校验（调用方构造的 ID 将在进入账本时被拒）。
    /// 注意：create 的 witness-kind 兼容检查接受 Inference witness（Derivation 前提依赖），
    /// 但 Inference witness 只能经 RuleEngine.verify 产生（P0 规则表为空 → deduction 不可达），
    /// 因此 Derivation warrant 在 P0 实际不可构造——类型门禁约定。
    /// verifierId 白名单（P0-3/P0-4）：kind 与 verifierId 必须匹配——持久化 witness 的
    /// 字符串字段不能脱离 kind 语义（重放/fromFields 恢复的 witness 必须属于声明 kind 的
    /// 版本化 verifier；信任 journal adapter 威胁模型下防程序错误与配置漂移）。
    let verifierIdMatches (w: VerifierWitness) : bool =
        match VerifierWitness.kind w, VerifierWitness.verifierId w with
        | VerifierKind.Observation, "observation/v1" -> true
        | VerifierKind.Inference, "inference/v1" -> true
        | VerifierKind.Source, "source/v1" -> true
        // deterministic-check/v1 与 schema/v1 同列：Validated 出口（relax/simplify/sampleThenVerify/construct）
        // 的复核 witness 以 Schema kind 持久化（见 THREAT_MODEL.md §2.5）。
        | VerifierKind.Schema, "schema/v1" -> true
        | VerifierKind.Schema, "deterministic-check/v1" -> true
        | _ -> false

    /// witness 集合与 warrant kind 的兼容性（P0-2 洗白防线 + P0-3 主要 witness 强制）：
    /// Observation warrant 必须含至少一个 Observation witness（其余可为 Schema）；
    /// SourceSpan 必须含至少一个 Source witness（其余可为 Schema）；
    /// Derivation 必须含至少一个 Inference witness（其余可为观察/来源/结构 witness）；
    /// UserStipulation/Elicitation 只能带 Schema。
    /// 效果：Schema witness 不能单独造 Observation/SourceSpan warrant；
    /// observation witness 不能被挪去造 SourceSpan/Derivation warrant。
    let witnessesCompatible (witnesses: VerifierWitness list) (kind: WarrantKind) : bool =
        let kinds = witnesses |> List.map VerifierWitness.kind

        match kind with
        | Observation ->
            List.contains VerifierKind.Observation kinds
            && kinds
               |> List.forall (fun k -> k = VerifierKind.Observation || k = VerifierKind.Schema)
        | SourceSpan ->
            List.contains VerifierKind.Source kinds
            && kinds
               |> List.forall (fun k -> k = VerifierKind.Source || k = VerifierKind.Schema)
        | Derivation ->
            List.contains VerifierKind.Inference kinds
            && kinds
               |> List.forall (fun k ->
                   k = VerifierKind.Inference
                   || k = VerifierKind.Observation
                   || k = VerifierKind.Source
                   || k = VerifierKind.Schema)
        | UserStipulation
        | Elicitation -> kinds |> List.forall (fun k -> k = VerifierKind.Schema)

    /// 唯一构造点：检查 §5 统一侧条件（VerifierWitnesses 非空 ∧ UltimateSourceIds 非空
    /// ∧ witness 集合与 warrant kind 兼容 ∧ verifierId 白名单——P0-3/P0-4）。
    /// scope 兼容由调用方保证（跨 scope 推广必须经显式推导）；fold 对事件重验本条。
    let create (data: WarrantData) : Result<Warrant, string> =
        if List.isEmpty data.VerifierWitnesses then
            Error "W side condition: VerifierWitnesses must be non-empty"
        elif List.isEmpty data.UltimateSourceIds then
            Error "W side condition: UltimateSourceIds must be non-empty"
        elif data.VerifierWitnesses |> List.exists (fun w -> not (verifierIdMatches w)) then
            Error "W side condition: witness verifierId not in policy whitelist"
        elif not (witnessesCompatible data.VerifierWitnesses data.Kind) then
            Error "W side condition: verifier witness kind incompatible with warrant kind"
        else
            Ok(Warrant data)

    /// P0-1：WarrantData 含 VerifierWitnesses——公开 data 等于重新暴露 witness。
    /// internal：witness 只能留在账本内（codec/fold/digest 同程序集；测试经 InternalsVisibleTo）。
    /// 外部拿不到 witness → Warrant.create 虽公开也无法构造新 WarrantData（缺 witness 字段），
    /// 洗白链（复制 warrant、转移 claim、升级 strength、伪造来源）彻底切断。
    let internal data (Warrant d) : WarrantData = d

    let id (Warrant d) = d.Id
    let claimId (Warrant d) = d.ClaimId
    let polarity (Warrant d) = d.Polarity
    let kind (Warrant d) = d.Kind
    let rule (Warrant d) = d.Rule
    let strength (Warrant d) = d.Strength
    let scope (Warrant d) = d.Scope
    let origin (Warrant d) = d.Origin
    /// internal（P0-2）：witness 不出程序集——外部拿不到 witness 就不能洗白转用。
    let internal witnesses (Warrant d) = d.VerifierWitnesses
    let dependencies (Warrant d) = d.DependencyWarrantIds
    let sources (Warrant d) = d.UltimateSourceIds

/// T3 属性测试义务：fold 后 v(c) 沿分量序不下降（反证不删除历史，只抬升 v）。

// ── G：grade 代数。每维独立 meet-semilattice，整体乘积序；禁止加权总分（§41.4）。
type Directness =
    | Direct
    | Indirect
    | Derivational

type Reliability =
    | Confirmed
    | Corroborated
    | Tentative

/// independence = provenance 依赖簇数量（§41.3），不是 warrant 条数、不是 Agent 数量。
type Independence = Clusters of int

type CoverageGrade =
    | ClosedWorldCoverage
    | OpenWorldCoverage

type Reproducibility =
    | Replayed
    | NotYetReplayed

type EpistemicGrade =
    { Directness: Directness
      Reliability: Reliability
      Independence: Independence
      Coverage: CoverageGrade
      Reproducibility: Reproducibility }

module Grade =
    let meetDirectness a b =
        match a, b with
        | Direct, Direct -> Direct
        | Derivational, _
        | _, Derivational -> Derivational
        | _ -> Indirect

    let meetReliability a b =
        match a, b with
        | Confirmed, Confirmed -> Confirmed
        | Tentative, _
        | _, Tentative -> Tentative
        | _ -> Corroborated

    let meetIndependence (Clusters a) (Clusters b) = Clusters(min a b)

    let meetCoverage a b =
        match a, b with
        | ClosedWorldCoverage, ClosedWorldCoverage -> ClosedWorldCoverage
        | _ -> OpenWorldCoverage

    let meetReproducibility a b =
        match a, b with
        | Replayed, Replayed -> Replayed
        | _ -> NotYetReplayed

    /// G-meet：逐维取弱。W3 侧条件 G(deriv) ⪯ ⊓ᵢ G(wᵢ) 由此计算。
    let meet (a: EpistemicGrade) (b: EpistemicGrade) : EpistemicGrade =
        { Directness = meetDirectness a.Directness b.Directness
          Reliability = meetReliability a.Reliability b.Reliability
          Independence = meetIndependence a.Independence b.Independence
          Coverage = meetCoverage a.Coverage b.Coverage
          Reproducibility = meetReproducibility a.Reproducibility b.Reproducibility }

    let meetAll (grades: EpistemicGrade list) : EpistemicGrade option =
        match grades with
        | [] -> None
        | g :: gs -> Some(List.fold meet g gs)

/// 未知区域关闭等级（§10.4）。O-cov 侧条件：NoHit 永远不能产生 VerifiedFinite。
/// 等级序（§9.2 UnknownRegionUpdated 非降级）：Open < ClaimedComplete < VerifiedFinite / UserAssumedComplete。
type UnknownCoverage =
    | Open
    | ClaimedComplete
    | VerifiedFinite of certificateDigest: string
    | UserAssumedComplete of stipulationRef: string

let coverageRank (c: UnknownCoverage) : int =
    match c with
    | Open -> 0
    | ClaimedComplete -> 1
    | VerifiedFinite _ -> 2
    | UserAssumedComplete _ -> 2

type UnknownRegion =
    { Id: UnknownId
      Description: string
      Coverage: UnknownCoverage }

/// 方法轨迹（§7.1.4）：方法应用的事实记录；SEARCH_ATTEMPTED 进此（§6.4）。
type MethodEpisode =
    { Id: EpisodeId
      MethodId: string
      ObligationId: string
      InputDigests: string list
      CandidateDigests: string list
      AcceptedDigests: string list
      RejectedDigests: string list }

/// 被拒 oracle 答案（§9.2：OracleAnswerRejected append 进 Rejections，不覆盖）。
type RejectedProposal =
    { InvocationKey: string
      Reason: string }

type SearchOutcome =
    | NoHit
    | Hit of digest: string

/// 搜索尝试的记账事实（§10.4"连续 N 轮 NoHit"的判定输入；进 SearchAttempts，append）。
type SearchAttempt =
    { ObligationId: string
      Outcome: SearchOutcome
      Sequence: int }

/// 进展保证（演算 §11）：同 key 已执行 → 不得再次调用 Oracle。
type AttemptKey =
    { ObligationId: string
      LedgerDigest: string
      PolicyVersion: string }

type MeditationResourceUsage =
    { OracleCalls: int
      NormalizationSweeps: int
      CreditsConsumed: int
      Stalls: int }

/// 账本（⊢ S wf）：认识事实的唯一容器。
/// 每个字段都有事件可抵达（演算 §9.2 对照表）；fold 对同 key 异内容报 DuplicateEvent。
/// EventCount：已折叠事件数（fold 的机械事实）——EventId 的序列分量来源（S2），
/// 不进入 semanticDigest（基础设施事实，不是认识事实）。
type MeditationLedger =
    { Claims: Map<ClaimId, Claim>
      Warrants: Map<WarrantId, Warrant>
      Hypotheses: Map<string, string>
      Concepts: Map<string, string>
      Relations: Map<string, string>
      Evidence: Map<string, string>
      Counterexamples: Map<string, string>
      UnknownRegions: Map<UnknownId, UnknownRegion>
      MethodEpisodes: Map<EpisodeId, MethodEpisode>
      Rejections: RejectedProposal list
      SearchAttempts: SearchAttempt list
      Attempts: Set<AttemptKey>
      AcceptedTranscripts: Map<string, string> // P0-5：invocation key → transcript digest（fold 防同 key 异 digest 分叉）
      ResourceUsage: MeditationResourceUsage
      EventCount: int
      CompletedReportDigest: string option } // P1-1：MeditationCompleted 已提交的标记——恢复路径据此跳过重复 append（幂等）

    static member Empty =
        { Claims = Map.empty
          Warrants = Map.empty
          Hypotheses = Map.empty
          Concepts = Map.empty
          Relations = Map.empty
          Evidence = Map.empty
          Counterexamples = Map.empty
          UnknownRegions = Map.empty
          MethodEpisodes = Map.empty
          Rejections = []
          SearchAttempts = []
          Attempts = Set.empty
          AcceptedTranscripts = Map.empty
          ResourceUsage =
            { OracleCalls = 0
              NormalizationSweeps = 0
              CreditsConsumed = 0
              Stalls = 0 }
          EventCount = 0
          CompletedReportDigest = None }

// ── C1：事件是账本唯一的作用量。命令可失败；已提交事件只证明对应领域动作发生（§7.1）。
type MeditationEvent =
    | MeditationRequested of intentDigest: string
    | ClaimFramed of Claim
    | OracleInvocationClaimed of invocationKey: string
    | OracleInvocationAccepted of invocationKey: string * transcriptDigest: string
    | OracleAnswerRejected of RejectedProposal
    | ContributionAccepted of Warrant
    | EvidenceObserved of evidenceId: string * digest: string
    | HypothesisRecorded of hypothesisId: string * digest: string
    | ConceptRecorded of conceptId: string * digest: string
    | RelationRecorded of relationId: string * digest: string
    | CounterexampleRecorded of counterexampleId: string * digest: string
    | UnknownRegionUpdated of UnknownRegion
    | SearchAttempted of SearchAttempt
    | EpisodeRecorded of MethodEpisode
    | AttemptRecorded of AttemptKey
    | NoProgress of obligationId: string * attemptKey: string
    | CreditsConsumed of amount: int
    | SweepCompleted
    | MeditationCompleted of reportDigest: string
    | MeditationFailed of reason: string

type FoldError =
    | DuplicateEvent of string
    | MissingClaim of ClaimId
    | InvalidTransition of string

/// 事件 schema 版本：codec 与 fold 的契约面；journal 行里 V 字段即此。
/// v2（安全审查）：opt 编码改为 \u0002 前缀 + 双写转义（消除 U+0001 哨兵歧义）。
/// v3（安全审查终轮）：canonicalRequest 的 MethodHints 改逐项长度前缀编码。
/// v4（P1-5）：canonicalRequest 的 Goal/EvidenceMode 改显式持久化标签（不依赖 DU ToString）。
/// v5（138 版）：EventId 与 OracleInvocation.key 改长度前缀编码（分隔符碰撞消除）。
/// 定义在此（P1-6）：Report.conclude 需要它构造完成事件——Kernel 编译顺序在后。
[<Literal>]
let EventSchemaVersion = 5

/// 事件信封（演算 §9.1）：EventId 由负载与版本派生（S2）。
/// Sequence（P1-1）：Q 字段——replay 必须校验其与折叠序号连续（检测删行/重排/缺口）。
/// 威胁模型（P0-4 明确声明）：Journal 是受信任的完整性边界——DSL 防程序错误与
/// 配置漂移（verifierId 白名单、sequence 连续、digest 校验、round-trip canonical），
/// 不防持久化介质的恶意篡改（SHA-256 无密钥，能改 journal 者可重算 EventId/PayloadDigest）；
/// 需要对抗恶意介质时须引入签名/MAC 或外部锚定 hash chain（记录为已知边界）。
type EventEnvelope =
    { EventId: EventId
      SchemaVersion: int
      PolicyVersion: string
      ReducerVersion: string
      Sequence: int
      Payload: MeditationEvent
      PayloadDigest: string }

// ── §9.3 canonical codec：唯一 owner（本文件）。事件编码、EventId、semanticDigest 共用。
// 长度前缀编码 `tag:len:bytes`：无分隔符歧义、无转义问题。
// option 编码（P0-5）：None 哨兵 \u0001；Some 前缀 \u0002 + 内容中 \u0002 双写转义——
// 内容恰为 U+0001 不再与 None 歧义。
// 禁止调用方注入 encode；编解码是 Ledger 边界的契约面（VERIFY-008）。
// 解析是重放路径：还原已提交事实，不重新签发（VerifierWitness 经 fromFields 还原）；
// 未知 tag/版本一律 Error——fail closed，不猜测迁移。
module EventCodec =

    let sha256Hex (s: string) : string =
        use sha = SHA256.Create()
        sha.ComputeHash(Encoding.UTF8.GetBytes s) |> Convert.ToHexString

    let field (tag: string) (s: string) : string =
        let len = Encoding.UTF8.GetByteCount s
        $"{tag}:{len}:{s}"

    let private opt (o: string option) : string =
        match o with
        | None -> "\u0001"
        // [安全-低]：Some 加 \u0002 前缀并把内容中的 \u0002 双写转义——
        // 内容恰为 U+0001 不再与 None 哨兵歧义（round-trip 无损）。
        | Some s -> "\u0002" + s.Replace("\u0002", "\u0002\u0002")

    let private noneOr (v: string) : Result<string option, string> =
        if v = "\u0001" then
            Ok None
        elif v.StartsWith("\u0002", StringComparison.Ordinal) then
            let decoded = v.Substring(1).Replace("\u0002\u0002", "\u0002")
            // round-trip 校验（P0-5）：合法编码必须满足 opt(Some decoded) = v——
            // 不完整转义（如 "\u0002\u0002"）不是任何合法编码的输出，fail closed。
            if opt (Some decoded) = v then
                Ok(Some decoded)
            else
                Error "codec: bad option encoding"
        else
            // [安全-中]：畸形 option 编码必须 fail closed（Error），
            // 不得 failwith 崩溃——重放不可信日志时畸形行是 DoS 面。
            Error "codec: bad option encoding"

    /// scope 的 canonical 渲染（deduce 的 WarrantId 等需要）。
    let renderScope (s: Scope) : string =
        String.concat
            ""
            [ field "sc" (opt s.Content)
              field "st" (opt s.Time)
              field "sm" (opt s.Modality)
              field "sp" (opt s.Population) ]

    let renderPropositionRole (r: PropositionRole) : string =
        match r with
        | Definition -> "DEF"
        | Assumption -> "ASM"
        | Hypothesis -> "HYP"
        | Assertion -> "ASR"
        | Preference -> "PRE"

    let renderProposalSource (s: ProposalSource) : string =
        match s with
        | ByObservation -> "OBS"
        | BySourceSpan -> "SRC"
        | ByDerivation -> "DRV"
        | ByUserStipulation -> "USR"
        | ByOracleProposal -> "ORC"

    let renderPolarity (p: Polarity) : string =
        match p with
        | Supports -> "SUP"
        | Opposes -> "OPP"

    let renderWarrantKind (k: WarrantKind) : string =
        match k with
        | Observation -> "OBS"
        | SourceSpan -> "SRC"
        | Derivation -> "DRV"
        | UserStipulation -> "USR"
        | Elicitation -> "ELI"

    let renderStrength (s: SupportStrength) : string =
        match s with
        | Weak -> "WEA"
        | Moderate -> "MOD"
        | Strong -> "STR"

    let renderVerifierKind (k: VerifierKind) : string =
        match k with
        | VerifierKind.Schema -> "SCH"
        | VerifierKind.Source -> "SRC"
        | VerifierKind.Inference -> "INF"
        | VerifierKind.Observation -> "OBS"

    let renderCoverage (c: UnknownCoverage) : string =
        match c with
        | Open -> "OP"
        | ClaimedComplete -> "CC"
        | VerifiedFinite d -> "VF" + field "d" d
        | UserAssumedComplete r -> "UA" + field "r" r

    let renderOutcome (o: SearchOutcome) : string =
        match o with
        | NoHit -> "NH"
        | Hit d -> "HI" + field "d" d

    let private renderList (items: string list) : string =
        items |> List.map (field "x") |> String.concat ""

    let renderClaim (c: Claim) : string =
        String.concat
            ""
            [ field "id" (claimIdText c.Id)
              field "st" c.Statement
              field "ro" (renderPropositionRole c.Role)
              field "so" (renderProposalSource c.Source)
              renderScope c.Scope
              field "b" c.IntroducedBy ]

    let renderProvenance (p: Provenance) : string =
        String.concat
            ""
            [ field "o" (Provenance.originId p)
              field "p" (Provenance.protocol p)
              field "t" (Provenance.producedAt p) ]

    /// 身份专用 provenance 渲染（P0-1 138 版）：**不含 ProducedAt**——
    /// 时间戳是提交元数据，不参与 canonical identity（与 §4.1/代码注释一致）。
    let renderProvenanceIdentity (p: Provenance) : string =
        String.concat "" [ field "o" (Provenance.originId p); field "p" (Provenance.protocol p) ]

    let renderWitness (w: VerifierWitness) : string =
        String.concat
            ""
            [ field "k" (renderVerifierKind (VerifierWitness.kind w))
              field "v" (VerifierWitness.verifierId w)
              field "d" (VerifierWitness.digest w) ]

    /// warrant canonical 主体（不含 Id 字段）——EventCodec.warrantIdOfData 的输入（P0-9 137 版：
    /// WarrantId 必须由内容派生，调用方不得任意指定）。
    /// P0-1（138 版）：**不含 ProducedAt 与 IntroducedBy**——提交后变化的字段不得进入身份
    /// （fold 填充 IntroducedBy 后账本中 warrant 的 ID 仍匹配自身内容）；集合字段
    /// （witnesses/dependencies/sources）排序去重后渲染——语义相同的集合顺序无关。
    let renderWarrantDataBody (w: WarrantData) : string =
        String.concat
            ""
            [ field "c" (claimIdText w.ClaimId)
              field "p" (renderPolarity w.Polarity)
              field "k" (renderWarrantKind w.Kind)
              field "r" w.Rule
              field "s" (renderStrength w.Strength)
              renderScope w.Scope
              renderProvenanceIdentity w.Origin
              field "w" (renderList (w.VerifierWitnesses |> List.map renderWitness |> List.sort |> List.distinct))
              field "d" (renderList (w.DependencyWarrantIds |> List.map warrantIdText |> List.sort |> List.distinct))
              field
                  "u"
                  (renderList (
                      w.UltimateSourceIds
                      |> List.map (fun (SourceId s) -> s)
                      |> List.sort
                      |> List.distinct
                  )) ]

    let renderWarrantData (w: WarrantData) : string =
        // 序列化（journal 行）：完整字段、原始顺序（含 ProducedAt/IntroducedBy——无损 roundtrip）。
        String.concat
            ""
            [ field "id" (warrantIdText w.Id)
              field "c" (claimIdText w.ClaimId)
              field "p" (renderPolarity w.Polarity)
              field "k" (renderWarrantKind w.Kind)
              field "r" w.Rule
              field "s" (renderStrength w.Strength)
              renderScope w.Scope
              renderProvenance w.Origin
              field "w" (renderList (List.map renderWitness w.VerifierWitnesses))
              field "d" (renderList (List.map warrantIdText w.DependencyWarrantIds))
              field "u" (renderList (List.map (fun (SourceId s) -> s) w.UltimateSourceIds))
              field "b" w.IntroducedBy ]


    /// P0-9：WarrantId 由 canonical 主体派生（Leibniz 同一性）——定义在 EventCodec 内
    /// （双向依赖：EventCodec 的 renderEvent/decodePayload 需要 Warrant，Warrant 模块在
    /// codec 之前编译无法引用本函数——ID 校验由 decodePayload 与 fold 双层兜底）。
    let warrantIdOfData (data: WarrantData) : WarrantId =
        WarrantId(sha256Hex (renderWarrantDataBody data))

    let renderEpisode (e: MethodEpisode) : string =
        String.concat
            ""
            [ field
                  "id"
                  (match e.Id with
                   | EpisodeId i -> i)
              field "m" e.MethodId
              field "o" e.ObligationId
              field "i" (renderList e.InputDigests)
              field "c" (renderList e.CandidateDigests)
              field "a" (renderList e.AcceptedDigests)
              field "r" (renderList e.RejectedDigests) ]

    /// 事件的 canonical 渲染（§9.3）：所有 case 全覆盖。
    let renderEvent (e: MeditationEvent) : string =
        match e with
        | MeditationRequested d -> "R" + field "d" d
        | ClaimFramed c -> "C" + renderClaim c
        | OracleInvocationClaimed k -> "OC" + field "k" k
        | OracleInvocationAccepted(k, d) -> "OA" + field "k" k + field "d" d
        | OracleAnswerRejected r -> "OR" + field "k" r.InvocationKey + field "r" r.Reason
        | ContributionAccepted w -> "W" + renderWarrantData (Warrant.data w)
        | EvidenceObserved(id, d) -> "E" + field "i" id + field "d" d
        | HypothesisRecorded(id, d) -> "H" + field "i" id + field "d" d
        | ConceptRecorded(id, d) -> "N" + field "i" id + field "d" d
        | RelationRecorded(id, d) -> "RL" + field "i" id + field "d" d
        | CounterexampleRecorded(id, d) -> "X" + field "i" id + field "d" d
        | UnknownRegionUpdated u ->
            "U"
            + field "id" (unknownIdText u.Id)
            + field "d" u.Description
            + field "c" (renderCoverage u.Coverage)
        | SearchAttempted a ->
            "S"
            + field "o" a.ObligationId
            + field "c" (renderOutcome a.Outcome)
            + field "n" (string a.Sequence)
        | EpisodeRecorded ep -> "M" + renderEpisode ep
        | AttemptRecorded k ->
            "A"
            + field "o" k.ObligationId
            + field "d" k.LedgerDigest
            + field "p" k.PolicyVersion
        | NoProgress(o, k) -> "NP" + field "o" o + field "k" k
        | CreditsConsumed n -> "K" + field "n" (string n)
        | SweepCompleted -> "SW"
        | MeditationCompleted d -> "MC" + field "d" d
        | MeditationFailed r -> "MF" + field "r" r

    /// S2：EventId 由负载 + 版本 + 序列派生——同负载同版本同序列必同 ID，重放/重试可复现；
    /// 序列 = 已折叠事件数（Kernel 传入；崩溃在 append 后 fold 前重跑时序列不变 → AlreadyCommitted）。
    /// 138 版：长度前缀编码（不再 \u001F 拼接——负载/版本/序列中出现 \u001F 时组件边界歧义）。
    let eventId
        (schemaVersion: int)
        (policyVersion: string)
        (reducerVersion: string)
        (sequence: int)
        (e: MeditationEvent)
        : EventId =
        EventId(
            sha256Hex (
                field "p" (renderEvent e)
                + field "v" (string schemaVersion)
                + field "p" policyVersion
                + field "r" reducerVersion
                + field "q" (string sequence)
            )
        )

    let payloadDigest (e: MeditationEvent) : string = sha256Hex (renderEvent e)

    /// Envelope 的 canonical 行：V:P:R:Q:I:D:E（全部长度前缀；Q 为序列分量，E 为 renderEvent 的负载）。
    let encode
        (schemaVersion: int)
        (policyVersion: string)
        (reducerVersion: string)
        (sequence: int)
        (e: MeditationEvent)
        : string =
        let id = eventId schemaVersion policyVersion reducerVersion sequence e
        let digest = payloadDigest e

        String.concat
            ""
            [ field "V" (string schemaVersion)
              field "P" policyVersion
              field "R" reducerVersion
              field "Q" (string sequence)
              field "I" (eventIdText id)
              field "D" digest
              field "E" (renderEvent e) ]

    // ── 解析（重放路径）。

    /// 按 `tag:len:bytes` 切出第一个字段，返回 (值, 剩余)。
    /// len 是 UTF-8 字节数（§9.3 canonical bytes 承诺）；解析端必须按字节切——
    /// 不能按 UTF-16 code unit 切（Substring）：中文占 3 bytes / 1 code unit，会错位（P0-5）。
    /// 前缀（tag、长度数字、':') 全为 ASCII，UTF-16 索引与字节索引一致，故 contentStart 安全。
    let private parseField (tag: string) (input: string) : Result<string * string, string> =
        let prefix = tag + ":"

        if not (input.StartsWith(prefix, StringComparison.Ordinal)) then
            Error $"codec: expected field {tag}"
        else
            let rest = input.Substring(prefix.Length)

            match rest.IndexOf(':', StringComparison.Ordinal) with
            | -1 -> Error $"codec: field {tag} missing length"
            | i ->
                match Int32.TryParse(rest.Substring(0, i)) with
                | true, len when len >= 0 ->
                    // 长度是 UTF-8 字节数（§9.3 canonical bytes 承诺）；解析端必须按字节切——
                    // 不能按 UTF-16 code unit 切（Substring）：中文占 3 bytes / 1 code unit，会错位（P0-5）。
                    // 前缀（tag、长度数字、':') 全为 ASCII，UTF-16 索引与字节索引一致，故 contentStart 安全。
                    // [安全-中]：contentStart + len 必须用 int64 比较——
                    // len 接近 Int32.MaxValue 时 int 加法溢出为负会绕过边界检查（畸形行崩溃 DoS）。
                    let bytes = Encoding.UTF8.GetBytes rest
                    let contentStart = int64 (i + 1)

                    if contentStart + int64 len > int64 bytes.Length then
                        Error $"codec: field {tag} bad length"
                    else
                        let value = Encoding.UTF8.GetString(bytes, int contentStart, len)

                        let remaining =
                            Encoding.UTF8.GetString(
                                bytes,
                                int (contentStart + int64 len),
                                bytes.Length - (int contentStart + len)
                            )

                        Ok(value, remaining)
                | _ -> Error $"codec: field {tag} bad length"

    /// trailing bytes 拒绝（P1-1）：payload parser 的最终剩余必须为空。
    let private expectEnd (rest: string) (what: string) : Result<unit, string> =
        if rest = "" then
            Ok()
        else
            Error $"codec: trailing bytes after {what} ({rest.Length})"

    let private parseList (input: string) : Result<string list, string> =
        let rec go (s: string) (acc: string list) : Result<string list, string> =
            if s = "" then
                Ok(List.rev acc)
            else
                match parseField "x" s with
                | Ok(v, rest) -> go rest (v :: acc)
                | Error e -> Error e

        go input []

    let private parseScope (input: string) : Result<Scope * string, string> =
        match parseField "sc" input with
        | Error e -> Error e
        | Ok(c0, r0) ->
            match parseField "st" r0 with
            | Error e -> Error e
            | Ok(t0, r1) ->
                match parseField "sm" r1 with
                | Error e -> Error e
                | Ok(m0, r2) ->
                    match parseField "sp" r2 with
                    | Error e -> Error e
                    | Ok(p0, rest) ->
                        match noneOr c0, noneOr t0, noneOr m0, noneOr p0 with
                        | Ok c, Ok t, Ok m, Ok p ->
                            Ok(
                                { Content = c
                                  Time = t
                                  Modality = m
                                  Population = p },
                                rest
                            )
                        | Error e, _, _, _
                        | _, Error e, _, _
                        | _, _, Error e, _
                        | _, _, _, Error e -> Error e

    let private parseProvenance (input: string) : Result<Provenance * string, string> =
        match parseField "o" input with
        | Error e -> Error e
        | Ok(o, r0) ->
            match parseField "p" r0 with
            | Error e -> Error e
            | Ok(p, r1) ->
                // P0-1（138 版）：身份渲染不含 t（ProducedAt）——t 字段 optional，缺失视为空。
                match parseField "t" r1 with
                | Ok(t, rest) -> Ok(Provenance.create t o p, rest)
                | Error _ -> Ok(Provenance.create "" o p, r1)

    let rec private parseWitnesses
        (fields: string list)
        (acc: VerifierWitness list)
        : Result<VerifierWitness list, string> =
        match fields with
        | [] -> Ok(List.rev acc)
        | f :: fs ->
            match parseField "k" f with
            | Error e -> Error e
            | Ok(k, r0) ->
                let kind =
                    match k with
                    | "SCH" -> Ok VerifierKind.Schema
                    | "SRC" -> Ok VerifierKind.Source
                    | "INF" -> Ok VerifierKind.Inference
                    | "OBS" -> Ok VerifierKind.Observation
                    | _ -> Error $"codec: unknown verifier kind {k}"

                match kind with
                | Error e -> Error e
                | Ok knd ->
                    match parseField "v" r0 with
                    | Error e -> Error e
                    | Ok(v, r1) ->
                        match parseField "d" r1 with
                        | Error e -> Error e
                        | Ok(d, rest) when rest = "" -> parseWitnesses fs (VerifierWitness.fromFields knd v d :: acc)
                        | Ok(_, rest) -> Error $"codec: trailing bytes after witness ({rest.Length})"

    let private parseClaim (input: string) : Result<Claim * string, string> =
        match parseField "id" input with
        | Error e -> Error e
        | Ok(id, r0) ->
            match parseField "st" r0 with
            | Error e -> Error e
            | Ok(st, r1) ->
                match parseField "ro" r1 with
                | Error e -> Error e
                | Ok(ro, r2) ->
                    match parseField "so" r2 with
                    | Error e -> Error e
                    | Ok(so, r3) ->
                        match parseScope r3 with
                        | Error e -> Error e
                        | Ok(scope, r4) ->
                            match parseField "b" r4 with
                            | Error e -> Error e
                            | Ok(introducedBy, rest) ->
                                let role =
                                    match ro with
                                    | "DEF" -> Ok Definition
                                    | "ASM" -> Ok Assumption
                                    | "HYP" -> Ok Hypothesis
                                    | "ASR" -> Ok Assertion
                                    | "PRE" -> Ok Preference
                                    | _ -> Error $"codec: unknown role {ro}"

                                let source =
                                    match so with
                                    | "OBS" -> Ok ByObservation
                                    | "SRC" -> Ok BySourceSpan
                                    | "DRV" -> Ok ByDerivation
                                    | "USR" -> Ok ByUserStipulation
                                    | "ORC" -> Ok ByOracleProposal
                                    | _ -> Error $"codec: unknown proposal source {so}"

                                match role, source with
                                | Error e, _
                                | _, Error e -> Error e
                                | Ok rl, Ok sr ->
                                    Ok(
                                        { Id = ClaimId id
                                          Statement = st
                                          Role = rl
                                          Source = sr
                                          Scope = scope
                                          IntroducedBy = introducedBy },
                                        rest
                                    )

    let private parseWarrantData (input: string) : Result<WarrantData * string, string> =
        match parseField "id" input with
        | Error e -> Error e
        | Ok(id, r0) ->
            match parseField "c" r0 with
            | Error e -> Error e
            | Ok(c, r1) ->
                match parseField "p" r1 with
                | Error e -> Error e
                | Ok(p, r2) ->
                    match parseField "k" r2 with
                    | Error e -> Error e
                    | Ok(k, r3) ->
                        match parseField "r" r3 with
                        | Error e -> Error e
                        | Ok(rule, r4) ->
                            match parseField "s" r4 with
                            | Error e -> Error e
                            | Ok(s, r5) ->
                                match parseScope r5 with
                                | Error e -> Error e
                                | Ok(scope, r6) ->
                                    match parseProvenance r6 with
                                    | Error e -> Error e
                                    | Ok(origin, r7) ->
                                        match parseField "w" r7 with
                                        | Error e -> Error e
                                        | Ok(ws, r8) ->
                                            match parseList ws with
                                            | Error e -> Error e
                                            | Ok(witnessFields) ->
                                                match parseWitnesses witnessFields [] with
                                                | Error e -> Error e
                                                | Ok(witnesses) ->
                                                    match parseField "d" r8 with
                                                    | Error e -> Error e
                                                    | Ok(deps, r9) ->
                                                        match parseList deps with
                                                        | Error e -> Error e
                                                        | Ok(depFields) ->
                                                            match parseField "u" r9 with
                                                            | Error e -> Error e
                                                            | Ok(srcs, rest) ->
                                                                match parseList srcs with
                                                                | Error e -> Error e
                                                                | Ok(srcFields) ->
                                                                    let polarity =
                                                                        match p with
                                                                        | "SUP" -> Ok Supports
                                                                        | "OPP" -> Ok Opposes
                                                                        | _ -> Error $"codec: unknown polarity {p}"

                                                                    let kind =
                                                                        match k with
                                                                        | "OBS" -> Ok Observation
                                                                        | "SRC" -> Ok SourceSpan
                                                                        | "DRV" -> Ok Derivation
                                                                        | "USR" -> Ok UserStipulation
                                                                        | "ELI" -> Ok Elicitation
                                                                        | _ -> Error $"codec: unknown warrant kind {k}"

                                                                    let strength =
                                                                        match s with
                                                                        | "WEA" -> Ok Weak
                                                                        | "MOD" -> Ok Moderate
                                                                        | "STR" -> Ok Strong
                                                                        | _ -> Error $"codec: unknown strength {s}"

                                                                    match polarity, kind, strength with
                                                                    | Error e, _, _
                                                                    | _, Error e, _
                                                                    | _, _, Error e -> Error e
                                                                    | Ok pl, Ok kd, Ok sg ->
                                                                        // P0-1（138 版）：身份渲染不含 b（IntroducedBy）——optional，缺失视为空。
                                                                        match parseField "b" rest with
                                                                        | Ok(introducedBy, restAfter) ->
                                                                            Ok(
                                                                                { Id = WarrantId id
                                                                                  ClaimId = ClaimId c
                                                                                  Polarity = pl
                                                                                  Kind = kd
                                                                                  Rule = rule
                                                                                  Strength = sg
                                                                                  Scope = scope
                                                                                  Origin = origin
                                                                                  VerifierWitnesses = witnesses
                                                                                  DependencyWarrantIds =
                                                                                    depFields |> List.map WarrantId
                                                                                  UltimateSourceIds =
                                                                                    srcFields |> List.map SourceId
                                                                                  IntroducedBy = introducedBy },
                                                                                restAfter
                                                                            )
                                                                        // P0-1（138 版）：b 字段 optional——缺失即 IntroducedBy="" 且无剩余消费。
                                                                        | Error _ ->
                                                                            Ok(
                                                                                { Id = WarrantId id
                                                                                  ClaimId = ClaimId c
                                                                                  Polarity = pl
                                                                                  Kind = kd
                                                                                  Rule = rule
                                                                                  Strength = sg
                                                                                  Scope = scope
                                                                                  Origin = origin
                                                                                  VerifierWitnesses = witnesses
                                                                                  DependencyWarrantIds =
                                                                                    depFields |> List.map WarrantId
                                                                                  UltimateSourceIds =
                                                                                    srcFields |> List.map SourceId
                                                                                  IntroducedBy = "" },
                                                                                rest
                                                                            )

    let private parseEpisode (input: string) : Result<MethodEpisode * string, string> =
        match parseField "id" input with
        | Error e -> Error e
        | Ok(id, r0) ->
            match parseField "m" r0 with
            | Error e -> Error e
            | Ok(m, r1) ->
                match parseField "o" r1 with
                | Error e -> Error e
                | Ok(o, r2) ->
                    match parseField "i" r2 with
                    | Error e -> Error e
                    | Ok(inputs, r3) ->
                        match parseField "c" r3 with
                        | Error e -> Error e
                        | Ok(cands, r4) ->
                            match parseField "a" r4 with
                            | Error e -> Error e
                            | Ok(accs, r5) ->
                                match parseField "r" r5 with
                                | Error e -> Error e
                                | Ok(rejs, rest) ->
                                    match parseList inputs with
                                    | Error e -> Error e
                                    | Ok(il) ->
                                        match parseList cands with
                                        | Error e -> Error e
                                        | Ok(cl) ->
                                            match parseList accs with
                                            | Error e -> Error e
                                            | Ok(al) ->
                                                match parseList rejs with
                                                | Error e -> Error e
                                                | Ok(rl) ->
                                                    Ok(
                                                        { Id = EpisodeId id
                                                          MethodId = m
                                                          ObligationId = o
                                                          InputDigests = il
                                                          CandidateDigests = cl
                                                          AcceptedDigests = al
                                                          RejectedDigests = rl },
                                                        rest
                                                    )

    let private parseOutcome (s: string) : Result<SearchOutcome, string> =
        if s = "NH" then
            Ok NoHit
        elif s.StartsWith("HI", StringComparison.Ordinal) then
            parseField "d" (s.Substring(2)) |> Result.map (fun (d, _) -> Hit d)
        else
            Error $"codec: unknown search outcome {s}"

    let private parseCoverage (s: string) : Result<UnknownCoverage, string> =
        if s = "OP" then
            Ok Open
        elif s = "CC" then
            Ok ClaimedComplete
        elif s.StartsWith("VF", StringComparison.Ordinal) then
            parseField "d" (s.Substring(2)) |> Result.map (fun (d, _) -> VerifiedFinite d)
        elif s.StartsWith("UA", StringComparison.Ordinal) then
            parseField "r" (s.Substring(2))
            |> Result.map (fun (r, _) -> UserAssumedComplete r)
        else
            Error $"codec: unknown coverage {s}"

    /// 把 canonical 负载还原为事件（受信任重放路径）。
    /// internal（P0-1 137 版）：公开 decode 等于"反序列化铸造口"——外部可手工拼 canonical 行
    /// 获得本无法构造的 Warrant（经 fromFields 恢复 witness）。解码只允许程序集内
    /// （Kernel.replay/Report.conclude）与测试（InternalsVisibleTo）；Journal adapter 只存取
    /// opaque bytes，不接触领域事件。
    /// tag 是裸字符前缀（"R"、"OC"、"NP"…），后面直接接长度前缀字段——
    /// 不能用 IndexOf(':') 切 tag（`"R" + field "d" d` 里 ':' 属于字段，tag 会变成 "Rd"）。
    /// 匹配必须最长优先：RL 先于 R、NP 先于 N、MC/MF 先于 M、SW 先于 S。
    /// 事件负载解析的统一收尾：解析出的最终剩余必须为空（P1-1 trailing bytes 拒绝）。
    /// 参数顺序适配管道：`parseField ... |> withEnd "事件名"`。
    let private withEnd (what: string) (r: Result<'a * string, string>) : Result<'a, string> =
        match r with
        | Error e -> Error e
        | Ok(v, rest) ->
            match expectEnd rest what with
            | Ok() -> Ok v
            | Error e -> Error e

    let internal decodePayload (s: string) : Result<MeditationEvent, string> =
        // 参数化 partial active pattern：tag 是参数，input 是 match 输入（最后一个参数由 match 提供）。
        let (|Body|_|) (tag: string) (input: string) : string option =
            if input.StartsWith(tag, StringComparison.Ordinal) then
                Some(input.Substring(tag.Length))
            else
                None

        match s with
        | Body "RL" body ->
            match parseField "i" body with
            | Error e -> Error e
            | Ok(id, r) ->
                parseField "d" r
                |> withEnd "RelationRecorded"
                |> Result.map (fun d -> RelationRecorded(id, d))
        | Body "NP" body ->
            match parseField "o" body with
            | Error e -> Error e
            | Ok(o, r) ->
                parseField "k" r
                |> withEnd "NoProgress"
                |> Result.map (fun k -> NoProgress(o, k))
        | Body "MC" body ->
            parseField "d" body
            |> withEnd "MeditationCompleted"
            |> Result.map MeditationCompleted
        | Body "MF" body -> parseField "r" body |> withEnd "MeditationFailed" |> Result.map MeditationFailed
        | Body "SW" body -> withEnd "SweepCompleted" (Ok((), body)) |> Result.map (fun () -> SweepCompleted)
        | Body "OC" body ->
            parseField "k" body
            |> withEnd "OracleInvocationClaimed"
            |> Result.map OracleInvocationClaimed
        | Body "OA" body ->
            match parseField "k" body with
            | Error e -> Error e
            | Ok(k, r) ->
                parseField "d" r
                |> withEnd "OracleInvocationAccepted"
                |> Result.map (fun d -> OracleInvocationAccepted(k, d))
        | Body "OR" body ->
            match parseField "k" body with
            | Error e -> Error e
            | Ok(k, r) ->
                parseField "r" r
                |> withEnd "OracleAnswerRejected"
                |> Result.map (fun reason -> OracleAnswerRejected { InvocationKey = k; Reason = reason })
        | Body "R" body ->
            parseField "d" body
            |> withEnd "MeditationRequested"
            |> Result.map MeditationRequested
        | Body "C" body -> parseClaim body |> withEnd "ClaimFramed" |> Result.map ClaimFramed
        | Body "W" body ->
            match parseWarrantData body with
            | Error e -> Error e
            | Ok(data, rest) ->
                match expectEnd rest "ContributionAccepted" with
                | Error e -> Error e
                | Ok() ->
                    // P0-9：恢复路径校验 WarrantId 派生（与 fold 对称，拒绝日志中的任意 ID）。
                    if data.Id <> warrantIdOfData data then
                        Error "codec: ContributionAccepted WarrantId does not match warrant content"
                    else
                        Warrant.create data
                        |> Result.mapError (fun e -> $"codec: {e}")
                        |> Result.map ContributionAccepted
        | Body "E" body ->
            match parseField "i" body with
            | Error e -> Error e
            | Ok(id, r) ->
                parseField "d" r
                |> withEnd "EvidenceObserved"
                |> Result.map (fun d -> EvidenceObserved(id, d))
        | Body "H" body ->
            match parseField "i" body with
            | Error e -> Error e
            | Ok(id, r) ->
                parseField "d" r
                |> withEnd "HypothesisRecorded"
                |> Result.map (fun d -> HypothesisRecorded(id, d))
        | Body "N" body ->
            match parseField "i" body with
            | Error e -> Error e
            | Ok(id, r) ->
                parseField "d" r
                |> withEnd "ConceptRecorded"
                |> Result.map (fun d -> ConceptRecorded(id, d))
        | Body "X" body ->
            match parseField "i" body with
            | Error e -> Error e
            | Ok(id, r) ->
                parseField "d" r
                |> withEnd "CounterexampleRecorded"
                |> Result.map (fun d -> CounterexampleRecorded(id, d))
        | Body "U" body ->
            match parseField "id" body with
            | Error e -> Error e
            | Ok(id, r0) ->
                match parseField "d" r0 with
                | Error e -> Error e
                | Ok(desc, r1) ->
                    match parseField "c" r1 with
                    | Error e -> Error e
                    | Ok(cov, rest) ->
                        match expectEnd rest "UnknownRegionUpdated" with
                        | Error e -> Error e
                        | Ok() ->
                            parseCoverage cov
                            |> Result.map (fun c ->
                                UnknownRegionUpdated
                                    { Id = UnknownId id
                                      Description = desc
                                      Coverage = c })
        | Body "S" body ->
            match parseField "o" body with
            | Error e -> Error e
            | Ok(o, r0) ->
                match parseField "c" r0 with
                | Error e -> Error e
                | Ok(c, r1) ->
                    match parseField "n" r1 with
                    | Error e -> Error e
                    | Ok(n, rest) ->
                        match expectEnd rest "SearchAttempted" with
                        | Error e -> Error e
                        | Ok() ->
                            match Int32.TryParse n with
                            | true, seqNo ->
                                parseOutcome c
                                |> Result.map (fun oc ->
                                    SearchAttempted
                                        { ObligationId = o
                                          Outcome = oc
                                          Sequence = seqNo })
                            | _ -> Error $"codec: bad sequence {n}"
        | Body "M" body -> parseEpisode body |> withEnd "EpisodeRecorded" |> Result.map EpisodeRecorded
        | Body "A" body ->
            match parseField "o" body with
            | Error e -> Error e
            | Ok(o, r0) ->
                match parseField "d" r0 with
                | Error e -> Error e
                | Ok(d, r1) ->
                    match parseField "p" r1 with
                    | Error e -> Error e
                    | Ok(p, rest) ->
                        match expectEnd rest "AttemptRecorded" with
                        | Error e -> Error e
                        | Ok() ->
                            Ok(
                                AttemptRecorded
                                    { ObligationId = o
                                      LedgerDigest = d
                                      PolicyVersion = p }
                            )
        | Body "K" body ->
            match parseField "n" body with
            | Error e -> Error e
            | Ok(n, rest) ->
                match expectEnd rest "CreditsConsumed" with
                | Error e -> Error e
                | Ok() ->
                    match Int32.TryParse n with
                    | true, amt -> Ok(CreditsConsumed amt)
                    | _ -> Error $"codec: bad amount {n}"
        | _ -> Error $"codec: unknown event tag {s}"

    /// 解析 envelope 行并校验完整性（EventId/PayloadDigest 与内容一致，演算 §9.1）。
    /// internal（P0-1 137 版）：与 decodePayload 同因——公开即反序列化铸造口。
    let internal decode (line: string) : Result<EventEnvelope, string> =
        match parseField "V" line with
        | Error e -> Error e
        | Ok(v, r0) ->
            match parseField "P" r0 with
            | Error e -> Error e
            | Ok(p, r1) ->
                match parseField "R" r1 with
                | Error e -> Error e
                | Ok(r, r2) ->
                    match parseField "Q" r2 with
                    | Error e -> Error e
                    | Ok(q, r3) ->
                        match parseField "I" r3 with
                        | Error e -> Error e
                        | Ok(i, r4) ->
                            match parseField "D" r4 with
                            | Error e -> Error e
                            | Ok(d, r5) ->
                                match parseField "E" r5 with
                                | Error e -> Error e
                                | Ok(payload, rest) when rest = "" ->
                                    match Int32.TryParse v, Int32.TryParse q with
                                    | (true, schemaVersion), (true, sequence) ->
                                        match decodePayload payload with
                                        | Error e -> Error e
                                        | Ok(evt) ->
                                            let expectedId = eventId schemaVersion p r sequence evt
                                            let expectedDigest = payloadDigest evt

                                            if eventIdText expectedId <> i then
                                                Error "codec: EventId mismatch (line corrupted or version drift)"
                                            elif expectedDigest <> d then
                                                Error "codec: PayloadDigest mismatch"
                                            else
                                                // 其他 3：round-trip canonical 校验——重新编码必须逐字节
                                                // 等于原行（拒绝嵌套 trailing、长度前导零、+1 等非规范数字、
                                                // 多字节等价形式——多种字节形态解码成同一事件 = 非法）。
                                                let reencoded = encode schemaVersion p r sequence evt

                                                if reencoded <> line then
                                                    Error "codec: non-canonical envelope"
                                                else
                                                    Ok
                                                        { EventId = expectedId
                                                          SchemaVersion = schemaVersion
                                                          PolicyVersion = p
                                                          ReducerVersion = r
                                                          Sequence = sequence
                                                          Payload = evt
                                                          PayloadDigest = d }
                                    | _ -> Error "codec: bad schema version or sequence"
                                | Ok(_, rest) -> Error $"codec: trailing bytes after payload ({rest.Length})"

module ClaimId =
    /// F1 Leibniz 同一性（§41.2）：ClaimId 由规范化 (statement, scope) 的 canonical bytes 派生。
    /// 禁止 GUID、进程 hash、当前时间参与（P0 固定裁决 6）。
    let ofProposition (statement: string) (scope: Scope) : ClaimId =
        ClaimId(EventCodec.sha256Hex (EventCodec.field "st" statement + EventCodec.renderScope scope))

/// §9.2 幂等规则：同 key 已存在时，内容相同 = 幂等（Ok），内容不同 = DuplicateEvent（覆盖写企图）。
let private upsertChecked (errorTag: string) (map: Map<'k, 'v>) (key: 'k) (value: 'v) : Result<Map<'k, 'v>, FoldError> =
    match map.TryFind key with
    | Some existing when existing <> value -> Error(DuplicateEvent errorTag)
    | _ -> Ok(map.Add(key, value))

/// 认识进展标记（P0-4）：epistemic 字段真正变化时清零 Stalls——"连续无进展"语义
/// （无进展 → 有进展 → 无进展 不累计），不是累计无进展。值未变（幂等重复提交）不算进展。
let private progressIfChanged (ledger: MeditationLedger) (next: MeditationLedger) : MeditationLedger =
    if next = ledger then
        ledger
    else
        { next with
            ResourceUsage = { next.ResourceUsage with Stalls = 0 } }

/// C1 的 β 规则：fold S e ≡ R(S, e)。
/// T5 确定性侧条件：同事件 bytes + 同 reducer version + 同 policy version ⇒ 同 digest。
/// S3：fold 不天然幂等——重复 EventId 由 journal 拒绝（S2），此处同 key 异内容兜底报错。
/// 第二道防线：ContributionAccepted 重验 §5 统一侧条件，不信任调用方。
/// 每个成功折叠使 EventCount + 1（EventId 序列分量的来源）。
let fold (ledger: MeditationLedger) (event: MeditationEvent) : Result<MeditationLedger, FoldError> =
    let withCount (r: Result<MeditationLedger, FoldError>) : Result<MeditationLedger, FoldError> =
        match r with
        | Error e -> Error e
        // [安全-低]：EventCount 溢出守卫（重放超长日志不得回绕为负）。
        | Ok l when l.EventCount = System.Int32.MaxValue -> Error(InvalidTransition "fold: EventCount overflow")
        | Ok l -> Ok { l with EventCount = l.EventCount + 1 }

    withCount (
        match event with
        // 其他 1：MeditationCompleted 是严格终态——其后任何事件（含第二条 MC）非法。
        | MeditationCompleted _ when ledger.CompletedReportDigest.IsSome ->
            Error(InvalidTransition "fold: MeditationCompleted already recorded")
        | _ when ledger.CompletedReportDigest.IsSome -> Error(InvalidTransition "fold: event after MeditationCompleted")
        | MeditationRequested _ when ledger.EventCount > 0 ->
            Error(InvalidTransition "MeditationRequested: must be the first event (and only once)")
        | MeditationRequested _ -> Ok ledger
        | ClaimFramed claim ->
            // 其他 4：身份不变量在 fold 重验——ClaimId 必须由 (statement, scope) 派生（§41.2）。
            if claim.Id <> ClaimId.ofProposition claim.Statement claim.Scope then
                Error(InvalidTransition "ClaimFramed: ClaimId does not match proposition identity")
            else
                // 提交时填充 IntroducedBy（停证可独立重放的依据；调用方内存态为空串）。
                let committed =
                    { claim with
                        IntroducedBy = EventCodec.payloadDigest event }

                upsertChecked "ClaimFramed" ledger.Claims claim.Id committed
                |> Result.map (fun m -> progressIfChanged ledger { ledger with Claims = m })
        | OracleInvocationClaimed _ -> Ok ledger
        | OracleInvocationAccepted(key, digest) ->
            // P0-5：同一 invocation key 只允许一个 accepted transcript digest——
            // store 与 journal 崩溃后分叉（key → digest2）在此被拒，非法状态。
            match ledger.AcceptedTranscripts.TryFind key with
            | Some existing when existing <> digest ->
                Error(InvalidTransition $"OracleInvocationAccepted: same invocation key with different digest (S2)")
            // P1-8：同 key 同 digest → 完全 no-op（OracleCalls 是唯一 oracle 调用数，不是事件折叠数）——
            // 重放/幂等重复不递增计数。
            | Some _ -> Ok ledger
            | None ->
                // P1-3：计数溢出防御（重放不可信日志时不得溢出回绕）。
                if ledger.ResourceUsage.OracleCalls = System.Int32.MaxValue then
                    Error(InvalidTransition "OracleInvocationAccepted: OracleCalls overflow")
                else
                    Ok
                        { ledger with
                            AcceptedTranscripts = ledger.AcceptedTranscripts.Add(key, digest)
                            ResourceUsage =
                                { ledger.ResourceUsage with
                                    OracleCalls = ledger.ResourceUsage.OracleCalls + 1 } }
        | OracleAnswerRejected rejected ->
            // append 不覆盖：拒绝是历史事实（§9.2）。
            Ok
                { ledger with
                    Rejections = rejected :: ledger.Rejections }
        | ContributionAccepted warrant ->
            match Warrant.data warrant with
            | { VerifierWitnesses = [] } ->
                Error(InvalidTransition "ContributionAccepted: VerifierWitnesses must be non-empty")
            | { UltimateSourceIds = [] } ->
                Error(InvalidTransition "ContributionAccepted: UltimateSourceIds must be non-empty")
            // P0-2：fold 重验 witness/warrant kind 兼容性，不信任调用方。
            | data when not (Warrant.witnessesCompatible data.VerifierWitnesses data.Kind) ->
                Error(InvalidTransition "ContributionAccepted: verifier witness kind incompatible with warrant kind")
            // P0-4：fold 重验 verifierId 白名单（重放 fromFields 恢复的 witness 必须 kind/verifierId 匹配）。
            | data when
                data.VerifierWitnesses
                |> List.exists (fun w -> not (Warrant.verifierIdMatches w))
                ->
                Error(InvalidTransition "ContributionAccepted: witness verifierId not in policy whitelist")
            | data ->
                if not (ledger.Claims.ContainsKey data.ClaimId) then
                    Error(MissingClaim data.ClaimId)
                // P0-9：fold 重验 WarrantId 派生（不信任调用方/日志）。
                elif data.Id <> EventCodec.warrantIdOfData data then
                    Error(InvalidTransition "ContributionAccepted: WarrantId does not match warrant content")
                else
                    // 其他 4：warrant scope 必须与 claim scope 一致（跨 scope 推广必须经显式推导）；
                    // 依赖 warrant 必须已存在于账本（依赖簇可重放）。
                    let claimScope = (ledger.Claims.[data.ClaimId]).Scope

                    if data.Scope <> claimScope then
                        Error(InvalidTransition "ContributionAccepted: warrant scope does not match claim scope")
                    elif
                        data.DependencyWarrantIds
                        |> List.exists (fun id -> not (ledger.Warrants.ContainsKey id))
                    then
                        Error(InvalidTransition "ContributionAccepted: dependency warrant not in ledger")
                    else
                        // 提交时填充 IntroducedBy（停证可独立重放的依据；调用方内存态为空串）。
                        // P0-9：create 失败必须 fail closed（InvalidTransition），不得静默保存未提交版本。
                        match
                            Warrant.create
                                { data with
                                    IntroducedBy = EventCodec.payloadDigest event }
                        with
                        | Ok committed ->
                            upsertChecked "ContributionAccepted" ledger.Warrants data.Id committed
                            |> Result.map (fun m -> progressIfChanged ledger { ledger with Warrants = m })
                        | Error err -> Error(InvalidTransition $"ContributionAccepted: {err}")
        | EvidenceObserved(id, digest) ->
            upsertChecked "EvidenceObserved" ledger.Evidence id digest
            |> Result.map (fun m -> progressIfChanged ledger { ledger with Evidence = m })
        | HypothesisRecorded(id, digest) ->
            upsertChecked "HypothesisRecorded" ledger.Hypotheses id digest
            |> Result.map (fun m -> progressIfChanged ledger { ledger with Hypotheses = m })
        | ConceptRecorded(id, digest) ->
            upsertChecked "ConceptRecorded" ledger.Concepts id digest
            |> Result.map (fun m -> progressIfChanged ledger { ledger with Concepts = m })
        | RelationRecorded(id, digest) ->
            upsertChecked "RelationRecorded" ledger.Relations id digest
            |> Result.map (fun m -> progressIfChanged ledger { ledger with Relations = m })
        | CounterexampleRecorded(id, digest) ->
            upsertChecked "CounterexampleRecorded" ledger.Counterexamples id digest
            |> Result.map (fun m -> progressIfChanged ledger { ledger with Counterexamples = m })
        | UnknownRegionUpdated region ->
            // 非降级（§9.2）：Coverage 只能沿等级序上升；实际变化才算认识进展（P0-4）。
            match ledger.UnknownRegions.TryFind region.Id with
            | Some existing when existing = region -> Ok ledger
            | Some existing when coverageRank region.Coverage < coverageRank existing.Coverage ->
                Error(InvalidTransition "UnknownRegionUpdated: coverage downgrade")
            | _ ->
                Ok(
                    progressIfChanged
                        ledger
                        { ledger with
                            UnknownRegions = ledger.UnknownRegions.Add(region.Id, region) }
                )
        | SearchAttempted attempt ->
            Ok
                { ledger with
                    SearchAttempts = attempt :: ledger.SearchAttempts }
        | EpisodeRecorded episode ->
            upsertChecked "EpisodeRecorded" ledger.MethodEpisodes episode.Id episode
            |> Result.map (fun m -> { ledger with MethodEpisodes = m })
        | AttemptRecorded key ->
            Ok
                { ledger with
                    Attempts = Set.add key ledger.Attempts }
        | NoProgress _ ->
            // P1-3：溢出防御。
            if ledger.ResourceUsage.Stalls = System.Int32.MaxValue then
                Error(InvalidTransition "NoProgress: Stalls overflow")
            else
                Ok
                    { ledger with
                        ResourceUsage =
                            { ledger.ResourceUsage with
                                Stalls = ledger.ResourceUsage.Stalls + 1 } }
        | CreditsConsumed amount ->
            // P1-3：重放不可信日志时拒绝负/零/溢出金额——Budget.consume 的安全不能
            // 被事件重放路径绕过（评审：CreditsConsumed -100 会减少历史消耗）。
            if amount < 1 then
                Error(InvalidTransition $"CreditsConsumed: amount must be >= 1 (got {amount})")
            elif ledger.ResourceUsage.CreditsConsumed > System.Int32.MaxValue - amount then
                Error(InvalidTransition "CreditsConsumed: overflow")
            else
                Ok
                    { ledger with
                        ResourceUsage =
                            { ledger.ResourceUsage with
                                CreditsConsumed = ledger.ResourceUsage.CreditsConsumed + amount } }
        | SweepCompleted ->
            // P1-3：溢出防御。
            if ledger.ResourceUsage.NormalizationSweeps = System.Int32.MaxValue then
                Error(InvalidTransition "SweepCompleted: NormalizationSweeps overflow")
            else
                Ok
                    { ledger with
                        ResourceUsage =
                            { ledger.ResourceUsage with
                                NormalizationSweeps = ledger.ResourceUsage.NormalizationSweeps + 1 } }
        | MeditationCompleted d ->
            // P1-1：完成标记（恢复路径的幂等依据）；不改变认识/operational digest 之外的任何字段。
            Ok
                { ledger with
                    CompletedReportDigest = Some d }
        | MeditationFailed _ -> Ok ledger
    )

// ── §41.3：provenance 依赖簇。来源集相交连边，连通分量为簇。

/// 按 scope 查询 claim 的派生极性（§41.2）：只有 scope 兼容的 warrant 参与同一判断——
/// 跨 scope 推广必须经显式推导（§4.2 保守规则），此处不混算。
let polarityOf (ledger: MeditationLedger) (claimId: ClaimId) (scope: Scope) : PolarityState =
    let matching =
        ledger.Warrants
        |> Map.toList
        |> List.map snd
        |> List.filter (fun w -> Warrant.claimId w = claimId && Warrant.scope w = scope)

    polarityState
        (matching |> List.filter (fun w -> Warrant.polarity w = Supports))
        (matching |> List.filter (fun w -> Warrant.polarity w = Opposes))

/// provenance 缺失（UltimateSourceIds 为空）→ 并入同一"未知"簇：fail closed，
/// 不完整 provenance 不允许冒充独立证据。
let dependencyClusters (warrants: Warrant list) : WarrantId list list =
    let parent = Collections.Generic.Dictionary<WarrantId, WarrantId>()

    let rec find (x: WarrantId) : WarrantId =
        match parent.TryGetValue x with
        | true, p when p <> x ->
            let root = find p
            parent.[x] <- root
            root
        | true, p -> p
        | false, _ ->
            parent.[x] <- x
            x

    let union (a: WarrantId) (b: WarrantId) : unit =
        let ra = find a
        let rb = find b

        if ra <> rb then
            parent.[ra] <- rb

    warrants
    |> List.collect (fun w -> w |> Warrant.sources |> List.map (fun s -> s, Warrant.id w))
    |> List.groupBy fst
    |> List.iter (fun (_, group) ->
        match group |> List.map snd with
        | [] -> ()
        | first :: rest -> rest |> List.iter (union first))

    // 139 版评审 #4：同一 receipt（同一 witness 集）的所有派生 warrant 必须落同一依赖簇——
    // 外部把同一个 Accepted 复制成多个 source 不能伪造独立证据簇。
    warrants
    |> List.collect (fun w -> (Warrant.data w).VerifierWitnesses |> List.map (fun s -> s, Warrant.id w))
    |> List.groupBy fst
    |> List.iter (fun (_, group) ->
        match group |> List.map snd with
        | [] -> ()
        | first :: rest -> rest |> List.iter (union first))

    match warrants |> List.filter (fun w -> List.isEmpty (Warrant.sources w)) with
    | [] -> ()
    | first :: rest -> rest |> List.iter (fun w -> union (Warrant.id first) (Warrant.id w))

    warrants
    |> List.groupBy (fun w -> find (Warrant.id w))
    |> List.map (fun (_, group) -> group |> List.map Warrant.id)

// ── G 的应用：从 warrant 集合派生 grade（按 polarity 分别计算，§41.4）。
/// Strength→Reliability 的映射是版本化 policy 的一部分（§5.4 语义评价阶段的输入），
/// 随 policyVersion 发布；它不是 grade 与 strength 的隐式转换——是一维的初始赋值。
let gradeOfWarrant (w: Warrant) : EpistemicGrade =
    { Directness =
        (match Warrant.kind w with
         | Observation -> Direct
         | Derivation -> Derivational
         | _ -> Indirect)
      Reliability =
        (match Warrant.strength w with
         | Strong -> Confirmed
         | Moderate -> Corroborated
         | Weak -> Tentative)
      Independence = Clusters 1
      Coverage = OpenWorldCoverage
      Reproducibility = NotYetReplayed }

/// 集合 grade：逐维 meet + independence = 依赖簇数。支持/反对分别调用，不合并（§41.4）。
let gradeOfWarrants (warrants: Warrant list) : EpistemicGrade option =
    match warrants with
    | [] -> None
    | _ ->
        let met = warrants |> List.map gradeOfWarrant |> List.reduce Grade.meet
        let clusters = dependencyClusters warrants |> List.length

        Some
            { met with
                Independence = Clusters clusters }

// ── T5/C1 的不动点判据：规范化 sweep 要求 digest 不变即停（§9.2③）。
/// semanticDigest 覆盖账本全部字段（演算 §9.3）：任何认识意义的差异都改变 digest。
let semanticDigest (ledger: MeditationLedger) : string =
    let claims =
        ledger.Claims
        |> Map.toList
        |> List.map (fun (id, c) -> EventCodec.field "C.id" (claimIdText id) + EventCodec.renderClaim c)
        |> String.concat ""

    let warrants =
        ledger.Warrants
        |> Map.toList
        |> List.map (fun (id, w) ->
            EventCodec.field "W.id" (warrantIdText id)
            + EventCodec.renderWarrantData (Warrant.data w))
        |> String.concat ""

    let unknowns =
        ledger.UnknownRegions
        |> Map.toList
        |> List.map (fun (id, u) ->
            EventCodec.field "U.id" (unknownIdText id)
            + EventCodec.field "U.d" u.Description
            + EventCodec.field "U.c" (EventCodec.renderCoverage u.Coverage))
        |> String.concat ""

    let episodes =
        ledger.MethodEpisodes
        |> Map.toList
        |> List.map (fun (id, ep) ->
            EventCodec.field
                "M.id"
                (match id with
                 | EpisodeId i -> i)
            + EventCodec.renderEpisode ep)
        |> String.concat ""

    let rejections =
        ledger.Rejections
        |> List.map (fun r -> EventCodec.field "R.k" r.InvocationKey + EventCodec.field "R.r" r.Reason)
        |> String.concat ""

    let searches =
        ledger.SearchAttempts
        |> List.map (fun a ->
            EventCodec.field "S.o" a.ObligationId
            + EventCodec.field "S.c" (EventCodec.renderOutcome a.Outcome)
            + EventCodec.field "S.n" (string a.Sequence))
        |> String.concat ""

    let attempts =
        ledger.Attempts
        |> Set.toList
        |> List.map (fun k ->
            EventCodec.field "A.o" k.ObligationId
            + EventCodec.field "A.d" k.LedgerDigest
            + EventCodec.field "A.p" k.PolicyVersion)
        |> String.concat ""

    let usage =
        EventCodec.field "U.oc" (string ledger.ResourceUsage.OracleCalls)
        + EventCodec.field "U.ns" (string ledger.ResourceUsage.NormalizationSweeps)
        + EventCodec.field "U.cc" (string ledger.ResourceUsage.CreditsConsumed)
        + EventCodec.field "U.st" (string ledger.ResourceUsage.Stalls)

    let canonical =
        String.concat
            ""
            [ claims
              warrants
              (ledger.Hypotheses
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "H.k" k + EventCodec.field "H.v" v)
               |> String.concat "")
              (ledger.Concepts
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "N.k" k + EventCodec.field "N.v" v)
               |> String.concat "")
              (ledger.Relations
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "RL.k" k + EventCodec.field "RL.v" v)
               |> String.concat "")
              (ledger.Evidence
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "E.k" k + EventCodec.field "E.v" v)
               |> String.concat "")
              (ledger.Counterexamples
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "X.k" k + EventCodec.field "X.v" v)
               |> String.concat "")
              unknowns
              episodes
              rejections
              searches
              attempts
              usage ]

    EventCodec.sha256Hex canonical

/// 认识 digest（P0-4）：只含影响认识结论的字段——Claims/Warrants/UnknownRegions/
/// Evidence/Hypotheses/Concepts/Relations/Counterexamples。
/// 不含 Attempts/SearchAttempts/Rejections/ResourceUsage/EventCount（operational bookkeeping）：
/// ① attempt key 用本 digest——执行 attempt 的记账不再使相同义务获得新 key（去重不被绕过）；
/// ② 进展判断（Kernel.seek 的 changed）也用本 digest——OracleCalls/搜索历史变化不算认识进展。
let epistemicDigest (ledger: MeditationLedger) : string =
    let claims =
        ledger.Claims
        |> Map.toList
        |> List.map (fun (id, c) -> EventCodec.field "C.id" (claimIdText id) + EventCodec.renderClaim c)
        |> String.concat ""

    let warrants =
        ledger.Warrants
        |> Map.toList
        |> List.map (fun (id, w) ->
            EventCodec.field "W.id" (warrantIdText id)
            + EventCodec.renderWarrantData (Warrant.data w))
        |> String.concat ""

    let unknowns =
        ledger.UnknownRegions
        |> Map.toList
        |> List.map (fun (id, u) ->
            EventCodec.field "U.id" (unknownIdText id)
            + EventCodec.field "U.d" u.Description
            + EventCodec.field "U.c" (EventCodec.renderCoverage u.Coverage))
        |> String.concat ""

    let canonical =
        String.concat
            ""
            [ claims
              warrants
              (ledger.Hypotheses
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "H.k" k + EventCodec.field "H.v" v)
               |> String.concat "")
              (ledger.Concepts
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "N.k" k + EventCodec.field "N.v" v)
               |> String.concat "")
              (ledger.Relations
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "RL.k" k + EventCodec.field "RL.v" v)
               |> String.concat "")
              (ledger.Evidence
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "E.k" k + EventCodec.field "E.v" v)
               |> String.concat "")
              (ledger.Counterexamples
               |> Map.toList
               |> List.map (fun (k, v) -> EventCodec.field "X.k" k + EventCodec.field "X.v" v)
               |> String.concat "")
              unknowns ]

    EventCodec.sha256Hex canonical

/// §11 进展保证：attempt key = (obligationId, epistemicDigest, policyVersion)（P0-4）。
/// 用认识 digest 而非全量 digest：bookkeeping（Attempts/CreditsConsumed/OracleCalls）变化
/// 不产生新 key——相同义务在相同认识状态下不可重复执行（去重不被绕过）。
let attemptKey (obligationId: string) (ledger: MeditationLedger) (policyVersion: string) : AttemptKey =
    { ObligationId = obligationId
      LedgerDigest = epistemicDigest ledger
      PolicyVersion = policyVersion }

/// 重放：把事件序列 fold 成账本；首个错误即失败（fail closed，不跳过后续行）。
let foldAll (ledger: MeditationLedger) (events: MeditationEvent list) : Result<MeditationLedger, FoldError> =
    List.fold
        (fun acc e ->
            match acc with
            | Error err -> Error err
            | Ok l -> fold l e)
        (Ok ledger)
        events

/// P0-1：封闭验证操作。外部调用方只能提交待验证材料与协议回执，
/// 不能拿到权柄本身（Verifiers.*、VerifierWitness.issue/fromFields、
/// Accepted/Validated/Constructed.create 均为 internal）。
/// witness 的 VerifierId 由程序集内权柄派生，调用方无法书写。
module Verification =
    /// Observation verifier（P0-2）：协议执行回执由程序集内验证操作产生——internal，
    /// 外部不能以任意字符串冒充"协议已执行"；产出 Accepted<Claim> 绑定具体 claim。
    /// 程序集内（可信验证器/测试）才可调用；外部经 meditate 的装配链使用。
    let internal observe (protocolDigest: string) (claim: Claim) : Result<Accepted<Claim>, RejectionReason> =
        match
            Accepted.create
                Verifiers.observation
                [ VerifierWitness.issue Verifiers.observation VerifierKind.Observation protocolDigest ]
                claim
        with
        | Ok accepted -> Ok accepted
        | Error reason -> Error(RejectionReason.PolicyViolation reason)

    /// Schema verifier（P0-2）：结构检查由程序集内验证操作产生——internal。
    let internal conforms (schemaDigest: string) (value: 'a) : Result<Accepted<'a>, RejectionReason> =
        match
            Accepted.create
                Verifiers.schema
                [ VerifierWitness.issue Verifiers.schema VerifierKind.Schema schemaDigest ]
                value
        with
        | Ok accepted -> Ok accepted
        | Error reason -> Error(RejectionReason.PolicyViolation reason)

/// P0-2（138 版）：安全公共验证服务（方案 A——公共 verifier 服务）。
/// 外部调用者（Release、无 IVT）提交协议回执与 claim，由程序集内验证操作签发 witness
/// （observe 的 internal 实现）；外部无需也不可访问权柄。这是封闭 witness 后保留的
/// 合法 ClaimTest 完成路径。架构选择记录于 THREAT_MODEL.md §2。
module PublicVerification =
    let observe (protocolDigest: string) (claim: Claim) : Result<Accepted<Claim>, string> =
        match Verification.observe protocolDigest claim with
        | Ok a -> Ok a
        | Error r -> Error(string r)

/// P0-2：公共观察 warrant 构造——消费 Accepted<Claim>（经 PublicVerification.observe
/// 产生，witness 由程序集内验证操作签发，外部不能伪造）→ Observation warrant。
/// 139 版：**接受 producedAt**（时间由调用方从环境时钟读取——Warrant opaque 后外部无
/// 其他填充途径，评审 #5）；**固定 Moderate 强度**（外部不得单方面把观察标为 Strong）。
/// 定位：特权 attestation port（THREAT_MODEL §2.9）——宿主只在受信任上下文暴露；
/// polarity 由观察结论决定（Supports/Opposes 两个构造，同一 receipt 语义）。
/// claim 存在性由 fold 的 MissingClaim 检查兜底。
/// 定义在 EventCodec 之后（需要 warrantIdOfData）。
let private warrantFromObservationCore
    (polarity: Polarity)
    (producedAt: string)
    (accepted: Accepted<Claim>)
    (sources: SourceId list)
    (scope: Scope)
    : Result<Warrant, string> =
    let claim = Accepted.value accepted

    let body =
        { Id = WarrantId ""
          ClaimId = claim.Id
          Polarity = polarity
          Kind = Observation
          Rule = "observation/v1"
          Strength = Moderate
          Scope = scope
          Origin = Provenance.create producedAt "public-observation/v1" "observation/v1" // ProducedAt 由调用方时钟注入；不参与身份
          VerifierWitnesses = Accepted.witnesses accepted
          DependencyWarrantIds = []
          UltimateSourceIds = sources
          IntroducedBy = "" }

    Warrant.create
        { body with
            Id = EventCodec.warrantIdOfData body }

/// 支持观察（139 版：producedAt 来自环境时钟）。
let warrantFromObservation
    (producedAt: string)
    (accepted: Accepted<Claim>)
    (sources: SourceId list)
    (scope: Scope)
    : Result<Warrant, string> =
    warrantFromObservationCore Supports producedAt accepted sources scope

/// 反对观察（139 版评审 #3：外部经公共 API 完成双侧 ClaimTest 的合法反对路径；
/// polarity 由调用方声明的观察结论决定——attestation port 语义，宿主信任上下文）。
let warrantFromObservationOpposing
    (producedAt: string)
    (accepted: Accepted<Claim>)
    (sources: SourceId list)
    (scope: Scope)
    : Result<Warrant, string> =
    warrantFromObservationCore Opposes producedAt accepted sources scope
