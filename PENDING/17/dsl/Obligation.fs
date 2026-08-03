// Meditator DSL — 义务派生（演算 §11 meta 层）。
// agenda 是纯派生视图，不持久化（§6.4）；同一 ledger 必派生同一义务——
// 推导义务的纯函数是控制确定性的载体（演算 T5 的控制侧）。
// 编译顺序：5（依赖 Ledger）。
module Meditator.Obligation

open Meditator.Ledger

/// 答案契约（§7.1.1）：输出合同，不是真理强度总序。
type AnswerGoal =
    | Explanation
    | Comparison
    | Decision
    | ClaimTest
    | Forecast
    | Diagnosis
    | Brainstorm

/// requestedEvidenceMode：合同目标；不改变实际 grade（§11.1）。
type EvidenceMode =
    | Exploratory
    | Qualitative
    | SourceGrounded
    | Empirical
    | Probabilistic

/// 报告章节（P0-4 137 版）：合同要求的是结构化章节，不是文本子串——
/// 章节存在性按结构检查（对应字段非空），禁止文本近似冒充。
type ReportSection =
    | ExecutiveSummary
    | Findings
    | Counterpoints
    | Dependencies
    | EvidenceLimitations
    | Unknowns
    | Recommendations

type AnswerContract =
    { Goal: AnswerGoal
      RequestedEvidenceMode: EvidenceMode
      Scope: Scope
      RequiredSections: ReportSection list
      UnacceptableClaims: string list
      TargetStatement: string option } // P0-4（138 版）：ClaimTest 的命题目标——Kernel 验证账本中存在该主张

type MeditationRequest =
    { Intent: string
      Contract: AnswerContract option
      MethodHints: string list } // 只影响同优先级义务的初始排序；不得绕门禁（§41.8）

/// AnswerGoal 的显式持久化标签（P1-5）：不依赖 DU ToString（编译重构不得改变身份）。
let goalTag (g: AnswerGoal) : string =
    match g with
    | Explanation -> "EXP"
    | Comparison -> "CMP"
    | Decision -> "DEC"
    | ClaimTest -> "CLM"
    | Forecast -> "FCT"
    | Diagnosis -> "DGN"
    | Brainstorm -> "BRN"

/// EvidenceMode 的显式持久化标签（P1-5）。
let modeTag (m: EvidenceMode) : string =
    match m with
    | Exploratory -> "EXP"
    | Qualitative -> "QLT"
    | SourceGrounded -> "SRC"
    | Empirical -> "EMP"
    | Probabilistic -> "PRO"

/// ReportSection 的显式持久化标签（P0-4 137 版）。
let sectionTag (s: ReportSection) : string =
    match s with
    | ExecutiveSummary -> "EXS"
    | Findings -> "FIN"
    | Counterpoints -> "CTP"
    | Dependencies -> "DEP"
    | EvidenceLimitations -> "LIM"
    | Unknowns -> "UNK"
    | Recommendations -> "REC"

/// canonicalRequest（P0-1）：MeditationRequest 的权威 canonical 编码——
/// requestDigest 与 contractDigest 共用同一实现（单一权威，禁止两处各自拼接）。
/// 覆盖完整 AnswerContract（goal/mode/scope/RequiredSections/UnacceptableClaims）：
/// 用户改变必需章节或禁止主张后，旧 journal/旧契约证明不得复用。
/// 列表逐项长度前缀编码（field "s"），不做分隔符拼接。
let canonicalRequest (request: MeditationRequest) : string =
    EventCodec.field "i" request.Intent
    + (match request.Contract with
       | None -> ""
       | Some c ->
           EventCodec.field "g" (goalTag c.Goal)
           + EventCodec.field "m" (modeTag c.RequestedEvidenceMode)
           + EventCodec.renderScope c.Scope
           + EventCodec.field
               "rs"
               (String.concat
                   ""
                   (List.map
                       (EventCodec.field "s")
                       (c.RequiredSections |> List.map sectionTag |> List.sort |> List.distinct)))
           + EventCodec.field
               "uc"
               (String.concat "" (List.map (EventCodec.field "s") (c.UnacceptableClaims |> List.sort |> List.distinct)))
           + EventCodec.field
               "ts"
               (match c.TargetStatement with
                | None -> ""
                | Some t -> EventCodec.field "t" t))
