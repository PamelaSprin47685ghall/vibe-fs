// Meditator DSL — 终止判断。语法化：演算 §11 出口决策 + §10.2 停证。
// 终止返回 ExitDecision（含停证/证明），不返回 done=true（§10.1）。
// StopProof/ContractSatisfactionProof/RefutationProof 均 opaque：构造点收敛在本文件，
// 外部代码无法直接写 record 字面量冒充停证。
// 编译顺序：8（依赖 Meditation、Ledger、Obligation）。
module Meditator.Stop

open Meditator.Meditation
open Meditator.Ledger
open Meditator.Obligation

type FiniteCoverageWitness = { Source: string; Digest: string }

type UserStipulationRef = UserStipulationRef of string

/// 覆盖证明（§10.2）：OpenWorld 之外的形态必须携带 certificate——
/// 结构化字段本身不能冒充覆盖证明。
type CoverageCertificate =
    | VerifiedFinite of FiniteCoverageWitness
    | UserAssumedComplete of UserStipulationRef

type CoverageProof =
    | OpenWorld
    | ClosedWorld of CoverageCertificate

type ObligationProof =
    { ObligationId: string
      DischargeEventDigests: string list }

/// 停证必须可独立重放：ProofEventDigests 覆盖每个 ObligationProof 的依赖事件（§10.2）。
/// opaque：唯一构造点 StopProof.create 检查覆盖不变量。
type StopProof =
    private
        { RequiredObligationsDischarged: ObligationProof list
          RemainingUnknowns: UnknownId list
          Coverage: CoverageProof
          AchievedGrade: EpistemicGrade
          ProhibitedClaims: string list
          ProofEventDigests: string list }

module StopProof =
    let create
        (discharged: ObligationProof list)
        (unknowns: UnknownId list)
        (coverage: CoverageProof)
        (grade: EpistemicGrade)
        (prohibited: string list)
        (eventDigests: string list)
        : Result<StopProof, string> =
        // §10.2：ProofEventDigests 必须覆盖每个 ObligationProof 的依赖事件——停证可独立重放。
        let uncovered =
            discharged
            |> List.collect (fun p -> p.DischargeEventDigests)
            |> List.filter (fun d -> not (List.contains d eventDigests))

        match uncovered with
        | [] ->
            Ok
                { RequiredObligationsDischarged = discharged
                  RemainingUnknowns = unknowns
                  Coverage = coverage
                  AchievedGrade = grade
                  ProhibitedClaims = prohibited
                  ProofEventDigests = eventDigests }
        | d :: _ -> Error $"StopProof: discharge event digest {d} not covered by ProofEventDigests"

    let discharged (p: StopProof) = p.RequiredObligationsDischarged
    let unknowns (p: StopProof) = p.RemainingUnknowns
    let coverage (p: StopProof) = p.Coverage
    let grade (p: StopProof) = p.AchievedGrade
    let prohibited (p: StopProof) = p.ProhibitedClaims
    let eventDigests (p: StopProof) = p.ProofEventDigests

/// 成功出口的前提对象：没有它，conclude 在语法上不可调用（§12.4）。
/// opaque：唯一构造点 ContractSatisfactionProof.create（要求停证合法）。
type ContractSatisfactionProof =
    private
        { ContractDigest: string
          StopProof: StopProof }

module ContractSatisfactionProof =
    let create (contractDigest: string) (proof: StopProof) : ContractSatisfactionProof =
        { ContractDigest = contractDigest
          StopProof = proof }

    let contractDigest (p: ContractSatisfactionProof) = p.ContractDigest
    let stopProof (p: ContractSatisfactionProof) = p.StopProof

