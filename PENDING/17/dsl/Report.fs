// Meditator DSL — 报告 = 证明的渲染（P10）。
// renderer 输入是 CanonicalReport 不是 Ledger——类型上碰不到原始账本，新增事实无入口。
// CanonicalReport 是 opaque：唯一构造点 compile（输入含停证），conclude 只读 canonical 一个
// 权威来源（演算 §11 末条）——出口决策与报告内停证不一致时显式失败，不静默取信其一。
// 编译顺序：9（依赖 Meditation、Ledger、Stop）。
module Meditator.Report

open System.Threading.Tasks
open Meditator.Meditation
open Meditator.Ledger
open Meditator.Obligation
open Meditator.Stop

type Qualification =
    { ScopeNote: string option
      Caveats: string list }

/// 报告级 grade（P0-7 137 版）：无证据 = NoEvidence（不是 Direct+Confirmed 的高可靠性外观）；
/// 有证据 = Graded（支持/反对 warrant 的 meet，§41.4）。停证级 EpistemicGrade 保留
/// （verifyStopProof 校验其机械派生）；报告层不得用正常 evidence grade 表示空集合。
type ReportGrade =
    | NoEvidence
    | Graded of EpistemicGrade

/// finding 必须引用 ledger 中的对象（§11.1）；
/// Contested claim 必须产生支持/反对两项，不得互相抵消（§41.4）。
type ReportFinding =
    { Text: string
      ClaimIds: ClaimId list
      WarrantIds: WarrantId list
      EvidenceIds: string list
      Polarity: Polarity
      Grade: EpistemicGrade
      Qualification: Qualification }

type ReportDependency = { FromId: string; ToIds: string list }

type ReportRecommendation =
    { Text: string
      BasedOnFindingIndexes: int list }

/// Canonical Report Model：程序机械生成，LLM 不可自由创造（§11.1）。
/// opaque：唯一构造点 CanonicalReport.compile——外部代码无法直接写 record 字面量冒充报告。
type CanonicalReport =
    private
        { IntentRestatement: string
          Scope: Scope
          Findings: ReportFinding list
          Dependencies: ReportDependency list
          EvidenceLimitations: string list
          Unknowns: string list
          Recommendations: ReportRecommendation list
          Grade: ReportGrade
          StopProof: StopProof }

module CanonicalReport =
    /// 唯一构造点：stop proof 与 grade 由此进入报告——conclude 的单一权威来源。
    let compile
        (intent: string)
        (scope: Scope)
        (findings: ReportFinding list)
        (dependencies: ReportDependency list)
        (limitations: string list)
        (unknowns: string list)
        (recommendations: ReportRecommendation list)
        (grade: ReportGrade)
        (proof: StopProof)
        : CanonicalReport =
        { IntentRestatement = intent
          Scope = scope
          Findings = findings
          Dependencies = dependencies
          EvidenceLimitations = limitations
          Unknowns = unknowns
          Recommendations = recommendations
          Grade = grade
          StopProof = proof }

    let intent (r: CanonicalReport) = r.IntentRestatement
    let scope (r: CanonicalReport) = r.Scope
    let findings (r: CanonicalReport) = r.Findings
    let dependencies (r: CanonicalReport) = r.Dependencies
    let limitations (r: CanonicalReport) = r.EvidenceLimitations
    let unknowns (r: CanonicalReport) = r.Unknowns
    let recommendations (r: CanonicalReport) = r.Recommendations
    let grade (r: CanonicalReport) = r.Grade
    let stopProof (r: CanonicalReport) = r.StopProof

/// 公开报告的停止原因（§11.2 细化）：与 ExitDecision 一一对应，不再是固定字符串。
type ReportStopReason =
    | ContractSatisfied
    | OpenWorldReportReady
    | TargetRefuted
    | Blocked
    | Inconsistent
    | Inconclusive
    | BudgetExhausted

/// 公开报告（§11.2）：Findings/Counterpoints 由 Polarity 确定性分区。
/// EvidenceLimitations 必须进入公开报告——报告层最不可丢失的信息之一。
/// Scope（P1-4）：公开报告携带 canonical scope——不同 scope 的报告不得同 digest。
type MeditationReport =
    { Title: string
      ExecutiveSummary: string
      Scope: Scope
      Findings: ReportFinding list
      Counterpoints: ReportFinding list
      Dependencies: ReportDependency list
      EvidenceLimitations: string list
      Unknowns: string list
      Recommendations: ReportRecommendation list
      EpistemicGrade: ReportGrade
      StopReason: ReportStopReason
      Provenance: string list }