// 139 版评审 #15：MethodHints 不进请求身份（身份稳定，与行为一致）——见 selectDeterministically 注释。
// 空串保持（字段结构稳定：i/contract/… 后续扩展在尾部追加）。

/// 义务种类（§6.1）：类型化的认识论义务，不是"相关问题"。
type ObligationKind =
    | FrameClaim
    | ClarifyScope
    | GenerateSupport
    | GenerateOpposition
    | GenerateConfounder
    | CheckMeasurement
    | NormalizeConcept
    | ReviewRelation
    | JudgeEffect
    | GroundEvidence
    | CheckCounterexample
    | ExpandUnknown
    | SynthesizeAnswer

/// 义务种类显式标签（P0-8 137 版）：义务 ID 进入 AttemptRecorded（持久化身份），
/// 不能依赖 DU ToString（编译重构不得改变 attempt key 语义）。
let obligationKindTag (k: ObligationKind) : string =
    match k with
    | FrameClaim -> "FC"
    | ClarifyScope -> "CS"
    | GenerateSupport -> "GS"
    | GenerateOpposition -> "GO"
    | GenerateConfounder -> "GC"
    | CheckMeasurement -> "CM"
    | NormalizeConcept -> "NC"
    | ReviewRelation -> "RR"
    | JudgeEffect -> "JE"
    | GroundEvidence -> "GE"
    | CheckCounterexample -> "CC"
    | ExpandUnknown -> "EU"
    | SynthesizeAnswer -> "SA"

/// 义务（§6.3 瘦身）：运行期视图，不进 ledger。
/// 排序键预计算进 record——排序函数不再回头查 ledger（纯派生的第二阶段）。
type Obligation =
    { Id: string
      Kind: ObligationKind
      SubjectIds: string list
      PriorityClass: int // P0..P7（§5.5）
      ContractImpact: int // 对未满足合同维度的影响
      ReversalPower: int // 推翻当前结论的能力
      DependentCount: int // 依赖它的其他义务数
      EstimatedCost: int
      CreatedSequence: int }

// ── 派生谓词（§6.4）：每个都是 ledger 的纯查询。
// ponytail: P0 细化前三个（最小切片 claim_test 的真实输入）；
// 余者契约钉住，随方法实现补全——防"先实现先合理化"。

let hasUsableIntentFrame (ledger: MeditationLedger) : bool =
    ledger.Claims
    |> Map.exists (fun _ c -> c.Role = Definition || c.Role = Assertion)

/// 平衡 = 无关键命题（vacuous），或每个关键命题（Assertion/Hypothesis）都已有反对面候选。
/// P0 裁决（claim_test 双侧纪律，§7.1.1）：关键命题必须同时检查支持与反对——
/// 反对侧由本义务保证，支持侧由 GroundEvidence 义务保证。
let hasBalancedCandidateSpace (ledger: MeditationLedger) : bool =
    ledger.Claims
    |> Map.toList
    |> List.choose (fun (id, c) ->
        if c.Role = Hypothesis || c.Role = Assertion then
            Some id
        else
            None)
    |> List.forall (fun id ->
        ledger.Warrants
        |> Map.exists (fun _ w -> Warrant.claimId w = id && Warrant.polarity w = Opposes))

/// 关键 = 出现在当前合同必需章节的命题（P0 近似：Assertion 角色即关键）。
/// "未接地" = 支持侧无 warrant（§6.4 GroundCriticalClaim 的 P0 裁决：
/// 支持侧由 ground 义务保证，反对侧由平衡义务保证——两者共同构成双侧纪律）。
let hasUngroundedCriticalClaim (ledger: MeditationLedger) : bool =
    ledger.Claims
    |> Map.exists (fun id c ->
        c.Role = Assertion
        && not (
            ledger.Warrants
            |> Map.exists (fun _ w -> Warrant.claimId w = id && Warrant.polarity w = Supports)
        ))

let hasUncheckedDecisiveCounterexample (_ledger: MeditationLedger) : bool = false // ponytail: P0 无反例搜索方法；随 falsification 实现接通

let reportContractSatisfied (_request: MeditationRequest) (_ledger: MeditationLedger) : bool = false // ponytail: P0 由 Stop.tryProve 的 full-contract prover 承担；此处保持 false = 继续

