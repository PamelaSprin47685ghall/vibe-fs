// Meditator DSL — 主循环。语法化：演算 §1 big-step 求值（S;K ⊢ M ⇓ S′;K′）。
// 七巧板的拼图板：方法 = 拼板（Methods.*），类型 = 拼接口，本文件 = 底板——
// 底板不认识任何拼板；execute 由装配处按 §14.5 静态映射注入。
// 恢复 = Replay 事件 → 版本校验 → fold 事实 + 重新执行普通程序（§9.1/§38.3）；
// 不存、不恢复程序位置。budget 从 CreditsConsumed 恢复，不重新获得初始 credit。
// 进展保证（§11）：attempt key 去重、NoProgress 记账、连续无进展只导向 OpenWorld/Inconclusive。
// 编译顺序：10（依赖上游全部）。
// 注：%A 仅保留在诊断错误消息（foldError/replay 错误文本）——不进报告/canonical/digest，
// 不参与任何持久化身份；义务 ID 与 UnresolvedProblem.Kind 等身份字段一律显式标签（P0-8）。
module Meditator.Kernel

open System.Threading
open Meditator.Meditation
open Meditator.Budget
open Meditator.Ledger
open Meditator.Obligation
open Meditator.Stop
open Meditator.Report

/// §36.1 公开契约的输入。
type MeditationIntent =
    { Request: MeditationRequest
      InitialCredit: int }

/// 义务执行器：控制层的静态分派（§14.5 eligibleMethods 是"义务 → 方法族"的静态映射，
/// 不是运行时 registry）。实现 = 手写 match，每个分支调一个方法函数——
//  这等价于 §36.4 的 applyAllMatching*：手写控制流段落，不是 registry |> filter |> map。
/// 返回事件批次（P0-2）：oracle 调用产生的 OracleInvocationAccepted 与领域事件
/// 由同一个批次携带，Kernel 统一 append+fold——同一运行与重放结果一致。
type ObligationExecutor = Obligation -> Meditation<MeditationEvent list>

/// 报告编译器：从停证与账本机械生成 CanonicalReport（§11.1）。
/// 策略注入：Kernel 不复制报告逻辑（Host adapter 同样不得复制，实施指南 §13.5）。
/// 输入是 StopProof（ContractSatisfied 的证明内嵌停证；OpenWorld/TargetRefuted 出口直接是停证或反驳证明）。
type ReportCompiler = MeditationIntent -> MeditationLedger -> StopProof -> CanonicalReport

/// 连续无进展上限（演算 §11）：Stalls ≥ 此值 → 停止执行义务，
/// 只允许 OpenWorld 或 Inconclusive 出口（ContractSatisfied 在义务未解除时结构上不可证明）。
[<Literal>]
let MaxStallsBeforeStopping = 3

/// 请求 + 初始预算的 canonical digest（P0-3）：InitialCredit 纳入身份——
/// 同一 Journal 只能被同一 (请求, 预算) 复用；异请求/异预算重放 = 非法状态（Inconsistent）。
/// 请求部分委托 Obligation.canonicalRequest（P0-1 单一权威：含完整 AnswerContract）。
let private requestDigest (intent: MeditationIntent) : string =
    EventCodec.sha256Hex (
        canonicalRequest intent.Request
        + EventCodec.field "c" (string intent.InitialCredit)
    )

let private snapshotUnresolved (request: MeditationRequest) (ledger: MeditationLedger) : UnresolvedProblem list =
    deriveObligations request ledger
    |> List.map (fun o ->
        let subjects = String.concat ", " o.SubjectIds

        { ObligationId = o.Id
          Kind = obligationKindTag o.Kind
          Description = $"subjects: {subjects}" })