/// 确定性分区（§11.1）：同一 contested claim 的两侧并列展示。
let partitionByPolarity (findings: ReportFinding list) : ReportFinding list * ReportFinding list =
    findings |> List.partition (fun f -> f.Polarity = Supports)

/// 其他 2：完整 MeditationReport 的 canonical codec（收进内核，装配处不得自定义摘要）。
/// 覆盖评审列出的全部字段：counterpoints 与 findings 的区分、Claim/Warrant/Evidence IDs、
/// qualifications、dependencies、limitations、recommendations、stop reason、provenance——
/// 两个明显不同的报告不得具有相同完成 digest。
module ReportCodec =
    /// finding 级 grade 显式标签（P1-5）。
    let private findingGradeText (g: EpistemicGrade) : string =
        // P1-5：显式固定标签（不依赖 DU ToString——编译重构不得改变持久化身份）。
        let d =
            match g.Directness with
            | Direct -> "DIR"
            | Indirect -> "IND"
            | Derivational -> "DRV"

        let r =
            match g.Reliability with
            | Confirmed -> "CON"
            | Corroborated -> "COR"
            | Tentative -> "TEN"

        let i =
            match g.Independence with
            | Clusters n -> $"CL{n}"

        let c =
            match g.Coverage with
            | ClosedWorldCoverage -> "CW"
            | OpenWorldCoverage -> "OW"

        let rep =
            match g.Reproducibility with
            | Replayed -> "RP"
            | NotYetReplayed -> "NR"

        $"{d}|{r}|{i}|{c}|{rep}"

    /// P1-5：option 编码复用 EventCodec 的双哨兵方案（None=\u0001；Some=\u0002 前缀 + 双写转义）——
    /// 不得重新发明哨兵（None 与 Some \"\\u0001\" 碰撞）。
    let private optEncode (o: string option) : string =
        match o with
        | None -> "\u0001"
        | Some s -> "\u0002" + s.Replace("\u0002", "\u0002\u0002")

    /// 报告级 grade 显式标签（P0-7）：NoEvidence 有独立标签——空证据不再是高可靠性外观。
    let reportGradeText (g: ReportGrade) : string =
        match g with
        | NoEvidence -> "NE"
        | Graded e -> findingGradeText e

    let private stopReasonTag (s: ReportStopReason) : string =
        match s with
        | ContractSatisfied -> "CS"
        | OpenWorldReportReady -> "OW"
        | TargetRefuted -> "TR"
        | Blocked -> "BL"
        | Inconsistent -> "IC"
        | Inconclusive -> "IN"
        | BudgetExhausted -> "BE"

    let private findingText (f: ReportFinding) : string =
        EventCodec.field "t" f.Text
        + EventCodec.field "c" (String.concat "" (f.ClaimIds |> List.map (fun (ClaimId s) -> EventCodec.field "id" s)))
        + EventCodec.field
            "w"
            (String.concat "" (f.WarrantIds |> List.map (fun (WarrantId s) -> EventCodec.field "id" s)))
        + EventCodec.field "e" (String.concat "" (f.EvidenceIds |> List.map (EventCodec.field "id")))
        + EventCodec.field
            "p"
            (match f.Polarity with
             | Supports -> "SUP"
             | Opposes -> "OPP")
        + EventCodec.field "g" (findingGradeText f.Grade)
        + EventCodec.field "qn" (optEncode f.Qualification.ScopeNote)
        + EventCodec.field "qc" (String.concat "" (f.Qualification.Caveats |> List.map (EventCodec.field "cv")))

    /// 全字段 digest——Kernel 构造完成事件的唯一权威（P1-6 后装配处不再参与）。
    let digest (r: MeditationReport) : string =
        let findings =
            r.Findings @ r.Counterpoints |> List.map findingText |> String.concat ""

        let dependencies =
            r.Dependencies
            |> List.map (fun d ->
                EventCodec.field "f" d.FromId
                + String.concat "" (d.ToIds |> List.map (EventCodec.field "to")))
            |> String.concat ""

        let recommendations =
            r.Recommendations
            |> List.map (fun rc ->
                EventCodec.field "t" rc.Text
                + String.concat "" (rc.BasedOnFindingIndexes |> List.map (fun i -> EventCodec.field "i" (string i))))
            |> String.concat ""

        EventCodec.sha256Hex (
            EventCodec.field "title" r.Title
            + EventCodec.field "sum" r.ExecutiveSummary
            + EventCodec.field "scope" (EventCodec.renderScope r.Scope)
            + EventCodec.field "find" findings
            + EventCodec.field "dep" dependencies
            + EventCodec.field "lim" (String.concat "" (r.EvidenceLimitations |> List.map (EventCodec.field "l")))
            + EventCodec.field "unk" (String.concat "" (r.Unknowns |> List.map (EventCodec.field "u")))
            + EventCodec.field "rec" recommendations
            + EventCodec.field "g" (reportGradeText r.EpistemicGrade)
            + EventCodec.field "sr" (stopReasonTag r.StopReason)
            + EventCodec.field "prov" (String.concat "" (r.Provenance |> List.map (EventCodec.field "p")))
        )

