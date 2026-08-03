// 第三阶段：ClaimTest 闭环（TASK.md 推荐实施顺序）。
// 请求 → framing → proposal → 支持 warrant → 反对 warrant → contested → 停止证明
// → 双侧报告 → 重放得到相同 digest。
module Meditator.Tests.ClaimTest

open System.Threading
open Meditator.Ledger
open Meditator.Meditation
open Meditator.Obligation
open Meditator.Stop
open Meditator.Report
open Meditator.Kernel
open Meditator.Tests.TestUtil
open Meditator.Tests.Scenario

let private mustReport (r: Result<MeditationReport, MeditationStop>) : MeditationReport =
    match r with
    | Ok report -> report
    | Error stop -> failwith $"expected Ok, got {stop}"

let private replayLedger (env: MeditationEnvironment) (lines: string list) : MeditationLedger =
    match replay env lines with
    | Ok(ledger, _) -> ledger
    | Error e -> failwith $"replay failed: {e}"

let run () =
    printfn "== ClaimTest 闭环 =="
    let failuresAtStart = failures

    // ── 1. 完整闭环：一次调用返回双侧报告，StopReason = ContractSatisfied。
    let journal = InMemoryJournal([])
    let env = makeEnv journal

    let firstResult =
        runMeditation env claimTestIntent executeScenario scenarioProvers compileCanonical

    match firstResult with
    | Error stop ->
        check "claim_test returns Ok" false
        printfn "     stop = %A" stop
    | Ok report ->
        check "stop reason = ContractSatisfied" (report.StopReason = ReportStopReason.ContractSatisfied)
        checkEq "findings (supports) = 1" 1 report.Findings.Length
        checkEq "counterpoints (opposes) = 1" 1 report.Counterpoints.Length
        check "provenance digests non-empty" (not (List.isEmpty report.Provenance))

    // 重放账本验证 contested 状态与停证独立重建（生产 replay 路径）。
    let lines = journal.Lines

    match replay env lines with
    | Error e ->
        check "replay after run succeeds" false
        printfn "     replay error = %s" e
    | Ok(ledger, _) ->
        check "replay after run succeeds" true
        check "target claim contested" (targetContested ledger)
        checkEq "two warrants folded" 2 ledger.Warrants.Count
        check "attempts recorded (>= 3 sweeps)" (ledger.Attempts.Count >= 3)
        checkEq "credits consumed = 3 (one per sweep)" 3 ledger.ResourceUsage.CreditsConsumed
        // 停证独立重建：账本重建的 proof 与报告内嵌 proof 一致（唯一权威来源）。
        let rebuilt = buildProof claimTestIntent.Request ledger

        match firstResult with
        | Ok report ->
            check
                "stop proof independently rebuilt from events"
                (StopProof.grade rebuilt = match report.EpistemicGrade with
                                           | ReportGrade.Graded g -> g
                                           | ReportGrade.NoEvidence -> StopProof.grade rebuilt)

            check "provenance digests = rebuilt proof digests" (report.Provenance = StopProof.eventDigests rebuilt)
        | Error _ -> ()

    // ── 2. 重放一致性：从同一 journal 行重新运行 → 同报告 digest、不新增行。
    let journal2 = InMemoryJournal(lines)
    let env2 = makeEnv journal2

    match runMeditation env2 claimTestIntent executeScenario scenarioProvers compileCanonical with
    | Error stop ->
        check "replay run returns Ok" false
        printfn "     stop = %A" stop
    | Ok report2 ->
        let first = mustReport firstResult
        check "replay run returns Ok" true
        checkEq "replay: same report digest" (reportDigest first) (reportDigest report2)
        check "replay: no new journal lines" (journal2.LineCount = journal.LineCount)
        check "replay: stop reason preserved" (report2.StopReason = ReportStopReason.ContractSatisfied)

    // ── 3. budget 超耗 = 非法状态（Inconsistent）：重放账本已消耗 3，InitialCredit=1 与账本矛盾。
    let journal3 = InMemoryJournal(lines)
    let env3 = makeEnv journal3

    let badIntent =
        { claimTestIntent with
            InitialCredit = 1 }

    match runMeditation env3 badIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconsistent _) -> check "budget over-consume is Inconsistent" true
    | _ -> check "budget over-consume is Inconsistent" false

    // ── 4. Empirical 契约：义务解除但合同维度未满足 → OpenWorldReportReady（有保留回答）。
    let journal4 = InMemoryJournal([])
    let env4 = makeEnv journal4

    let empiricalIntent =
        { claimTestIntent with
            Request =
                { claimTestIntent.Request with
                    Contract =
                        Some
                            { claimTestIntent.Request.Contract.Value with
                                RequestedEvidenceMode = Empirical } } }

    match runMeditation env4 empiricalIntent executeScenario scenarioProvers compileCanonical with
    | Ok report4 ->
        check "empirical contract → OpenWorldReportReady" (report4.StopReason = ReportStopReason.OpenWorldReportReady)

        check
            "open world report still shows both sides"
            (report4.Findings.Length = 1 && report4.Counterpoints.Length = 1)
    | Error _ -> check "empirical contract → OpenWorldReportReady" false

    // ── 5. Budget 耗尽：credit=1，义务未解除 → BudgetExhausted + 未决义务非空。
    let journal5 = InMemoryJournal([])
    let env5 = makeEnv journal5

    let tinyIntent =
        { claimTestIntent with
            InitialCredit = 1 }

    match runMeditation env5 tinyIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.BudgetExhausted unresolved) ->
        check "budget exhausted returns unresolved obligations" (not (List.isEmpty unresolved))
    | _ -> check "budget exhausted returns unresolved obligations" false

    // ── 6. P0-3：同一 Journal 只能被同一 (请求, 预算) 复用——异请求/异预算 → Inconsistent。
    let otherRequestIntent =
        { claimTestIntent with
            Request =
                { claimTestIntent.Request with
                    Intent = "A completely different question" } }

    let journal6 = InMemoryJournal(lines)
    let env6 = makeEnv journal6

    match runMeditation env6 otherRequestIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconsistent _) -> check "different request on same journal → Inconsistent" true
    | _ -> check "different request on same journal → Inconsistent" false

    let biggerBudgetIntent =
        { claimTestIntent with
            InitialCredit = 99 }

    let journal7 = InMemoryJournal(lines)
    let env7 = makeEnv journal7

    match runMeditation env7 biggerBudgetIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconsistent _) -> check "different InitialCredit on same journal → Inconsistent" true
    | _ -> check "different InitialCredit on same journal → Inconsistent" false

    // ── 6b. P0-1：RequiredSections/UnacceptableClaims 是契约身份的一部分——
    //     变更后复用旧 journal → Inconsistent（旧契约证明不得匹配较弱合同）。
    let sectionsChangedIntent =
        { claimTestIntent with
            Request =
                { claimTestIntent.Request with
                    Contract =
                        Some
                            { claimTestIntent.Request.Contract.Value with
                                RequiredSections = [ ReportSection.Findings ] } } }

    let journal6b = InMemoryJournal(lines)
    let env6b = makeEnv journal6b

    match runMeditation env6b sectionsChangedIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconsistent _) -> check "RequiredSections change rejects old journal" true
    | _ -> check "RequiredSections change rejects old journal" false

    let forbiddenChangedIntent =
        { claimTestIntent with
            Request =
                { claimTestIntent.Request with
                    Contract =
                        Some
                            { claimTestIntent.Request.Contract.Value with
                                UnacceptableClaims = [ "this is unacceptable" ] } } }

    let journal6c = InMemoryJournal(lines)
    let env6c = makeEnv journal6c

    match runMeditation env6c forbiddenChangedIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconsistent _) -> check "UnacceptableClaims change rejects old journal" true
    | _ -> check "UnacceptableClaims change rejects old journal" false

    // ── 7. 139 版评审 #12：executor 事件必须绑定当前义务——SearchAttempted 不是
    //    FrameClaim/GO/GE 义务的合法完成（无进展事件被 validateContribution 拒），
    //    结果 Inconclusive + journal 无该事件（不污染）。
    let stallExecute: ObligationExecutor =
        fun _obligation ->
            meditation {
                return
                    [ SearchAttempted
                          { ObligationId = "stall"
                            Outcome = NoHit
                            Sequence = 0 } ]
            }

    let journal8 = InMemoryJournal([])
    let env8 = makeEnv journal8

    match runMeditation env8 claimTestIntent stallExecute scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check "unbound contribution stops with Inconclusive" (unresolved |> List.exists (fun u -> u.Kind = "execute"))
    | _ -> check "unbound contribution stops with Inconclusive" false

    check "unbound contribution is not appended" (not (journal8.Lines |> List.exists (fun l -> l.Contains "SA")))

    // ── 7c. 139 版评审 #12：义务绑定极性——GO 义务交 supporting warrant → 拒。
    let wrongPolarityExecute: ObligationExecutor =
        fun obligation ->
            match obligation.Kind with
            | GenerateOpposition -> meditation { return [ ContributionAccepted supportingWarrant ] } // Supports 而非 Opposes
            | _ -> executeScenario obligation

    let journal8c = InMemoryJournal([])
    let env8c = makeEnv journal8c

    match runMeditation env8c claimTestIntent wrongPolarityExecute scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check
            "GO obligation rejects supporting warrant (polarity binding)"
            (unresolved |> List.exists (fun u -> u.Kind = "execute"))
    | _ -> check "GO obligation rejects supporting warrant (polarity binding)" false

    // ── 7b. P0-6（138 版）：executor 不能发控制事件；非法批次 preflight 拒绝且不污染 journal。
    let rogueExecute: ObligationExecutor =
        fun _obligation -> meditation { return [ MeditationCompleted "fake" ] }

    let journal8b = InMemoryJournal([])
    let env8b = makeEnv journal8b

    match runMeditation env8b claimTestIntent rogueExecute scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check "executor cannot emit MeditationCompleted" (unresolved |> List.exists (fun u -> u.Kind = "execute"))
    | _ -> check "executor cannot emit MeditationCompleted" false

    check
        "invalid executor event is not appended (no journal pollution)"
        (not (journal8b.Lines |> List.exists (fun l -> l.Contains "MC")))

    let missingClaimExecute: ObligationExecutor =
        fun _obligation ->
            meditation {
                // supportingWarrant 指向 targetClaim，但 claim 尚未 frame——fold 拒（MissingClaim），
                // preflight 必须拦截，不写入。
                return [ ContributionAccepted supportingWarrant ]
            }

    let journal8c = InMemoryJournal([])
    let env8c = makeEnv journal8c

    match runMeditation env8c claimTestIntent missingClaimExecute scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check
            "invalid batch rejected by preflight (no pollution)"
            (unresolved |> List.exists (fun u -> u.Kind = "execute"))
    | _ -> check "invalid batch rejected by preflight (no pollution)" false

    check "invalid batch leaves journal clean" (not (journal8c.Lines |> List.exists (fun l -> l.Contains "W:")))

    // ── 7d. 140 版：ClaimTest 合同语义 = MinimalDialecticCompleted（EPISTEMICS §6）——
    //     仅单侧证据不满足合同（GO 义务未解除 → 义务非空 → ContractSatisfied 永不触发）。
    let singleSideExecute: ObligationExecutor =
        fun obligation ->
            match obligation.Kind with
            | GenerateOpposition -> meditation { return [] } // 不提供反对面
            | _ -> executeScenario obligation

    let journal8d = InMemoryJournal([])
    let env8d = makeEnv journal8d

    match runMeditation env8d claimTestIntent singleSideExecute scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconclusive _) ->
        check "single-side evidence cannot satisfy ClaimTest contract (minimal dialectic requires both sides)" true
    | Ok _ ->
        check "single-side evidence cannot satisfy ClaimTest contract (minimal dialectic requires both sides)" false
    | Error _ ->
        check "single-side evidence cannot satisfy ClaimTest contract (minimal dialectic requires both sides)" false

    // ── 8. P1-2：伪造 report compiler 被 Kernel 拒绝——findings 引用账本中不存在的 claim。
    let fakeCompiler (intent: MeditationIntent) (_ledger: MeditationLedger) (proof: StopProof) : CanonicalReport =
        let ghostClaimId = ClaimId.ofProposition "ghost-claim" scopeEarth

        let fakeFinding =
            { Text = "ghost"
              ClaimIds = [ ghostClaimId ]
              WarrantIds = []
              EvidenceIds = []
              Polarity = Supports
              Grade = gradeOfWarrant supportingWarrant
              Qualification = { ScopeNote = None; Caveats = [] } }

        CanonicalReport.compile
            intent.Request.Intent
            scopeEarth
            [ fakeFinding ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade proof))
            proof

    let journal9 = InMemoryJournal([])
    let env9 = makeEnv journal9

    match runMeditation env9 claimTestIntent executeScenario scenarioProvers fakeCompiler with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check "fake report compiler rejected" (unresolved |> List.exists (fun u -> u.Kind = "exit-proof"))
    | _ -> check "fake report compiler rejected" false

    // ── 9. P1-2 契约绑定：ContractSatisfied 的 contractDigest 必须匹配当前请求——
    //     "用另一个请求的契约 digest 证明当前契约已满足"必须被 Kernel 拒绝。
    let wrongContractProvers: ExitProver list =
        [ fun _ _ -> ExitDecision.Continue
          fun _ _ -> ExitDecision.Continue
          fun request ledger ->
              match deriveObligations request ledger with
              | [] ->
                  ExitDecision.ContractSatisfied(
                      ContractSatisfactionProof.create
                          "wrong-contract-digest"
                          (buildProof claimTestIntent.Request ledger)
                  )
              | _ -> ExitDecision.Continue
          tryProveOpenWorld
          fun _ _ -> ExitDecision.Continue
          fun request ledger ->
              match deriveObligations request ledger with
              | [] -> ExitDecision.Inconclusive []
              | _ -> ExitDecision.Continue ]

    let journal10 = InMemoryJournal([])
    let env10 = makeEnv journal10

    match runMeditation env10 claimTestIntent executeScenario wrongContractProvers compileCanonical with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check "contract digest mismatch rejected" (unresolved |> List.exists (fun u -> u.Kind = "exit-proof"))
    | _ -> check "contract digest mismatch rejected" false

    // ── 10. review 回归：verifyCanonicalReport 拒绝 ghost evidence 引用。
    let fakeEvidenceCompiler
        (intent: MeditationIntent)
        (_ledger: MeditationLedger)
        (proof: StopProof)
        : CanonicalReport =
        let fakeFinding =
            { Text = "ghost evidence"
              ClaimIds = []
              WarrantIds = []
              EvidenceIds = [ "ghost-evidence" ]
              Polarity = Supports
              Grade = gradeOfWarrant supportingWarrant
              Qualification = { ScopeNote = None; Caveats = [] } }

        CanonicalReport.compile
            intent.Request.Intent
            scopeEarth
            [ fakeFinding ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade proof))
            proof

    let journal11 = InMemoryJournal([])
    let env11 = makeEnv journal11

    match runMeditation env11 claimTestIntent executeScenario scenarioProvers fakeEvidenceCompiler with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check "fake evidence reference rejected" (unresolved |> List.exists (fun u -> u.Kind = "exit-proof"))
    | _ -> check "fake evidence reference rejected" false

    // ── 11. review 回归：conclude 恢复路径（expectedCompletedDigest）拒绝历史 digest 不一致。
    let digestProof = buildProof claimTestIntent.Request MeditationLedger.Empty

    let digestCanonical =
        CanonicalReport.compile
            "intent"
            scopeEarth
            []
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade digestProof))
            digestProof

    let digestDecision = ExitDecision.OpenWorldReportReady digestProof
    let journal12 = InMemoryJournal([])
    let env12 = makeEnv journal12

    let concludeResult =
        (conclude 0 (Some "wrong-history-digest") digestProof digestDecision digestCanonical)
            env12
            CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    match concludeResult with
    | Error(MeditationStop.Inconsistent _) -> check "restored path rejects history digest mismatch" true
    | _ -> check "restored path rejects history digest mismatch" false

    // ── 12. P0-4：空引用 finding（自由文本）被拒绝；polarity 与 warrant 不一致被拒绝。
    let emptyRefCompiler (intent: MeditationIntent) (_ledger: MeditationLedger) (proof: StopProof) : CanonicalReport =
        let freeTextFinding =
            { Text = "任意未经支持的结论"
              ClaimIds = []
              WarrantIds = []
              EvidenceIds = []
              Polarity = Supports
              Grade = gradeOfWarrant supportingWarrant
              Qualification = { ScopeNote = None; Caveats = [] } }

        CanonicalReport.compile
            intent.Request.Intent
            scopeEarth
            [ freeTextFinding ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade proof))
            proof

    let journal13 = InMemoryJournal([])
    let env13 = makeEnv journal13

    match runMeditation env13 claimTestIntent executeScenario scenarioProvers emptyRefCompiler with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check "empty-reference finding rejected" (unresolved |> List.exists (fun u -> u.Kind = "exit-proof"))
    | _ -> check "empty-reference finding rejected" false

    let wrongPolarityCompiler
        (intent: MeditationIntent)
        (ledger: MeditationLedger)
        (proof: StopProof)
        : CanonicalReport =
        // 引用真实 warrant 但 polarity 反转（支持 warrant 标成 Opposes）。
        let wrongFinding =
            { Text = "wrong polarity"
              ClaimIds = [ targetClaim.Id ]
              WarrantIds = [ Warrant.id supportingWarrant ]
              EvidenceIds = []
              Polarity = Opposes
              Grade = gradeOfWarrant supportingWarrant
              Qualification = { ScopeNote = None; Caveats = [] } }

        CanonicalReport.compile
            intent.Request.Intent
            scopeEarth
            [ wrongFinding ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade proof))
            proof

    let journal14 = InMemoryJournal([])
    let env14 = makeEnv journal14

    match runMeditation env14 claimTestIntent executeScenario scenarioProvers wrongPolarityCompiler with
    | Error(MeditationStop.Inconclusive unresolved) ->
        check "finding polarity mismatch rejected" (unresolved |> List.exists (fun u -> u.Kind = "exit-proof"))
    | _ -> check "finding polarity mismatch rejected" false

    // ── 13. P0-4：expectedProof 统一锚——canonical 停证 ≠ Kernel 验证的停证 → 拒绝。
    let otherProof =
        match
            StopProof.create
                (StopProof.discharged digestProof)
                (StopProof.unknowns digestProof)
                (StopProof.coverage digestProof)
                { StopProof.grade digestProof with
                    Reliability = Tentative }
                (StopProof.prohibited digestProof)
                (StopProof.eventDigests digestProof)
        with
        | Ok p -> p
        | Error e -> failwith e

    let otherCanonical =
        CanonicalReport.compile
            "intent"
            scopeEarth
            []
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade otherProof))
            otherProof

    let mismatchConcludeResult =
        (conclude 0 None digestProof digestDecision otherCanonical) env12 CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    match mismatchConcludeResult with
    | Error(MeditationStop.Inconclusive _) -> check "canonical proof must match verified expected proof" true
    | _ -> check "canonical proof must match verified expected proof" false

    failures = failuresAtStart