/// 反驳规则（140 版评审 §5）：TargetRefuted 由 claim 形态与 refutation rule 共同决定，
/// 不能仅由极性位（RefutedOnly）产生——单个反例不得反驳统计性命题。
/// 注册表版本化：P0 仅注册 LogicalCounterexample（全称/存在形态命题的逻辑反例）；
/// StatisticalRefutation（统计/普遍性经验命题，需统计模型）未注册 → 出口拒绝。
/// 140 版 review：**形态-规则匹配机械编码**（create 校验）——"全称/存在形态"不是
/// 文档承诺，是 create 的强制参数。
type RefutationRule =
    | LogicalCounterexample
    | StatisticalRefutation

/// Claim 的逻辑形态（140 版 review：TargetRefuted 的规则适用性由形态决定）。
type ClaimMorphology =
    | Universal // 全称命题（"所有 X 都是 Y"）——逻辑反例可反驳
    | Existential // 存在命题（"存在路径/实例"）——穷举否定可反驳
    | Statistical // 统计/普遍性经验命题（"锻炼有助于健康"）——需统计反驳模型
    | General // 一般性命题（形态未声明）——不适用任何反驳规则

/// 已注册的反驳规则（P0 版本化 policy 的一部分，随 policyVersion 发布）。
let registeredRefutationRules: RefutationRule list = [ LogicalCounterexample ]

/// 形态-规则适用性（版本化 policy 的一部分）。
let refutationRuleApplies (morphology: ClaimMorphology) (rule: RefutationRule) : bool =
    match rule with
    | LogicalCounterexample -> morphology = Universal || morphology = Existential
    | StatisticalRefutation -> morphology = Statistical

/// 目标被反驳的证明（targetRefuted 出口）。
/// opaque：唯一构造点 RefutationProof.create。
type RefutationProof =
    private
        { TargetClaimId: ClaimId
          OpposingWarrantIds: WarrantId list
          Scope: Scope
          Rule: RefutationRule
          Morphology: ClaimMorphology }

module RefutationProof =
    let create
        (target: ClaimId)
        (opposing: WarrantId list)
        (scope: Scope)
        (rule: RefutationRule)
        (morphology: ClaimMorphology)
        : Result<RefutationProof, string> =
        if List.isEmpty opposing then
            Error "RefutationProof: at least one opposing warrant required"
        elif not (List.contains rule registeredRefutationRules) then
            Error
                "RefutationProof: refutation rule not registered (statistical refutation requires a statistical model)"
        elif not (refutationRuleApplies morphology rule) then
            Error
                $"RefutationProof: rule {rule} does not apply to claim morphology {morphology} (single counterexample cannot refute a statistical/general claim)"
        else
            Ok
                { TargetClaimId = target
                  OpposingWarrantIds = opposing
                  Scope = scope
                  Rule = rule
                  Morphology = morphology }

    let target (p: RefutationProof) = p.TargetClaimId
    let opposing (p: RefutationProof) = p.OpposingWarrantIds
    let scope (p: RefutationProof) = p.Scope
    let rule (p: RefutationProof) = p.Rule

    /// TargetRefuted 出口的停证转换：验证反方 warrant 真实存在于账本
    /// （属于目标 claim、极性 Opposes），停证事件 digest 取自 warrant 的 IntroducedBy——可独立重放。
    /// 反驳证明的验证在此完成；conclude 对 TargetRefuted 不再重复比对（类型不同，见 Report.fs）。
    let toStopProof (ledger: MeditationLedger) (p: RefutationProof) : Result<StopProof, string> =
        let collected =
            p.OpposingWarrantIds
            |> List.map (fun id ->
                match ledger.Warrants.TryFind id with
                | None -> Error $"RefutationProof: opposing warrant {warrantIdText id} not in ledger"
                | Some w ->
                    if Warrant.claimId w <> p.TargetClaimId then
                        Error
                            $"RefutationProof: warrant {warrantIdText id} belongs to {claimIdText (Warrant.claimId w)}, not target {claimIdText p.TargetClaimId}"
                    elif Warrant.polarity w <> Opposes then
                        Error $"RefutationProof: warrant {warrantIdText id} is not opposing"
                    // 其他 4：反对 warrant 的 scope 必须等于证明的 scope——跨 scope 反证
                    // 不得用于反驳当前 scope 的目标。
                    elif Warrant.scope w <> p.Scope then
                        Error $"RefutationProof: warrant {warrantIdText id} scope does not match proof scope"
                    else
                        Ok w)

        match
            collected
            |> List.tryPick (function
                | Error e -> Some e
                | Ok _ -> None)
        with
        | Some e -> Error e
        | None ->
            // P1-4：全部校验通过才继续——不允许静默丢弃后续无效 warrant（评审：只查首元素）。
            let warrants =
                collected
                |> List.choose (function
                    | Ok w -> Some w
                    | Error _ -> None)

            let eventDigests =
                warrants
                |> List.map (fun w -> (Warrant.data w).IntroducedBy)
                |> List.filter (fun s -> s <> "")
                |> List.distinct

            let grade =
                gradeOfWarrants warrants
                |> Option.defaultValue
                    { Directness = Direct
                      Reliability = Confirmed
                      Independence = Clusters 1
                      Coverage = OpenWorldCoverage
                      Reproducibility = NotYetReplayed }

            StopProof.create
                [ { ObligationId = $"target-refuted:{claimIdText p.TargetClaimId}"
                    DischargeEventDigests = eventDigests } ]
                []
                OpenWorld
                grade
                []
                eventDigests