// ── 确定性 section renderer（§11.1）：第一版不用 LLM。
// prose polish 若引入，输入仅限 CanonicalReport——改善 grade/隐去 unknown 在类型上无入口。

let renderExecutiveSummary (model: CanonicalReport) : string =
    $"Intent: {CanonicalReport.intent model}\nGrade: {ReportCodec.reportGradeText (CanonicalReport.grade model)}\nUnknowns: {List.length (CanonicalReport.unknowns model)}"

let renderFindings (model: CanonicalReport) : string =
    model
    |> CanonicalReport.findings
    |> List.filter (fun f -> f.Polarity = Supports)
    |> List.map (fun f -> $"+ {f.Text}")
    |> String.concat "\n"

let renderCounterpoints (model: CanonicalReport) : string =
    model
    |> CanonicalReport.findings
    |> List.filter (fun f -> f.Polarity = Opposes)
    |> List.map (fun f -> $"- {f.Text}")
    |> String.concat "\n"

let renderDependencies (model: CanonicalReport) : string =
    model
    |> CanonicalReport.dependencies
    |> List.map (fun d ->
        let targets = String.concat ", " d.ToIds
        $"{d.FromId} <- {targets}")
    |> String.concat "\n"

let renderUnknowns (model: CanonicalReport) : string =
    model |> CanonicalReport.unknowns |> String.concat "\n"

let renderRecommendations (model: CanonicalReport) : string =
    model
    |> CanonicalReport.recommendations
    |> List.map (fun r -> $"* {r.Text}")
    |> String.concat "\n"

let renderReport (model: CanonicalReport) : string =
    [ renderExecutiveSummary model
      renderFindings model
      renderCounterpoints model
      renderDependencies model
      renderUnknowns model
      renderRecommendations model ]
    |> String.concat "\n\n"

/// 出口 → 公开停止原因（一一对应；非成功出口不会到达这里）。
let private stopReasonFor (decision: ExitDecision) : ReportStopReason =
    match decision with
    | ExitDecision.ContractSatisfied _ -> ReportStopReason.ContractSatisfied
    | ExitDecision.OpenWorldReportReady _ -> ReportStopReason.OpenWorldReportReady
    | ExitDecision.TargetRefuted _ -> ReportStopReason.TargetRefuted
    | ExitDecision.Continue -> ReportStopReason.Inconclusive // 不可达：Continue 不会进入 conclude
    | ExitDecision.Blocked _ -> ReportStopReason.Blocked // 不可达：短路通道，不经 conclude
    | ExitDecision.Inconsistent _ -> ReportStopReason.Inconsistent
    | ExitDecision.Inconclusive _ -> ReportStopReason.Inconclusive

