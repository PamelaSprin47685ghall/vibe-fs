// 第二阶段收尾：崩溃注入测试（TASK.md 第二阶段：Replay/Conflict/CommitUnknown reconcile/版本校验/崩溃注入）。
// 重启前后 ledger、report、budget 结果一致；CommitUnknown fail closed（PERSIST-003）。
module Meditator.Tests.Crash

open System.Threading
open Meditator.Ledger
open Meditator.Meditation
open Meditator.Obligation
open Meditator.Report
open Meditator.Kernel
open Meditator.Tests.TestUtil
open Meditator.Tests.Scenario

let run () =
    printfn "== 崩溃注入 =="
    let failuresAtStart = failures

    // ── 1. append 后 fold 前崩溃：行已写，异常抛出；重跑从行恢复 → 成功且不重复。
    let journal = InMemoryJournal([], crashAfterAppend = true)
    let env = makeEnv journal

    let crashed =
        try
            runMeditation env claimTestIntent executeScenario scenarioProvers compileCanonical
            |> ignore

            false
        with _ ->
            true

    check "crash after append raises" crashed
    check "line persisted before crash" (journal.LineCount >= 1)

    let journal2 = InMemoryJournal(journal.Lines)
    let env2 = makeEnv journal2

    match runMeditation env2 claimTestIntent executeScenario scenarioProvers compileCanonical with
    | Error stop ->
        check "recovery after crash succeeds" false
        printfn "     stop = %A" stop
    | Ok report ->
        check "recovery after crash succeeds" true
        check "recovered report has both sides" (report.Findings.Length = 1 && report.Counterpoints.Length = 1)
        check "recovered stop reason = ContractSatisfied" (report.StopReason = ReportStopReason.ContractSatisfied)

    // ── 2. CommitUnknown → Reconcile 确认已提交 → 继续成功（PERSIST-003 出口）。
    let journal3 = InMemoryJournal([], unknownFirstAppend = true)
    let env3 = makeEnv journal3

    match runMeditation env3 claimTestIntent executeScenario scenarioProvers compileCanonical with
    | Error stop ->
        check "CommitUnknown reconciled → Ok" false
        printfn "     stop = %A" stop
    | Ok report ->
        check "CommitUnknown reconciled → Ok" true
        check "reconciled report contested" (report.Findings.Length = 1 && report.Counterpoints.Length = 1)

    // ── 3. CommitUnknown → StillUnknown → Blocked（fail closed，不重发不猜测）。
    let journal4 = InMemoryJournal([], unknownFirstAppend = true, neverReconcile = true)
    let env4 = makeEnv journal4

    match runMeditation env4 claimTestIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Blocked _) -> check "unreconcilable CommitUnknown → Blocked" true
    | _ -> check "unreconcilable CommitUnknown → Blocked" false

    // ── 3b. 其他 2：完成事件的 CommitUnknown 走 Reconcile（与普通事件同语义），
    //     确认已提交 → 成功返回（不再立即 Blocked）。
    let journal4b = InMemoryJournal([], unknownEveryAppend = true)
    let env4b = makeEnv journal4b

    match runMeditation env4b claimTestIntent executeScenario scenarioProvers compileCanonical with
    | Ok report ->
        check "completion CommitUnknown reconciled → Ok" (report.StopReason = ReportStopReason.ContractSatisfied)
    | Error stop ->
        check "completion CommitUnknown reconciled → Ok" false
        printfn "     stop = %A" stop

    // ── 3c. review 回归：已完成 journal（MC 是严格终态）→ 跳过 seek 直接尾部 tryProve——
    //     义务非空的历史日志恢复为 Inconclusive（fail-closed），不误伤为 Blocked。
    let completedMrDigest =
        EventCodec.sha256Hex (
            canonicalRequest claimTestIntent.Request
            + EventCodec.field "c" (string claimTestIntent.InitialCredit)
        )

    let completedMrLine =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested completedMrDigest)

    let completedOaLine =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 1 (OracleInvocationAccepted("k1", "d1"))

    let completedMcLine =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 2 (MeditationCompleted "report-digest")

    let completedJournal =
        InMemoryJournal([ completedMrLine; completedOaLine; completedMcLine ])

    let completedEnv = makeEnv completedJournal

    match runMeditation completedEnv claimTestIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Inconclusive _) -> check "completed journal skips seek (no false Blocked)" true
    | Error(MeditationStop.Blocked _) ->
        check "completed journal skips seek (no false Blocked)" false
        printfn "     completed journal falsely Blocked"
    | _ -> check "completed journal skips seek (no false Blocked)" false

    // ── 4. 同 EventId 异字节 → Conflict（S2 违规，journal 层检测；appendAndFold 将其译为 Inconsistent）。
    let requestLine =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested "x")

    let forged = replaceField requestLine "D" "deadbeef" // 同 EventId 异字节
    let journal5 = InMemoryJournal([ forged ])

    let conflictOutcome =
        (journal5 :> IMeditationJournal).Append 1 requestLine CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    checkEq "forged bytes → Conflict (S2)" AppendOutcome.Conflict conflictOutcome

    // ── 5. 版本漂移：重放行与当前 policy/reducer 不匹配 → Blocked（fail closed，不猜测迁移）。
    let driftedLine =
        EventCodec.encode EventSchemaVersion "policy-other/v9" reducerVersion 0 (MeditationRequested "x")

    let journal6 = InMemoryJournal([ driftedLine ])
    let env6 = makeEnv journal6

    match runMeditation env6 claimTestIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Blocked _) -> check "policy version drift → Blocked" true
    | _ -> check "policy version drift → Blocked" false

    // ── 6. 尾部行损坏（digest 失配）→ Blocked，不跳过继续。
    let goodLine =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested "x")

    let corruptLine = replaceField goodLine "D" "c0ffee"
    let journal7 = InMemoryJournal([ corruptLine ])
    let env7 = makeEnv journal7

    match runMeditation env7 claimTestIntent executeScenario scenarioProvers compileCanonical with
    | Error(MeditationStop.Blocked _) -> check "corrupted tail line → Blocked" true
    | _ -> check "corrupted tail line → Blocked" false

    failures = failuresAtStart