/// 出口决策（演算 §11）：Continue 之外都是终止形态；每种形态携带自己的证明或原因。
/// Blocked/Inconsistent/Inconclusive 与 MeditationStop 的短路通道对应；成功形态经 conclude 正常返回。
type ExitDecision =
    | Continue
    | ContractSatisfied of ContractSatisfactionProof
    | OpenWorldReportReady of StopProof
    | TargetRefuted of RefutationProof
    | Blocked of RequiredInput list
    | Inconsistent of Contradiction list
    | Inconclusive of UnresolvedProblem list

/// 契约 digest（P1-2）：ContractSatisfactionProof 的 ContractDigest 的唯一权威计算——
/// 装配处（prover）与 Kernel（verifyExitDecision）共用同一实现，防止"用任意字符串
/// 冒充当前契约的证明"。委托 Obligation.canonicalRequest（P0-1：含完整 AnswerContract，
/// RequiredSections/UnacceptableClaims 变更使旧契约证明失效）；预算不属契约身份。
let contractDigest (request: MeditationRequest) : string =
    EventCodec.sha256Hex (canonicalRequest request)

/// P0-5：Kernel 机械验证完整 AnswerContract——不依赖场景 prover 自行解释合同语义。
/// P0 支持的模式集明确；其余 → UnsupportedContractMode（fail closed，不允许静默放行）。
/// ① 通用：账本中所有 claim 的 scope 必须与合同 scope 一致（跨 scope 满足 = 非法）；
/// ② 模式判定：ClaimTest 需要目标 Assertion（双侧纪律由义务派生保证——义务空即双侧齐）；
///    Brainstorm 无双侧要求；Empirical/Probabilistic 需要数值层（P0 未实现 → Unsupported）。
let verifyContractSatisfaction (request: MeditationRequest) (ledger: MeditationLedger) : Result<unit, string> =
    match request.Contract with
    | None -> Ok()
    | Some c ->
        let scopeOk =
            ledger.Claims |> Map.toList |> List.forall (fun (_, cl) -> cl.Scope = c.Scope)

        if not scopeOk then
            Error "verifyContractSatisfaction: claims outside contract scope"
        else
            match c.Goal, c.RequestedEvidenceMode with
            | ClaimTest, (Exploratory | Qualitative) ->
                // P0-4（138 版）：ClaimTest 的目标必须是请求声明的命题——账本中存在
                // Statement = TargetStatement 的 Assertion（任意 assertion 不能满足他请求）。
                let targetOk =
                    match c.TargetStatement with
                    | None -> ledger.Claims |> Map.exists (fun _ cl -> cl.Role = Assertion)
                    | Some ts ->
                        ledger.Claims
                        |> Map.exists (fun _ cl -> cl.Role = Assertion && cl.Statement = ts)

                if targetOk then
                    Ok()
                else
                    Error "verifyContractSatisfaction: claim_test target claim not found"
            | Brainstorm, Exploratory -> Ok()
            | _ -> Error $"verifyContractSatisfaction: unsupported contract mode ({c.Goal}, {c.RequestedEvidenceMode})"