/// 重放：decode 全部行 → 版本校验（schema/policy/reducer 与当前装配一致，fail closed）→ fold。
/// 返回 (账本, 历史请求 digest option)（P0-3：None = 尚无 MeditationRequested；
/// Some d = 首个 MeditationRequested 的负载 digest——meditate 必须与当前 intent 比对）。
/// 公开：测试与恢复工具以生产路径独立重建账本（§10.2 停证可独立重放）。
let replay (env: MeditationEnvironment) (lines: string list) : Result<MeditationLedger * string option, string> =
    let mutable ledger = MeditationLedger.Empty
    let mutable historicalDigest: string option = None

    let rec go (remaining: string list) (seqNo: int) : Result<MeditationLedger * string option, string> =
        match remaining with
        | [] -> Ok(ledger, historicalDigest)
        | line :: rest ->
            match EventCodec.decode line with
            | Error e -> Error $"replay: line {seqNo}: {e}"
            | Ok envelope ->
                // P1-1：sequence 必须连续（0,1,2,…）——检测删行/重排/缺口/重复序列。
                if envelope.Sequence <> seqNo then
                    Error
                        $"replay: line {seqNo}: sequence {envelope.Sequence} != expected {seqNo} (gap, reorder, or duplicate)"
                // P1-1：首行必须是 MeditationRequested；多条 MR 的 digest 必须一致（拒绝混入异请求）。
                elif
                    seqNo = 0
                    && (match envelope.Payload with
                        | MeditationRequested _ -> false
                        | _ -> true)
                then
                    Error "replay: first line must be MeditationRequested"
                // §9.1 版本校验：旧 schema/policy/reducer 不猜测迁移（PERSIST-005 同构）。
                elif envelope.SchemaVersion <> EventSchemaVersion then
                    Error
                        $"replay: schema version {envelope.SchemaVersion} != {EventSchemaVersion}; migration not supported"
                elif envelope.PolicyVersion <> env.PolicyVersion then
                    Error $"replay: policy version {envelope.PolicyVersion} != {env.PolicyVersion}"
                elif envelope.ReducerVersion <> env.ReducerVersion then
                    Error $"replay: reducer version {envelope.ReducerVersion} != {env.ReducerVersion}"
                else
                    match fold ledger envelope.Payload with
                    | Error foldError -> Error $"replay: line {seqNo}: %A{foldError}"
                    | Ok nextLedger ->
                        ledger <- nextLedger

                        let digestError =
                            match envelope.Payload with
                            | MeditationRequested d ->
                                match historicalDigest with
                                | None ->
                                    historicalDigest <- Some d
                                    None
                                // P1-1：后续 MR 的 digest 必须与首条一致（混入异请求 = 非法日志）。
                                | Some existing when existing <> d ->
                                    Some $"replay: line {seqNo}: multiple MeditationRequested with different digests"
                                | Some _ -> None
                            | _ -> None

                        match digestError with
                        | Some e -> Error e
                        | None -> go rest (seqNo + 1)

    go lines 0

/// 追加一个事件并 fold；AlreadyCommitted 视为成功（幂等重放）。
/// 失败统一翻译为 MeditationStop（Meditation<'a> 的 Error 通道）：
/// Conflict = S2 违规（Inconsistent）；CommitUnknown 无法 reconcile = 阻塞（Blocked）；
/// fold 失败 = 阻塞。先 append 成功再 fold（C1 侧条件）。
/// appendAndFold（139 版）：internal——meditate 是唯一写入入口（§4.1/评审 #6）。
/// sequence 参数已删除（评审 #7）：序列单一来源 = ledger.EventCount（调用方提供两次会漂移）。
let internal appendAndFold
    (env: MeditationEnvironment)
    (ledger: MeditationLedger)
    (event: MeditationEvent)
    : Meditation<MeditationLedger> =
    meditation {
        let sequence = ledger.EventCount

        let line =
            EventCodec.encode EventSchemaVersion env.PolicyVersion env.ReducerVersion sequence event

        let eventId =
            eventIdText (EventCodec.eventId EventSchemaVersion env.PolicyVersion env.ReducerVersion sequence event)

        let! outcome = ofTask (fun ct -> env.Journal.Append ledger.EventCount line ct)

        match outcome with
        | Committed
        | AlreadyCommitted ->
            match fold ledger event with
            | Error foldError ->
                return!
                    halt (
                        MeditationStop.Blocked
                            [ { What = "ledger fold"
                                WhyNeeded = $"append committed but fold failed: %A{foldError}" } ]
                    )
            | Ok next -> return next
        | Conflict ->
            return!
                halt (
                    MeditationStop.Inconsistent
                        [ { SubjectId = "journal"
                            SupportDigest = eventId
                            OpposeDigest = "duplicate EventId with different bytes (S2)" } ]
                )
        // 138 版：并发追加冲突（两个进程从同一 revision 追加）——fail closed。
        | WrongExpectedRevision actual ->
            return!
                halt (
                    MeditationStop.Blocked
                        [ { What = "journal append"
                            WhyNeeded =
                              $"expected revision {ledger.EventCount} but journal has {actual} rows (concurrent append?)" } ]
                )
        | CommitUnknown ->
            // PERSIST-003：不重新请求模型"保证写入"；fail closed 到 reconcile。
            let! reconcile = ofTask (fun ct -> env.Journal.Reconcile eventId line ct)

            match reconcile with
            | Reconciled(Committed)
            | Reconciled(AlreadyCommitted) ->
                match fold ledger event with
                | Error foldError ->
                    return!
                        halt (
                            MeditationStop.Blocked
                                [ { What = "ledger fold"
                                    WhyNeeded = $"append reconciled but fold failed: %A{foldError}" } ]
                        )
                | Ok next -> return next
            | Reconciled(Conflict) ->
                return!
                    halt (
                        MeditationStop.Inconsistent
                            [ { SubjectId = "journal"
                                SupportDigest = eventId
                                OpposeDigest = "reconciled as Conflict (S2)" } ]
                    )
            | Reconciled(CommitUnknown)
            | StillUnknown ->
                return!
                    halt (
                        MeditationStop.Blocked
                            [ { What = "journal reconcile"
                                WhyNeeded = "event append outcome unknown; fail closed per PERSIST-003" } ]
                    )
    }

