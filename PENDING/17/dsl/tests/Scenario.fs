// Meditator DSL — 场景装配（实施指南 §13.5：Kernel 不复制报告/方法/停止逻辑，装配处提供）。
// P0 claim_test 场景：目标 claim "The sky is blue (earth)"，义务执行器 + 完整 prover 链 +
// 报告编译器 + MeditationCompleted 编码。这是第三阶段 ClaimTest 闭环的生产路径。
module Meditator.Tests.Scenario

open Meditator.Boundary
open Meditator.Ledger
open Meditator.Meditation
open Meditator.Obligation
open Meditator.Stop
open Meditator.Report
open Meditator.Kernel
open Meditator.Tests.TestUtil

let policyVersion = "policy-test/v1"
let reducerVersion = "reducer-test/v1"
let fixedClock = "2024-01-01T00:00:00Z"

let scopeEarth: Scope =
    { Content = None
      Time = None
      Modality = None
      Population = Some "earth" }

let targetClaim: Claim =
    { Id = ClaimId.ofProposition "The sky is blue" scopeEarth
      Statement = "The sky is blue"
      Role = Assertion
      Source = ByObservation
      Scope = scopeEarth
      IntroducedBy = "" }

/// 139 版：每次构造签发**独立 witness**（不同观察=不同 receipt）——依赖簇按 witness
/// 分组后，共享 witness 的测试构造会把无关 warrant 并簇（同一 receipt 语义）。
let private mkWitnessFor (idSuffix: string) =
    VerifierWitness.issue Verifiers.observation VerifierKind.Observation ("observation-protocol:" + idSuffix)

let private witness = mkWitnessFor "global"

let private mkWarrant (idSuffix: string) (polarity: Polarity) : Warrant =
    let body =
        { Id = WarrantId idSuffix // 占位；下方 ofData 派生真实 ID
          ClaimId = targetClaim.Id
          Polarity = polarity
          Kind = Observation
          Rule = "observation/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "observation-protocol" "observation/v1"
          VerifierWitnesses = [ mkWitnessFor idSuffix ]
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId("src-" + idSuffix) ]
          IntroducedBy = "" }

    { body with
        Id = EventCodec.warrantIdOfData body }
    |> Warrant.create
    |> function
        | Ok w -> w
        | Error e -> failwith $"mkWarrant: {e}"

let supportingWarrant: Warrant = mkWarrant "sup" Supports
let opposingWarrant: Warrant = mkWarrant "opp" Opposes

let makeEnv (journal: InMemoryJournal) : MeditationEnvironment =
    { Journal = journal
      TranscriptStore = (journal :> IAcceptedTranscriptStore)
      PolicyVersion = policyVersion
      ReducerVersion = reducerVersion
      Clock = fun () -> fixedClock }

let claimTestIntent: MeditationIntent =
    { Request =
        { Intent = "Test whether the sky is blue (earth)"
          Contract =
            Some
                { Goal = ClaimTest
                  RequestedEvidenceMode = Qualitative
                  Scope = scopeEarth
                  RequiredSections = []
                  UnacceptableClaims = []
                  TargetStatement = Some "The sky is blue" }
          MethodHints = [] }
      InitialCredit = 10 }

/// 义务 → 事件的静态分派（§14.5）：手写 match，每个分支一个方法函数，不是运行时 registry。
/// 返回事件批次（P0-2）：Kernel 统一 append+fold，与重放路径一致。
let executeScenario: ObligationExecutor =
    fun obligation ->
        match obligation.Kind with
        | FrameClaim -> meditation { return [ ClaimFramed targetClaim ] }
        | GenerateOpposition -> meditation { return [ ContributionAccepted opposingWarrant ] }
        | GroundEvidence -> meditation { return [ ContributionAccepted supportingWarrant ] }
        | _ -> meditation { return [ SweepCompleted ] } // 不可达（P0 义务集不含其他种类）

let targetContested (ledger: MeditationLedger) : bool =
    polarityOf ledger targetClaim.Id scopeEarth = Contested

/// 从账本重建停证（§10.2/§12.4）：discharge digest = claim/warrant 的 IntroducedBy（可独立重放）；
/// discharged 委托 Kernel 的 deriveDischargeProof（逐义务证明，P0-5 137 版）。
let buildProof (request: MeditationRequest) (ledger: MeditationLedger) : StopProof =
    let eventDigests =
        (ledger.Claims |> Map.toList |> List.map (fun (_, c) -> c.IntroducedBy))
        @ (ledger.Warrants
           |> Map.toList
           |> List.map (fun (_, w) -> (Warrant.data w).IntroducedBy))
        |> List.filter (fun d -> d <> "")
        |> List.distinct

    let grade = Meditator.Stop.ledgerDerivedGrade ledger

    let discharged =
        match Meditator.Stop.deriveDischargeProof request ledger with
        | Ok d -> d
        | Error _ -> [] // 义务未解除的 proof 会在 verify 的义务空/discharged 检查被拒（测试用）

    // 138 版：grade 必须由停证**引用**证据派生（gradeForStopProof）——两步构造：
    // 占位 grade → 引用集已知 → 最终 grade。
    match StopProof.create discharged [] OpenWorld grade [] eventDigests with
    | Error e -> failwith $"buildProof: {e}"
    | Ok placeholder ->
        let referencedGrade = Meditator.Stop.gradeForStopProof ledger placeholder

        match StopProof.create discharged [] OpenWorld referencedGrade [] eventDigests with
        | Ok p -> p
        | Error e -> failwith $"buildProof: {e}"