/// 报告验证（P1-2/P0-4/P1-1..3）：Kernel 信任报告前自行验证——
/// ① findings 引用必须真实存在（claim/warrant/evidence）且**非空引用**（空引用自由文本拒绝）；
/// ② finding polarity 必须与引用 warrant 的 polarity 一致；
/// ③ finding grade 必须等于引用 warrants 的机械派生 grade；
/// ④ ledger 中 contested 的 claim 必须在 findings 双侧展示；finding 的 claim 必须被引用 warrant 支持/反对；
/// ⑤ grade 与停证一致；⑥ canonical.StopProof 必须等于 Kernel 验证过的 expectedProof；
/// ⑦ P1-1：intent/scope 必须等于请求；RequiredSections 必须存在；UnacceptableClaims 不得出现；
/// ⑧ P1-2：finding Text 必须等于引用 claim 的 statement（自由文本不允许）；
/// ⑨ P1-3：dependency IDs 存在、recommendation index 在范围、unknowns 等于 ledger 未知区域描述。
let verifyCanonicalReport
    (request: MeditationRequest)
    (ledger: MeditationLedger)
    (expectedProof: StopProof)
    (canonical: CanonicalReport)
    : Result<unit, string> =
    let findings = CanonicalReport.findings canonical

    let badClaim =
        findings
        |> List.collect (fun f -> f.ClaimIds)
        |> List.tryFind (fun id -> not (ledger.Claims.ContainsKey id))

    let badWarrant =
        findings
        |> List.collect (fun f -> f.WarrantIds)
        |> List.tryFind (fun id -> not (ledger.Warrants.ContainsKey id))

    let badEvidence =
        findings
        |> List.collect (fun f -> f.EvidenceIds)
        |> List.tryFind (fun id -> not (ledger.Evidence.ContainsKey id))

    // ① 空引用 finding：无任何引用的自由文本不得作为 finding。
    let emptyReferenceFinding =
        findings
        |> List.tryFind (fun f ->
            List.isEmpty f.ClaimIds
            && List.isEmpty f.WarrantIds
            && List.isEmpty f.EvidenceIds)

    // ② polarity 与 warrant 一致。
    let polarityMismatch =
        findings
        |> List.collect (fun f -> f.WarrantIds |> List.map (fun wid -> f, wid))
        |> List.tryFind (fun (f, wid) ->
            match ledger.Warrants.TryFind wid with
            | Some w -> Warrant.polarity w <> f.Polarity
            | None -> false)

    // ③ grade 机械派生：finding grade == 引用 warrants 的 gradeOfWarrants。
    let gradeMismatch =
        findings
        |> List.tryFind (fun f ->
            let ws = f.WarrantIds |> List.choose (fun wid -> ledger.Warrants.TryFind wid)

            match gradeOfWarrants ws with
            | Some g -> g <> f.Grade
            | None -> false)

    // ④ contested 双侧展示。
    let contestedMissingSide =
        ledger.Claims
        |> Map.toList
        |> List.tryFind (fun (cid, _) ->
            match polarityOf ledger cid (ledger.Claims.[cid].Scope) with
            | Contested ->
                let support =
                    findings
                    |> List.exists (fun f -> List.contains cid f.ClaimIds && f.Polarity = Supports)

                let oppose =
                    findings
                    |> List.exists (fun f -> List.contains cid f.ClaimIds && f.Polarity = Opposes)

                not (support && oppose)
            | _ -> false)

    // P0-4：finding 的每个 claim 必须被其引用 warrant 支持/反对——
    // 防止"真实存在的无关 warrant + 无关 claim"拼凑伪造（contested 双侧展示可被绕过）。
    let claimNotBackedByWarrant =
        findings
        |> List.tryFind (fun f ->
            f.ClaimIds
            |> List.exists (fun cid ->
                not (
                    f.WarrantIds
                    |> List.exists (fun wid ->
                        match ledger.Warrants.TryFind wid with
                        | Some w -> Warrant.claimId w = cid
                        | None -> false)
                )))

    // 138 版 review blocking：停证引用集必须 ⊆ 报告 finding 引用集——装配不能把未在
    // 报告中展示的强 warrant 塞进停证 digest 集注水 grade（引用集自选 = 自我参照）。
    let findingClaimDigests =
        findings
        |> List.collect (fun f -> f.ClaimIds)
        |> List.choose (fun cid -> ledger.Claims.TryFind cid |> Option.map (fun c -> c.IntroducedBy))
        |> Set.ofList

    let findingWarrantDigests =
        findings
        |> List.collect (fun f -> f.WarrantIds)
        |> List.choose (fun wid ->
            ledger.Warrants.TryFind wid
            |> Option.map (fun w -> (Warrant.data w).IntroducedBy))
        |> Set.ofList

    let proofDigests = StopProof.eventDigests expectedProof |> Set.ofList

    let unreferencedProofDigests =
        proofDigests - (findingClaimDigests + findingWarrantDigests)

    // 138 版：每个 finding 引用的 claim/warrant scope 必须与报告 scope 兼容——
    // （报告头部写 Earth 却引用 Mars 的真实 claim 会被拒）。
    let outOfScopeFinding =
        findings
        |> List.tryFind (fun f ->
            let reportScope = CanonicalReport.scope canonical

            let claimScopesOk =
                f.ClaimIds
                |> List.forall (fun cid ->
                    match ledger.Claims.TryFind cid with
                    | Some c -> c.Scope = reportScope
                    | None -> true)

            let warrantScopesOk =
                f.WarrantIds
                |> List.forall (fun wid ->
                    match ledger.Warrants.TryFind wid with
                    | Some w -> Warrant.scope w = reportScope
                    | None -> true)

            not (claimScopesOk && warrantScopesOk))

    match badClaim with
    | Some id -> Error $"verifyCanonicalReport: finding references ghost claim {claimIdText id}"
    | None ->
        match badWarrant with
        | Some id -> Error $"verifyCanonicalReport: finding references ghost warrant {warrantIdText id}"
        | None ->
            match badEvidence with
            | Some id -> Error $"verifyCanonicalReport: finding references ghost evidence {id}"
            | None ->
                match emptyReferenceFinding with
                | Some _ -> Error "verifyCanonicalReport: finding without any ledger reference"
                | None ->
                    match polarityMismatch with
                    | Some _ -> Error "verifyCanonicalReport: finding polarity does not match warrant"
                    | None ->
                        match gradeMismatch with
                        | Some _ -> Error "verifyCanonicalReport: finding grade is not warrant-derived"
                        | None ->
                            match contestedMissingSide with
                            | Some _ -> Error "verifyCanonicalReport: contested claim missing a side"
                            | None ->
                                match claimNotBackedByWarrant with
                                | Some _ ->
                                    Error "verifyCanonicalReport: finding claim not backed by its referenced warrants"
                                | None ->
                                    match outOfScopeFinding with
                                    | Some _ ->
                                        Error "verifyCanonicalReport: finding references out-of-scope claim/warrant"
                                    | None ->
                                        if not (Set.isEmpty unreferencedProofDigests) then
                                            Error
                                                "verifyCanonicalReport: stop proof references events not in report findings"
                                        else
                                            // ⑧ P1-2：finding Text 必须等于引用 claim 的 statement——
                                            // 多 claim 引用时与每个 claim 的 statement 一致。
                                            let textMismatch =
                                                findings
                                                |> List.tryFind (fun f ->
                                                    let referencedClaims =
                                                        match f.ClaimIds with
                                                        | cid :: _ -> Some cid
                                                        | [] ->
                                                            f.WarrantIds
                                                            |> List.tryPick (fun wid ->
                                                                ledger.Warrants.TryFind wid
                                                                |> Option.map Warrant.claimId)

                                                    match referencedClaims with
                                                    | None -> true // 无任何可引用 claim（前面已拒空引用，防御）
                                                    | Some firstCid ->
                                                        // 无显式 ClaimIds 时退化为引用 warrant 的首个 claim（防御分支）；
                                                        // 有 ClaimIds 时与每个显式 claim 的 statement 全比对。
                                                        let allCids =
                                                            f.ClaimIds
                                                            |> fun ids ->
                                                                if List.isEmpty ids then [ firstCid ] else ids

                                                        allCids
                                                        |> List.exists (fun cid ->
                                                            match ledger.Claims.TryFind cid with
                                                            | Some c -> f.Text <> c.Statement
                                                            | None -> false))

                                            // ⑨ P1-3：dependency IDs 存在、recommendation index 在范围、
                                            //     unknowns 等于 ledger 未知区域描述、scope 等于请求 scope、intent 等于请求。
                                            let badDependency =
                                                canonical
                                                |> CanonicalReport.dependencies
                                                |> List.collect (fun d -> d.FromId :: d.ToIds)
                                                |> List.tryFind (fun id ->
                                                    not (ledger.Claims.ContainsKey(ClaimId id))
                                                    && not (ledger.Warrants.ContainsKey(WarrantId id))
                                                    && not (ledger.Evidence.ContainsKey id))

                                            let badRecommendationIndex =
                                                canonical
                                                |> CanonicalReport.recommendations
                                                |> List.collect (fun rc -> rc.BasedOnFindingIndexes)
                                                |> List.tryFind (fun i -> i < 0 || i >= List.length findings)

                                            let ledgerUnknownTexts =
                                                ledger.UnknownRegions
                                                |> Map.toList
                                                |> List.map (fun (_, u) -> u.Description)
                                                |> Set.ofList

                                            let reportUnknownTexts = canonical |> CanonicalReport.unknowns |> Set.ofList
                                            let unknownsOk = ledgerUnknownTexts = reportUnknownTexts

                                            let contractScope = request.Contract |> Option.map (fun c -> c.Scope)

                                            let scopeOk =
                                                match contractScope with
                                                | None -> true
                                                | Some s -> CanonicalReport.scope canonical = s

                                            let intentOk = CanonicalReport.intent canonical = request.Intent

                                            // P0-4（137 版）：UnacceptableClaims 检查全部用户可见文本
                                            // （intent/findings/unknowns/limitations/recommendations），不仅是 findings+unknowns。
                                            let unacceptable =
                                                request.Contract
                                                |> Option.map (fun c -> c.UnacceptableClaims |> Set.ofList)
                                                |> Option.defaultValue Set.empty

                                            let reportText =
                                                [ CanonicalReport.intent canonical
                                                  findings |> List.map (fun f -> f.Text) |> String.concat "\n"
                                                  // security_review MEDIUM：Qualification（ScopeNote/Caveats）是自由文本且
                                                  // 无 ledger 绑定——禁止主张可能藏在其中，必须并入全文本检查。
                                                  findings
                                                  |> List.collect (fun f ->
                                                      [ match f.Qualification.ScopeNote with
                                                        | None -> ""
                                                        | Some s -> s ]
                                                      @ f.Qualification.Caveats)
                                                  |> String.concat "\n"
                                                  canonical |> CanonicalReport.unknowns |> String.concat "\n"
                                                  canonical |> CanonicalReport.limitations |> String.concat "\n"
                                                  canonical
                                                  |> CanonicalReport.recommendations
                                                  |> List.map (fun r -> r.Text)
                                                  |> String.concat "\n" ]
                                                |> String.concat "\n"

                                            let unacceptablePresent =
                                                unacceptable |> Set.exists (fun u -> reportText.Contains u)

                                            // P0-4（137 版）：RequiredSections 按结构检查（章节存在 = 对应字段非空），
                                            // 禁止文本子串冒充。
                                            let sectionExists (s: ReportSection) : bool =
                                                match s with
                                                | ExecutiveSummary -> CanonicalReport.intent canonical <> ""
                                                | Findings -> findings |> List.exists (fun f -> f.Polarity = Supports)
                                                | Counterpoints ->
                                                    findings |> List.exists (fun f -> f.Polarity = Opposes)
                                                | Dependencies ->
                                                    not (List.isEmpty (CanonicalReport.dependencies canonical))
                                                | EvidenceLimitations ->
                                                    not (List.isEmpty (CanonicalReport.limitations canonical))
                                                | Unknowns -> not (List.isEmpty (CanonicalReport.unknowns canonical))
                                                | Recommendations ->
                                                    not (List.isEmpty (CanonicalReport.recommendations canonical))

                                            let requiredSections =
                                                request.Contract
                                                |> Option.map (fun c -> c.RequiredSections |> Set.ofList)
                                                |> Option.defaultValue Set.empty

                                            let missingSections =
                                                requiredSections |> Set.filter (fun s -> not (sectionExists s))

                                            // P0-7/P0-5（138 版）：报告级 grade 由停证**引用**证据派生——
                                            // 无引用 → NoEvidence；有引用 → Graded(gradeForStopProof)。
                                            // （verifyStopProofCore 已校验 proof grade = gradeForStopProof。）
                                            let expectedReportGrade =
                                                if List.isEmpty (referencedWarrants ledger expectedProof) then
                                                    ReportGrade.NoEvidence
                                                else
                                                    ReportGrade.Graded(gradeForStopProof ledger expectedProof)

                                            let reportGradeOk = CanonicalReport.grade canonical = expectedReportGrade

                                            // P1-1：禁止主张优先于文本绑定拒绝（报告含 UnacceptableClaims = 直接非法）。
                                            if unacceptablePresent then
                                                Error "verifyCanonicalReport: report contains unacceptable claim"
                                            elif not (Set.isEmpty missingSections) then
                                                Error "verifyCanonicalReport: required sections missing"
                                            else
                                                match textMismatch with
                                                | Some _ ->
                                                    Error
                                                        "verifyCanonicalReport: finding text does not equal referenced claim statement"
                                                | None ->
                                                    match badDependency with
                                                    | Some _ ->
                                                        Error "verifyCanonicalReport: dependency references unknown id"
                                                    | None ->
                                                        match badRecommendationIndex with
                                                        | Some _ ->
                                                            Error
                                                                "verifyCanonicalReport: recommendation index out of range"
                                                        | None ->
                                                            if not unknownsOk then
                                                                Error
                                                                    "verifyCanonicalReport: report unknowns do not match ledger unknowns"
                                                            elif not scopeOk then
                                                                Error
                                                                    "verifyCanonicalReport: report scope does not match request scope"
                                                            elif not intentOk then
                                                                Error
                                                                    "verifyCanonicalReport: report intent does not match request"
                                                            elif not reportGradeOk then
                                                                Error
                                                                    "verifyCanonicalReport: report grade does not match ledger-derived grade"
                                                            elif
                                                                CanonicalReport.stopProof canonical <> expectedProof
                                                            then
                                                                Error
                                                                    "verifyCanonicalReport: canonical stop proof does not match verified expected proof"
                                                            else
                                                                Ok()