/// 按顺序提交事件批次（P0-2）：每个事件先 append 成功再 fold，sequence 连续递增；
/// 与重放路径的 fold 完全一致，无"中间多写一条但当前运行看不到"的分裂。
let rec private applyEvents
    (env: MeditationEnvironment)
    (ledger: MeditationLedger)
    (events: MeditationEvent list)
    : Meditation<MeditationLedger> =
    match events with
    | [] -> meditation { return ledger }
    | e :: rest ->
        meditation {
            let! next = appendAndFold env ledger e
            return! applyEvents env next rest
        }

/// P0-6（138 版）：executor 事件批次必须先内存 preflight（foldAll 合法性 + 控制事件白名单）——
/// 非法事件不写入 journal（写入后被 fold 拒会永久污染日志，重启 Replay 再失败）。
/// 139 版评审 #12：**绑定当前义务**——事件必须完成当前 obligation（GO:A 不能交 B 的 warrant）。
/// 139 版 review should-fix：**全量校验**（List.forall 而非 List.exists）——批次不得夹带
/// 不匹配的方法贡献事件（夹带可提前满足其他义务、绕过无进展保护）。
let private validateContribution (obligation: Obligation) (events: MeditationEvent list) : Result<unit, string> =
    let subjectIdText = obligation.SubjectIds |> List.tryHead |> Option.defaultValue ""

    // 方法贡献事件（ClaimFramed/ContributionAccepted/SearchAttempted/EpisodeRecorded）；
    // OracleInvocation*/EvidenceObserved 等辅助事件不参与义务完成判定。
    let contributionEvents =
        events
        |> List.filter (function
            | ClaimFramed _
            | ContributionAccepted _
            | SearchAttempted _
            | EpisodeRecorded _ -> true
            | _ -> false)

    match obligation.Kind with
    | FrameClaim ->
        let allMatch =
            contributionEvents
            |> List.forall (function
                | ClaimFramed c -> claimIdText c.Id = subjectIdText
                | _ -> false)

        if allMatch then
            Ok()
        else
            Error "FrameClaim obligation must produce exactly the target ClaimFramed"
    | GenerateOpposition ->
        let allMatch =
            contributionEvents
            |> List.forall (function
                | ContributionAccepted w ->
                    claimIdText (Warrant.claimId w) = subjectIdText && Warrant.polarity w = Opposes
                | _ -> false)

        if allMatch then
            Ok()
        else
            Error "GenerateOpposition must produce only opposing warrants for the subject claim"
    | GroundEvidence ->
        let allMatch =
            contributionEvents
            |> List.forall (function
                | ContributionAccepted w ->
                    claimIdText (Warrant.claimId w) = subjectIdText && Warrant.polarity w = Supports
                | _ -> false)

        if allMatch then
            Ok()
        else
            Error "GroundEvidence must produce only supporting warrants for the subject claim"
    | CheckCounterexample ->
        // 139 版：SearchAttempted/EpisodeRecorded 的 ObligationId 必须等于当前义务。
        let allMatch =
            contributionEvents
            |> List.forall (function
                | SearchAttempted sa -> sa.ObligationId = obligation.Id
                | EpisodeRecorded ep -> ep.ObligationId = obligation.Id
                | _ -> false)

        if allMatch then
            Ok()
        else
            Error "auxiliary events must reference the current obligation"