/// 契约 digest：权威计算在 Meditator.Stop.contractDigest（verifyExitDecision 与装配处共用）。
/// 停证引用集 ⊆ findings 引用集的 proof（138 版 review blocking 修复后的测试辅助）：
/// 报告的停证只能引用报告展示的证据——测试 canonical 只含部分 finding 时用此构造匹配 proof。
let proofForFindings (ledger: MeditationLedger) (findings: ReportFinding list) : StopProof =
    let digests =
        (findings
         |> List.collect (fun f -> f.ClaimIds)
         |> List.choose (fun cid -> ledger.Claims.TryFind cid |> Option.map (fun c -> c.IntroducedBy)))
        @ (findings
           |> List.collect (fun f -> f.WarrantIds)
           |> List.choose (fun wid ->
               ledger.Warrants.TryFind wid
               |> Option.map (fun w -> (Warrant.data w).IntroducedBy)))
        |> List.filter (fun d -> d <> "")
        |> List.distinct

    match
        StopProof.create
            [ { ObligationId = "contract"
                DischargeEventDigests = digests } ]
            []
            OpenWorld
            (Meditator.Stop.ledgerDerivedGrade ledger)
            []
            digests
    with
    | Error e -> failwith $"proofForFindings: {e}"
    | Ok placeholder ->
        let grade = Meditator.Stop.gradeForStopProof ledger placeholder

        match
            StopProof.create
                [ { ObligationId = "contract"
                    DischargeEventDigests = digests } ]
                []
                OpenWorld
                grade
                []
                digests
        with
        | Ok p -> p
        | Error e -> failwith $"proofForFindings: {e}"

let contractDigest (request: MeditationRequest) : string = Meditator.Stop.contractDigest request

/// full-contract prover（§10.3 成功终止）：义务全部解除后，按契约维度细查——
/// Empirical/Probabilistic 在 P0 无来源（coverage 只能 OpenWorld）→ 不满足；
/// ClaimTest 双侧未齐 → 不满足；其余契约接受 OpenWorld 覆盖。
let tryProveFullContract (request: MeditationRequest) (ledger: MeditationLedger) : ExitDecision =
    match deriveObligations request ledger with
    | [] ->
        match request.Contract with
        | Some { RequestedEvidenceMode = Empirical | Probabilistic } -> ExitDecision.Continue
        | Some { Goal = ClaimTest } when not (targetContested ledger) -> ExitDecision.Continue
        | _ ->
            ExitDecision.ContractSatisfied(
                ContractSatisfactionProof.create (contractDigest request) (buildProof request ledger)
            )
    | _ -> ExitDecision.Continue

/// useful-open-world prover（§10.5）：义务解除但合同维度未满足 → 有保留报告。
let tryProveOpenWorld (request: MeditationRequest) (ledger: MeditationLedger) : ExitDecision =
    match deriveObligations request ledger with
    | [] -> ExitDecision.OpenWorldReportReady(buildProof request ledger)
    | _ -> ExitDecision.Continue

/// §10.5 完整顺序：inconsistency → targetRefuted → fullContract → openWorld → blocked → inconclusive。
/// P0：inconsistency/targetRefuted/blocked 保持 Continue（对应出口由单元测试直接覆盖：
/// Contested 照常推进 §41.2；RefutationProof.toStopProof 单测；Blocked 由故障注入路径覆盖）。
/// 兜底 prover：义务空且 fullContract/openWorld 均未命中（防御性不可达分支）→ Inconclusive；
/// 义务非空 → Continue，交 seek 循环推进（义务非空但全部已 attempt 时由 Kernel 直接短路 Inconclusive）。
let scenarioProvers: ExitProver list =
    [ fun _ _ -> ExitDecision.Continue
      fun _ _ -> ExitDecision.Continue
      tryProveFullContract
      tryProveOpenWorld
      fun _ _ -> ExitDecision.Continue
      fun request ledger ->
          match deriveObligations request ledger with
          | [] -> ExitDecision.Inconclusive []
          | _ -> ExitDecision.Continue ]

let compileCanonical (intent: MeditationIntent) (ledger: MeditationLedger) (proof: StopProof) : CanonicalReport =
    let findings =
        ledger.Warrants
        |> Map.toList
        |> List.map (fun (_, w) ->
            let claim =
                match ledger.Claims.TryFind(Warrant.claimId w) with
                | Some c -> c
                | None -> failwith $"compileCanonical: warrant {warrantIdText (Warrant.id w)} has no claim"

            { Text = claim.Statement
              ClaimIds = [ claim.Id ]
              WarrantIds = [ Warrant.id w ]
              EvidenceIds = []
              Polarity = Warrant.polarity w
              Grade = gradeOfWarrant w
              Qualification = { ScopeNote = None; Caveats = [] } })

    let unknowns =
        ledger.UnknownRegions |> Map.toList |> List.map (fun (_, u) -> u.Description)

    let scope =
        match intent.Request.Contract with
        | Some c -> c.Scope
        | None -> scopeEarth

    CanonicalReport.compile
        intent.Request.Intent
        scope
        findings
        []
        []
        unknowns
        []
        (ReportGrade.Graded(StopProof.grade proof))
        proof

/// 报告 digest：与 ReportCodec 同一实现（重放一致性比较用）。
let reportDigest (r: MeditationReport) : string = ReportCodec.digest r