/// §6.4：义务由当前事实即时推导。
/// P0-5（138 版）：义务按缺口逐条派生——ID 含 subject（与 deriveDischargeProof 完全一致：
/// FrameClaim: / GenerateOpposition:<claim-id> / GroundEvidence:<claim-id>），
/// SubjectIds 填充 claim id——executor 知道处理哪个 Claim，证明链逐义务闭合。
/// P0 裁决：SynthesizeAnswer 不在此派生——"报告合同未满足"的判断由装配处的
/// full-contract prover 承担（§10.3 成功终止的语义），避免 SynthesizeAnswer 成为
/// 反复出现的兜底义务（TASK.md 评审 #6：reportContractSatisfied 恒 false 的问题）。
let deriveObligations (request: MeditationRequest) (ledger: MeditationLedger) : Obligation list =
    let mk id kind priority subjects =
        { Id = id
          Kind = kind
          SubjectIds = subjects
          PriorityClass = priority
          ContractImpact = 0
          ReversalPower = 0
          DependentCount = 0
          EstimatedCost = 1
          CreatedSequence = 0 }

    [ // 139 版 review blocking：FrameClaim 义务只在有目标声明时派生——无 TargetStatement 时
      // subject 为空串，validateContribution 要求 ClaimFramed 的 id 为空（永不成立，fold
      // 强制 claim id = hash(statement, scope)），义务结构上不可满足（必然 Inconclusive）。
      // P0 契约（ClaimTest）都带 TargetStatement；无目标的请求无 FrameClaim 义务，
      // 由 prover 链的 verifyContractSatisfaction targetOk 检查兜底（不会误成功）。
      if not (hasUsableIntentFrame ledger) then
          // 139 版评审 #11：FrameClaim 义务带目标 subject——带 TargetStatement 的 ClaimTest
          // 在 framing 前即可确定 claim ID，义务 ID 与 discharge proof 精确相等
          // （并防止错误 claim 让 hasUsableIntentFrame 提前返回 true）。
          let targetId =
              match request.Contract |> Option.bind (fun c -> c.TargetStatement) with
              | Some ts ->
                  let contractScope =
                      request.Contract
                      |> Option.map (fun c -> c.Scope)
                      |> Option.defaultValue
                          { Content = None
                            Time = None
                            Modality = None
                            Population = None }

                  claimIdText (ClaimId.ofProposition ts contractScope)
              | None -> ""

          if targetId <> "" then
              mk $"FrameClaim:{targetId}" FrameClaim 0 [ targetId ]
      // 每个缺少反对面的关键 claim（Assertion/Hypothesis）各一条。
      for (cid, c) in ledger.Claims |> Map.toList do
          if
              (c.Role = Hypothesis || c.Role = Assertion)
              && not (
                  ledger.Warrants
                  |> Map.exists (fun _ w -> Warrant.claimId w = cid && Warrant.polarity w = Opposes)
              )
          then
              mk $"GenerateOpposition:{claimIdText cid}" GenerateOpposition 2 [ claimIdText cid ]
      // 每个缺少支持的 Assertion 各一条。
      for (cid, c) in ledger.Claims |> Map.toList do
          if
              c.Role = Assertion
              && not (
                  ledger.Warrants
                  |> Map.exists (fun _ w -> Warrant.claimId w = cid && Warrant.polarity w = Supports)
              )
          then
              mk $"GroundEvidence:{claimIdText cid}" GroundEvidence 4 [ claimIdText cid ]
      if hasUncheckedDecisiveCounterexample ledger then
          mk "CheckCounterexample:" CheckCounterexample 4 [] ]

/// §5.5 词典序：hint 只在所有前置键相同后参与方法选择层的 tie-break，不进义务排序（§41.8）。
/// 139 版评审 #15：义务优先级由 kind 决定（kind→priority 映射下同优先级跨 kind 不可能），
/// hint tie-break 不可达——故 MethodHints **不进请求身份**（canonicalRequest 不含 hints：
/// 身份稳定，行为也稳定，二者不再脱节）。MethodHints 保留在 intent 上作未来方法选择层占位。
let selectDeterministically (_hints: string list) (obligations: Obligation list) : Obligation option =
    obligations
    |> List.sortBy (fun o ->
        o.PriorityClass,
        -o.ContractImpact,
        -o.ReversalPower,
        -o.DependentCount,
        o.EstimatedCost,
        o.CreatedSequence,
        o.Id)
    |> List.tryHead