let private preflightEvents
    (ledger: MeditationLedger)
    (obligation: Obligation)
    (events: MeditationEvent list)
    : Result<unit, string> =
    let isMethodContribution (e: MeditationEvent) : bool =
        match e with
        | ClaimFramed _
        | ContributionAccepted _
        | OracleInvocationClaimed _
        | OracleInvocationAccepted _
        | OracleAnswerRejected _
        | EvidenceObserved _
        | HypothesisRecorded _
        | ConceptRecorded _
        | RelationRecorded _
        | CounterexampleRecorded _
        | UnknownRegionUpdated _
        | SearchAttempted _
        | EpisodeRecorded _ -> true
        | MeditationRequested _
        | MeditationCompleted _
        | MeditationFailed _
        | CreditsConsumed _
        | AttemptRecorded _
        | NoProgress _
        | SweepCompleted -> false

    if not (List.forall isMethodContribution events) then
        Error "executor emitted kernel control event"
    else
        match foldAll ledger events with
        | Error e -> Error $"invalid event batch: %A{e}"
        | Ok _ -> validateContribution obligation events

/// 主循环（实施指南 §9 形状）：每轮最多 4+ 个事件（execute 批次、AttemptRecorded、NoProgress、CreditsConsumed），
/// 每个事件都是"先 append 成功再 fold"（C1 侧条件）；序列 = 已折叠事件数（EventId 的序列分量）。
/// 尾递归：F# 编译为循环，栈无界增长不存在（ARCH-001：语言运行时提供 continuation）。
let rec seek
    (execute: ObligationExecutor)
    (provers: ExitProver list)
    (request: MeditationRequest)
    (ledger: MeditationLedger)
    (budget: Budget)
    : Meditation<MeditationLedger> =
    meditation {
        let! env = ask

        match tryProve provers request ledger with
        | ExitDecision.Continue ->
            if not (Budget.canContinue budget) then
                return! halt (MeditationStop.BudgetExhausted(snapshotUnresolved request ledger))
            elif ledger.ResourceUsage.Stalls >= MaxStallsBeforeStopping then
                // 连续无进展（§11）：停止执行义务，只允许 OpenWorld/Inconclusive 出口。
                return ledger
            else
                // P1-7：区分"派生为空"与"派生非空但全部已 attempt"——后者是真实未决状态
                // （携带 unresolved 清单），不是"无义务"（原 selectDeterministically None 分支
                // 对非空 list 永远 Some，else 分支是死代码）。
                let derived = deriveObligations request ledger

                let eligible =
                    derived
                    |> List.filter (fun o -> not (ledger.Attempts.Contains(attemptKey o.Id ledger env.PolicyVersion)))

                match derived, eligible with
                | [], _ ->
                    // 义务空：交 prover 出口（OpenWorld/Inconclusive）。
                    return ledger
                | _ :: _, [] ->
                    // 义务存在但全部已 attempt（§11 进展保证）：无法再推进 → 无结论 + 未决清单。
                    return! halt (MeditationStop.Inconclusive(snapshotUnresolved request ledger))
                | _, _ ->
                    match selectDeterministically request.MethodHints eligible with
                    | None ->
                        // 防御：eligible 非空时 select 必有 Some；不可达即实现 bug。
                        return! halt (MeditationStop.Inconclusive(snapshotUnresolved request ledger))
                    | Some obligation ->
                        let key = attemptKey obligation.Id ledger env.PolicyVersion
                        let beforeDigest = epistemicDigest ledger

                        let! events = execute obligation

                        // P0-6：preflight——非法批次不写入 journal（fail closed，无污染）。
                        match preflightEvents ledger obligation events with
                        | Error reason ->
                            return!
                                halt (
                                    MeditationStop.Inconclusive
                                        [ { ObligationId = obligation.Id
                                            Kind = "execute"
                                            Description = reason } ]
                                )
                        | Ok() ->

                            let! afterExecute = applyEvents env ledger events
                            let changed = epistemicDigest afterExecute <> beforeDigest

                            // 1. attempt 记账（key = 执行前的 (obligationId, digest, policyVersion)）。
                            let! afterAttempt = appendAndFold env afterExecute (AttemptRecorded key)

                            // 2. 无进展记账：execute 事件未改变语义 digest。
                            let! afterNoProgress =
                                if changed then
                                    meditation { return afterAttempt }
                                else
                                    appendAndFold
                                        env
                                        afterAttempt
                                        (NoProgress(
                                            obligation.Id,
                                            EventCodec.sha256Hex (
                                                EventCodec.field "o" key.ObligationId
                                                + EventCodec.field "d" key.LedgerDigest
                                                + EventCodec.field "p" key.PolicyVersion
                                            )
                                        ))

                            // 3. credit 记账：每次展开至少消耗 1（R1）。
                            let! finalLedger = appendAndFold env afterNoProgress (CreditsConsumed 1)

                            match Budget.consume 1 budget with
                            | Ok nextBudget -> return! seek execute provers request finalLedger nextBudget
                            | Error err ->
                                return!
                                    halt (
                                        MeditationStop.Inconclusive
                                            [ { ObligationId = "kernel"
                                                Kind = "budget"
                                                Description = $"consume failed: {err}" } ]
                                    )
        | ExitDecision.Blocked inputs -> return! halt (MeditationStop.Blocked inputs)
        | ExitDecision.Inconsistent cons -> return! halt (MeditationStop.Inconsistent cons)
        | ExitDecision.Inconclusive unresolved -> return! halt (MeditationStop.Inconclusive unresolved)
        | ExitDecision.ContractSatisfied _
        | ExitDecision.OpenWorldReportReady _
        | ExitDecision.TargetRefuted _ ->
            // 成功出口返回账本，由 meditate 转 conclude。
            return ledger
    }