/// 出口验证（P1-2/P0-3..7）：Kernel 在信任注入的 prover/compiler 之前自行验证——
/// ① 停证的事件 digest 必须真实存在于账本（claim/warrant 的 IntroducedBy）；
/// ② RemainingUnknowns 必须 ⊆ 账本未知区域；
/// ③ TargetRefuted 的反对 warrant 必须真实存在于账本（toStopProof 已验，此处再确认决策形态）；
/// ④ ContractSatisfied 的 ContractDigest 必须等于当前请求的契约 digest（P0-4：
///    ContractSatisfactionProof.create 对 digest 零约束，必须在此绑定，否则"用任意契约
///    证明当前契约已满足"的伪造全链放行）。
/// 防止"形式合法、内容虚假"的出口证明（恶意或有 bug 的装配代码）。
let rec verifyExitDecision
    (request: MeditationRequest)
    (ledger: MeditationLedger)
    (decision: ExitDecision)
    : Result<unit, string> =
    match decision with
    | ExitDecision.Continue -> Error "verifyExitDecision: Continue is not a terminal exit"
    | ExitDecision.Blocked _
    | ExitDecision.Inconsistent _
    | ExitDecision.Inconclusive _ -> Error "verifyExitDecision: non-success exit must not reach Kernel verification"
    | ExitDecision.TargetRefuted refutation ->
        // P0-3（137 版）：TargetRefuted 专用出口校验——不绕过合同/停证校验。
        match RefutationProof.toStopProof ledger refutation with
        | Error e -> Error $"verifyExitDecision: {e}"
        | Ok proof ->
            // ① 仅 ClaimTest 契约允许反驳出口；证明 scope 必须等于合同 scope。
            match request.Contract with
            | Some { Goal = ClaimTest
                     Scope = contractScope } when contractScope = (refutation |> RefutationProof.scope) ->
                // ② 目标必须存在于账本且为 Assertion（当前请求的目标主张）。
                match ledger.Claims.TryFind refutation.TargetClaimId with
                | Some target when target.Role = Assertion ->
                    // 138 版 review：目标必须与请求声明的 TargetStatement 一致——否则装配可
                    // 反驳账本中任意其他 Assertion 通过 TargetRefuted 出口。
                    let targetMatchesRequest =
                        match request.Contract |> Option.bind (fun c -> c.TargetStatement) with
                        | None -> true
                        | Some ts -> target.Statement = ts

                    if not targetMatchesRequest then
                        Error "verifyExitDecision: refutation target does not match request TargetStatement"
                    else
                        // ③ 目标必须处于 RefutedOnly——Contested（存在支持）不得标为"已反驳"。
                        match polarityOf ledger refutation.TargetClaimId (RefutationProof.scope refutation) with
                        | RefutedOnly -> verifyStopProofCore request ledger proof
                        | _ -> Error "verifyExitDecision: target not RefutedOnly (contested or supported)"
                | _ -> Error "verifyExitDecision: refutation target missing or not an assertion"
            | _ -> Error "verifyExitDecision: TargetRefuted requires ClaimTest contract with matching scope"
    | ExitDecision.ContractSatisfied proof ->
        if ContractSatisfactionProof.contractDigest proof <> contractDigest request then
            Error "verifyExitDecision: contract digest does not match current request"
        elif not (List.isEmpty (deriveObligations request ledger)) then
            // P0-3：成功出口（除 TargetRefuted）要求当前请求的义务全部解除——
            // 停证不得证明"弱于当前义务集的契约已满足"。
            Error "verifyExitDecision: obligations not discharged (cannot prove contract satisfied)"
        else
            // P0-5：合同语义由 Kernel 机械验证（模式支持集 + scope 一致性）。
            match verifyContractSatisfaction request ledger with
            | Error e -> Error $"verifyExitDecision: {e}"
            | Ok() -> verifyStopProof request ledger (ContractSatisfactionProof.stopProof proof)
    | ExitDecision.OpenWorldReportReady proof ->
        if not (List.isEmpty (deriveObligations request ledger)) then
            Error "verifyExitDecision: obligations not discharged (open-world report requires discharge)"
        else
            // 139 版评审 #10：OpenWorld 也必须保证请求目标已正确 frame——证据模式未满足
            // 降低的是证据结论，不是主题相关性（不得报告另一个问题）。
            // 139 版 review should-fix：按 scope 敏感的 ClaimId 查（statement 纯文本匹配
            // 可被同 statement 异 scope 的 claim 绕过——与 FrameClaim 义务的绑定不一致）。
            let targetFramed =
                match request.Contract |> Option.bind (fun c -> c.TargetStatement) with
                | None -> true
                | Some ts ->
                    let contractScope =
                        request.Contract
                        |> Option.map (fun c -> c.Scope)
                        |> Option.defaultValue
                            { Content = None
                              Time = None
                              Modality = None
                              Population = None }

                    ledger.Claims.ContainsKey(ClaimId.ofProposition ts contractScope)

            if not targetFramed then
                Error "verifyExitDecision: open-world report target not framed in ledger"
            else
                verifyStopProof request ledger proof