/// 成功出口的形态（§12.4）：先持久化 MeditationCompleted，成功后才返回（§37.5）。
/// CommitUnknown → fail closed：不重新渲染、不重发模型（PERSIST-003）。
/// P1-6：完成事件由内核构造（EventCodec.encode + ReportCodec.digest）——不再接受注入的
/// encodeCompleted 函数（恶意 encoder 可返回任意行冒充完成事件）。
/// expectedCompletedDigest（P1-1）：恢复路径已完成时携带历史完成行的报告 digest——
/// 当前报告必须与其一致（不等 = journal 与交付报告分裂，Inconsistent fail closed）；
/// None = 正常路径，append 完成事件。
/// expectedProof（P0-4）：Kernel 验证过的停证——canonical.StopProof 必须与之相等。
/// 统一锚取代旧 decisionProofMatches：TargetRefuted 出口不再放行"另一份停证"，
/// 伪造者无法自洽（旧检查的 canonical 从同一 decision.proof 提取，永远相等）。
/// conclude（139 版）：internal——meditate 是唯一写入入口；外部（Release）不得直接
/// 构造自洽但未经验证的 proof/canonical 后调用（评审 #6：verifyExitDecision/
/// verifyCanonicalReport 只由 Kernel 调用）。
let internal conclude
    (sequence: int)
    (expectedCompletedDigest: string option)
    (expectedProof: StopProof)
    (decision: ExitDecision)
    (canonical: CanonicalReport)
    : Meditation<MeditationReport> =
    fun env ct ->
        task {
            let canonicalProof = CanonicalReport.stopProof canonical

            if canonicalProof <> expectedProof then
                return
                    Error(
                        MeditationStop.Inconclusive
                            [ { ObligationId = "kernel"
                                Kind = "invariant"
                                Description = "conclude: canonical stop proof does not match verified expected proof" } ]
                    )
            else
                let findings, counterpoints =
                    partitionByPolarity (CanonicalReport.findings canonical)

                let report =
                    { Title = CanonicalReport.intent canonical
                      ExecutiveSummary = renderExecutiveSummary canonical
                      Scope = CanonicalReport.scope canonical
                      Findings = findings
                      Counterpoints = counterpoints
                      Dependencies = CanonicalReport.dependencies canonical
                      EvidenceLimitations = CanonicalReport.limitations canonical
                      Unknowns = CanonicalReport.unknowns canonical
                      Recommendations = CanonicalReport.recommendations canonical
                      EpistemicGrade = CanonicalReport.grade canonical
                      StopReason = stopReasonFor decision
                      Provenance = StopProof.eventDigests canonicalProof }

                match expectedCompletedDigest with
                | Some expected ->
                    // 恢复路径已完成（P1-1）：不重复 append，但必须校验当前报告与历史完成行
                    // 的 digest 一致（P1-1）——不等 = journal 与交付报告分裂。
                    let currentLine =
                        EventCodec.encode
                            EventSchemaVersion
                            env.PolicyVersion
                            env.ReducerVersion
                            sequence
                            (MeditationCompleted(ReportCodec.digest report))

                    match EventCodec.decode currentLine with
                    | Error e ->
                        return
                            Error(
                                MeditationStop.Inconsistent
                                    [ { SubjectId = "journal"
                                        SupportDigest = expected
                                        OpposeDigest = $"encode/decode failed: {e}" } ]
                            )
                    | Ok envelope ->
                        match envelope.Payload with
                        | MeditationCompleted d when d = expected -> return Ok report
                        | _ ->
                            return
                                Error(
                                    MeditationStop.Inconsistent
                                        [ { SubjectId = "journal"
                                            SupportDigest = expected
                                            OpposeDigest = "current report digest differs from completed journal line" } ]
                                )
                | None ->
                    let! outcome =
                        env.Journal.Append
                            sequence
                            (EventCodec.encode
                                EventSchemaVersion
                                env.PolicyVersion
                                env.ReducerVersion
                                sequence
                                (MeditationCompleted(ReportCodec.digest report)))
                            ct

                    match outcome with
                    | Committed -> return Ok report
                    | AlreadyCommitted ->
                        // 幂等重放：同一完成事件已提交（崩溃恢复后重跑）——报告内容一致，直接返回。
                        return Ok report
                    | Conflict ->
                        return
                            Error(
                                MeditationStop.Inconsistent
                                    [ { SubjectId = "journal"
                                        SupportDigest = "MeditationCompleted"
                                        OpposeDigest = "duplicate EventId with different bytes (S2)" } ]
                            )
                    | CommitUnknown ->
                        // 其他 2：完成事件与普通事件同语义——CommitUnknown 走 Reconcile，
                        // 而不是立即 Blocked（PERSIST-003 出口统一）。
                        let currentLine =
                            EventCodec.encode
                                EventSchemaVersion
                                env.PolicyVersion
                                env.ReducerVersion
                                sequence
                                (MeditationCompleted(ReportCodec.digest report))

                        match EventCodec.decode currentLine with
                        | Error e ->
                            return
                                Error(
                                    MeditationStop.Inconsistent
                                        [ { SubjectId = "journal"
                                            SupportDigest = "MeditationCompleted"
                                            OpposeDigest = $"encode/decode failed: {e}" } ]
                                )
                        | Ok envelope ->
                            // 139 版：Reconcile 携带 expectedLine——adapter 可验证存储的是本次精确行。
                            let! reconcile = env.Journal.Reconcile (eventIdText envelope.EventId) currentLine ct

                            match reconcile with
                            | Reconciled(Committed)
                            | Reconciled(AlreadyCommitted) -> return Ok report
                            | Reconciled(Conflict) ->
                                return
                                    Error(
                                        MeditationStop.Inconsistent
                                            [ { SubjectId = "journal"
                                                SupportDigest = "MeditationCompleted"
                                                OpposeDigest = "reconciled as Conflict (S2)" } ]
                                    )
                            | Reconciled(CommitUnknown)
                            | Reconciled(WrongExpectedRevision _)
                            | StillUnknown ->
                                return
                                    Error(
                                        MeditationStop.Blocked
                                            [ { What = "journal reconcile"
                                                WhyNeeded =
                                                  "MeditationCompleted outcome unknown; fail closed per PERSIST-003" } ]
                                    )
                    // 139 版：完成事件路径与普通事件同语义——并发追加冲突 fail closed。
                    | WrongExpectedRevision actual ->
                        return
                            Error(
                                MeditationStop.Blocked
                                    [ { What = "journal append"
                                        WhyNeeded =
                                          $"MeditationCompleted expected revision {sequence} but journal has {actual} rows (concurrent append?)" } ]
                            )
        }