/// P1-2/P0-4/P1-6：验证出口决策与报告后走 conclude——Kernel 不信任注入的 prover/compiler，
/// 任一验证失败 → Inconclusive（invariant），不把虚假报告交给用户。
/// expectedProof：Kernel 验证过的停证（统一锚；TargetRefuted 由 toStopProof 产出）。
/// sequence：完成事件的真实序列（finalLedger.EventCount）。
let private concludeVerified
    (expectedCompletedDigest: string option)
    (sequence: int)
    (request: MeditationRequest)
    (ledger: MeditationLedger)
    (expectedProof: StopProof)
    (decision: ExitDecision)
    (canonical: CanonicalReport)
    : Meditation<MeditationReport> =
    meditation {
        match
            verifyExitDecision request ledger decision, verifyCanonicalReport request ledger expectedProof canonical
        with
        | Error reason, _
        | _, Error reason ->
            return!
                halt (
                    MeditationStop.Inconclusive
                        [ { ObligationId = "kernel"
                            Kind = "exit-proof"
                            Description = reason } ]
                )
        | Ok(), Ok() -> return! conclude sequence expectedCompletedDigest expectedProof decision canonical
    }

/// §36.1：唯一公开契约。
/// meditate 一次调用返回完整结果；调用者不参与内部访谈循环。
/// 恢复语义（§9.1/§38.3）：先 Replay 全部事件 → 版本校验 → fold 出账本；
/// 新 session 先 append MeditationRequested；budget 从 CreditsConsumed 恢复。
let meditate
    (execute: ObligationExecutor)
    (provers: ExitProver list)
    (compileCanonical: ReportCompiler)
    (intent: MeditationIntent)
    : Meditation<MeditationReport> =
    meditation {
        let! env = ask
        let! lines = ofTask (fun ct -> env.Journal.Replay ct)

        match replay env lines with
        | Error reason ->
            return!
                halt (
                    MeditationStop.Blocked
                        [ { What = "journal replay"
                            WhyNeeded = reason } ]
                )
        | Ok(replayedLedger, historicalDigest) ->
            // P0-3：同一 Journal 只能被同一 (请求, 预算) 复用——历史请求 digest 必须与当前一致。
            let currentDigest = requestDigest intent

            match historicalDigest with
            | Some d when d <> currentDigest ->
                return!
                    halt (
                        MeditationStop.Inconsistent
                            [ { SubjectId = "journal"
                                SupportDigest = currentDigest
                                OpposeDigest = d } ]
                    )
            | _ ->
                // 1. 新 session 首事件：MeditationRequested（重放恢复时已存在则跳过）。
                let! ledger =
                    if historicalDigest.IsSome then
                        meditation { return replayedLedger }
                    else
                        appendAndFold env replayedLedger (MeditationRequested currentDigest)

                // 2. budget 恢复：initial − 已消耗；超耗 = 账本与意图不一致（非法，fail closed）。
                let consumed = ledger.ResourceUsage.CreditsConsumed

                if consumed > intent.InitialCredit then
                    return!
                        halt (
                            MeditationStop.Inconsistent
                                [ { SubjectId = "budget"
                                    SupportDigest = $"InitialCredit={intent.InitialCredit}"
                                    OpposeDigest = $"CreditsConsumed={consumed}" } ]
                        )
                else
                    // P1-3：Budget.restore 恢复 Spent（历史消耗），不再从 0 起算。
                    let budget = Budget.restore intent.InitialCredit consumed
                    // 恢复路径：已完成（MC 是严格终态，其后无合法事件）→ 跳过 seek
                    // 直接尾部 tryProve——TargetRefuted 历史日志的义务非空不再误伤恢复（Blocked）。
                    let! finalLedger =
                        if ledger.CompletedReportDigest.IsSome then
                            meditation { return ledger }
                        else
                            seek execute provers intent.Request ledger budget
                    // P1-1：恢复路径已完成 → conclude 校验当前报告与历史完成 digest 一致（不重复 append）。
                    let expectedCompletedDigest = finalLedger.CompletedReportDigest
                    let completionSequence = finalLedger.EventCount

                    match tryProve provers intent.Request finalLedger with
                    | ExitDecision.ContractSatisfied proof ->
                        let expectedProof = ContractSatisfactionProof.stopProof proof
                        let canonical = compileCanonical intent finalLedger expectedProof

                        return!
                            concludeVerified
                                expectedCompletedDigest
                                completionSequence
                                intent.Request
                                finalLedger
                                expectedProof
                                (ExitDecision.ContractSatisfied proof)
                                canonical
                    | ExitDecision.OpenWorldReportReady proof ->
                        let canonical = compileCanonical intent finalLedger proof

                        return!
                            concludeVerified
                                expectedCompletedDigest
                                completionSequence
                                intent.Request
                                finalLedger
                                proof
                                (ExitDecision.OpenWorldReportReady proof)
                                canonical
                    | ExitDecision.TargetRefuted refutation ->
                        // 反驳出口：验证与停证转换在 RefutationProof.toStopProof 内完成
                        // （反方 warrant 必须真实存在于账本、属于目标 claim、极性 Opposes）。
                        match RefutationProof.toStopProof finalLedger refutation with
                        | Error reason ->
                            return!
                                halt (
                                    MeditationStop.Inconclusive
                                        [ { ObligationId = "kernel"
                                            Kind = "refutation"
                                            Description = reason } ]
                                )
                        | Ok expectedProof ->
                            let canonical = compileCanonical intent finalLedger expectedProof

                            return!
                                concludeVerified
                                    expectedCompletedDigest
                                    completionSequence
                                    intent.Request
                                    finalLedger
                                    expectedProof
                                    (ExitDecision.TargetRefuted refutation)
                                    canonical
                    | ExitDecision.Blocked inputs -> return! halt (MeditationStop.Blocked inputs)
                    | ExitDecision.Inconsistent cons -> return! halt (MeditationStop.Inconsistent cons)
                    | ExitDecision.Inconclusive unresolved -> return! halt (MeditationStop.Inconclusive unresolved)
                    | ExitDecision.Continue ->
                        // seek 返回时必有停证或短路；此分支不可达——若到达即实现 bug，显式失败而非兜底。
                        return!
                            halt (
                                MeditationStop.Inconclusive
                                    [ { ObligationId = "kernel"
                                        Kind = "invariant"
                                        Description = "seek returned without stop proof and without halt" } ]
                            )
    }