/// 停证验证（P0-3 强化 + P0-6）：核心校验（digest/unknowns/grade/prohibited/coverage）+
/// ⑤ discharged 必须等于 deriveDischargeProof（义务解除证明不得由装配自由构造）。
and private verifyStopProof
    (request: MeditationRequest)
    (ledger: MeditationLedger)
    (stopProof: StopProof)
    : Result<unit, string> =
    match verifyStopProofCore request ledger stopProof with
    | Error e -> Error e
    | Ok() ->
        // ⑤ P0-6：discharged 必须等于 Kernel 机械派生的义务解除证明。
        match deriveDischargeProof request ledger with
        | Error e -> Error $"verifyExitDecision: {e}"
        | Ok expected ->
            if StopProof.discharged stopProof = expected then
                Ok()
            else
                Error "verifyExitDecision: discharged obligations do not match derived discharge proof"

/// 停证核心校验（P0-3 137 版拆分）：digest/unknowns/grade/prohibited/coverage——
/// TargetRefuted 出口复用（不要求义务空）；ContractSatisfied/OpenWorld 在其上加 discharge 校验。
and private verifyStopProofCore
    (request: MeditationRequest)
    (ledger: MeditationLedger)
    (stopProof: StopProof)
    : Result<unit, string> =
    let committedDigests =
        (ledger.Claims |> Map.toList |> List.map (fun (_, c) -> c.IntroducedBy))
        @ (ledger.Warrants
           |> Map.toList
           |> List.map (fun (_, w) -> (Warrant.data w).IntroducedBy))
        |> Set.ofList

    // ① 停证事件 digest 真实存在。
    let unknownDigests =
        StopProof.eventDigests stopProof
        |> List.filter (fun d -> not (committedDigests.Contains d))
    // ② unknown 完整性：RemainingUnknowns == ledger 未知区域（子集不完整 = 拒绝）。
    let knownUnknowns =
        ledger.UnknownRegions
        |> Map.toList
        |> List.map (fun (id, _) -> id)
        |> Set.ofList

    let unknownIds = StopProof.unknowns stopProof |> Set.ofList
    let strayUnknowns = Set.difference unknownIds knownUnknowns
    let missingUnknowns = Set.difference knownUnknowns unknownIds
    // ③ grade 机械派生：proof grade == 停证**引用**证据的派生 grade（138 版：不是全账本——
    // 与目标/报告无关的证据不污染 TargetRefuted 与顶层报告）。
    let gradeOk = StopProof.grade stopProof = gradeForStopProof ledger stopProof
    // ④ ProhibitedClaims ⊇ 契约 UnacceptableClaims。
    let prohibited = StopProof.prohibited stopProof |> Set.ofList

    let unacceptable =
        request.Contract
        |> Option.map (fun c -> c.UnacceptableClaims |> Set.ofList)
        |> Option.defaultValue Set.empty

    let missingProhibited = Set.difference unacceptable prohibited

    // ⑥ P0-6/P0-7（137 版）：Coverage 仅支持 OpenWorld——grade 无 ClosedWorld 来源
    // （ledgerDerivedGrade 恒 OpenWorldCoverage），ClosedWorld 证书不可达（诚实拒绝而非伪验证）。
    let coverageOk =
        match StopProof.coverage stopProof, StopProof.grade stopProof with
        | OpenWorld, { Coverage = OpenWorldCoverage } -> true
        | _ -> false

    if not (List.isEmpty unknownDigests) then
        Error $"verifyExitDecision: proof digests not in ledger: {List.head unknownDigests}"
    elif not (Set.isEmpty strayUnknowns) || not (Set.isEmpty missingUnknowns) then
        Error "verifyExitDecision: proof unknowns do not match ledger unknowns"
    elif not gradeOk then
        Error "verifyExitDecision: proof grade is not ledger-derived"
    elif not (Set.isEmpty missingProhibited) then
        Error $"verifyExitDecision: prohibited claims missing: {missingProhibited |> Set.toList |> List.head}"
    elif not coverageOk then
        Error "verifyExitDecision: coverage certificate not ledger-derived or inconsistent with grade"
    else
        Ok()

/// 停证引用的证据（138 版）：ledger 中 IntroducedBy ∈ 停证 eventDigests 的 warrants——
/// grade 按此集合派生，与目标/报告无关的证据不参与。
and referencedWarrants (ledger: MeditationLedger) (proof: StopProof) : Warrant list =
    let digests = StopProof.eventDigests proof |> Set.ofList

    ledger.Warrants
    |> Map.toList
    |> List.map snd
    |> List.filter (fun w -> digests.Contains (Warrant.data w).IntroducedBy)

/// 按停证引用证据计算 grade（138 版）。
and gradeForStopProof (ledger: MeditationLedger) (proof: StopProof) : EpistemicGrade =
    gradeOfWarrants (referencedWarrants ledger proof)
    |> Option.defaultValue
        { Directness = Direct
          Reliability = Confirmed
          Independence = Clusters 1
          Coverage = OpenWorldCoverage
          Reproducibility = NotYetReplayed }

/// ledger 机械派生的报告级 grade（P0-3：公开，装配处不得复制实现——
/// 复制偏差会让合法报告被 verifyStopProof 拒绝）：支持/反对 warrant 各自 gradeOfWarrants 后 meet（§41.4）。
/// P1-9 已知限制：无证据时返回基准 grade（Direct/Confirmed/Clusters 1/OpenWorld/NotYetReplayed）——
/// 它表示"无证据的默认基准"，不表示高可靠性；grade 各维缺显式 bottom（如 NoEvidence），
/// 报告层已引入 ReportGrade（P0-7）；停证级 EpistemicGrade 无 bottom 的剩余限制见 THREAT_MODEL.md。
/// 基准值被测试锚定（empty ledger grade is baseline），防止实现漂移。
and ledgerDerivedGrade (ledger: MeditationLedger) : EpistemicGrade =
    let supports =
        ledger.Warrants
        |> Map.toList
        |> List.map snd
        |> List.filter (fun w -> Warrant.polarity w = Supports)

    let opposes =
        ledger.Warrants
        |> Map.toList
        |> List.map snd
        |> List.filter (fun w -> Warrant.polarity w = Opposes)

    match gradeOfWarrants supports, gradeOfWarrants opposes with
    | Some a, Some b -> Grade.meet a b
    | Some a, None -> a
    | None, Some b -> b
    | None, None ->
        { Directness = Direct
          Reliability = Confirmed
          Independence = Clusters 1
          Coverage = OpenWorldCoverage
          Reproducibility = NotYetReplayed }

/// P0-6/P0-5（137 版）：义务解除证明由 Kernel 机械派生——装配不得自由构造 ObligationProof。
/// 逐义务证明：每种义务的 ID 含 subject（FrameClaim:<claim-id>、GenerateOpposition:<claim-id>、
/// GroundEvidence:<claim-id>），支撑事件 = 对应引入 digest——不再是总括状态快照。
and deriveDischargeProof
    (request: MeditationRequest)
    (ledger: MeditationLedger)
    : Result<ObligationProof list, string> =
    if deriveObligations request ledger <> [] then
        Error "deriveDischargeProof: obligations not discharged"
    else
        let claimProofs =
            ledger.Claims
            |> Map.toList
            |> List.map (fun (cid, c) ->
                { ObligationId = $"FrameClaim:{claimIdText cid}"
                  DischargeEventDigests = [ c.IntroducedBy ] })

        // 139 版评审 #13：最小 discharge witness——每义务选确定性最小证明：
        // 按 WarrantId 排序后的首个合格 warrant（重复/额外 warrant 不扩大最小停证；
        // 报告可附注额外证据，但不进停证引用集）。
        let minimalWarrantProof (polarity: Polarity) (kindName: string) =
            ledger.Warrants
            |> Map.toList
            |> List.filter (fun (_, w) -> Warrant.polarity w = polarity)
            |> List.sortBy (fun (wid, _) -> warrantIdText wid)
            |> List.groupBy (fun (_, w) -> claimIdText (Warrant.claimId w))
            |> List.map (fun (cidText, ws) ->
                let (_, w) = List.head ws

                { ObligationId = $"{kindName}:{cidText}"
                  DischargeEventDigests = [ (Warrant.data w).IntroducedBy ] })

        let opposeProofs = minimalWarrantProof Opposes "GenerateOpposition"
        let supportProofs = minimalWarrantProof Supports "GroundEvidence"

        Ok(claimProofs @ opposeProofs @ supportProofs)

/// 出口 prover：从 request + ledger 尝试构造出口决策。纯函数。
/// 顺序即语义（§10.5）：inconsistency → targetRefuted → fullContract
/// → usefulOpenWorldReport → blocked → inconclusive（最后一个 prover 恒返回 Inconclusive）。
type ExitProver = MeditationRequest -> MeditationLedger -> ExitDecision

/// §10.5：按明确证明顺序匹配多个可能出口。
/// 全 Continue = 尚无终止证明 = 继续调查（seek 循环推进）；不是 Inconclusive。
/// "prover 链不完整"的防御由装配处承担：链必须以义务空时给出决策的 prover 结尾
/// （Kernel 的义务空分支 + meditate 的 invariant 分支兜底），否则义务空时 tryProve
/// 返回 Continue → seek 交还账本 → meditate 尾部 invariant halt 显式失败。
let tryProve (provers: ExitProver list) (request: MeditationRequest) (ledger: MeditationLedger) : ExitDecision =
    provers
    |> List.tryPick (fun prove ->
        match prove request ledger with
        | Continue -> None
        | decision -> Some decision)
    |> function
        | Some decision -> decision
        | None -> Continue
