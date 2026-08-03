// 第四阶段：属性测试（TASK.md 明确列出的优先项）。
// 1. 不同 scope 不混算  2. provenance 依赖簇传递闭包  3. deduction 不升级 grade
// 4. NoHit 不能关闭 unknown  5. duplicate EventId 拒绝  6. 并行完成顺序不影响结果
// 7. credit 严格下降  8. stop proof 可由事件独立重建（闭环内已验，此处验 codec 层往返）。
module Meditator.Tests.Properties

open System.Threading
open System.Threading.Tasks
open System.Reflection
open Meditator.Boundary
open Meditator.Ensure
open Meditator.Budget
open Meditator.Ledger
open Meditator.Meditation
open Meditator.Obligation
open Meditator.Oracle
open Meditator.Stop
open Meditator.Report
open Meditator.Kernel
open Meditator.Methods.Warrants
open Meditator.Tests.TestUtil
open Meditator.Tests.Scenario

let private scopeOf (pop: string) : Scope =
    { Content = None
      Time = None
      Modality = None
      Population = Some pop }

let private mkClaim (statement: string) (scope: Scope) : Claim =
    { Id = ClaimId.ofProposition statement scope
      Statement = statement
      Role = Assertion
      Source = ByOracleProposal
      Scope = scope
      IntroducedBy = "" }

let private mkWitness =
    VerifierWitness.issue Verifiers.observation VerifierKind.Observation "obs"

// 139 版：每 warrant 独立 witness（不同观察=不同 receipt）——依赖簇按 witness 分组后，
// 共享 witness 会把无关 warrant 并簇；需要"同 witness 同簇"语义的测试显式共享。
let private mkWitnessFor (suffix: string) =
    VerifierWitness.issue Verifiers.observation VerifierKind.Observation ("obs:" + suffix)

let private mkWarrantFor (claim: Claim) (scope: Scope) (polarity: Polarity) (sources: string list) : Warrant =
    let body =
        { Id =
            WarrantId(
                EventCodec.sha256Hex (
                    EventCodec.field "c" (claimIdText claim.Id)
                    + EventCodec.renderScope scope
                    + EventCodec.field "p" (if polarity = Supports then "S" else "O")
                )
            ) // 占位；ofData 派生
          ClaimId = claim.Id
          Polarity = polarity
          Kind = Observation
          Rule = "observation/v1"
          Strength = Moderate
          Scope = scope
          Origin = Provenance.create fixedClock "obs" "observation/v1"
          VerifierWitnesses = [ mkWitnessFor (String.concat "|" sources) ]
          DependencyWarrantIds = []
          UltimateSourceIds = sources |> List.map SourceId
          IntroducedBy = "" }

    { body with
        Id = EventCodec.warrantIdOfData body }
    |> Warrant.create
    |> function
        | Ok w -> w
        | Error e -> failwith e

/// journal Append 的同步调用（测试专用）——expectedRevision = 当前行数。
let private runAppend (journal: InMemoryJournal) (line: string) : AppendOutcome =
    (journal :> IMeditationJournal).Append journal.LineCount line CancellationToken.None
    |> fun t -> t.GetAwaiter().GetResult()

let run () =
    printfn "== 属性测试 =="
    let failuresAtStart = failures

    // ── 1. scope 隔离：warrant 必须与 claim scope 一致（fold 强制，其他 4）；
    //     不同 scope 的 claim 各自计算极性，不混算（§41.2）。
    let scopeA = scopeOf "A"
    let scopeB = scopeOf "B"
    let claimA = mkClaim "p" scopeA
    let claimB = mkClaim "p" scopeB // 同 statement 不同 scope = 不同命题（Leibniz）
    let wA = mkWarrantFor claimA scopeA Supports [ "s1" ]
    let wB = mkWarrantFor claimB scopeB Opposes [ "s2" ]

    let ledger =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed claimA
              ClaimFramed claimB
              ContributionAccepted wA
              ContributionAccepted wB ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    checkEq "scope A: SupportedOnly" SupportedOnly (polarityOf ledger claimA.Id scopeA)
    checkEq "scope B: RefutedOnly" RefutedOnly (polarityOf ledger claimB.Id scopeB)

    // ── 2. 依赖簇传递闭包：A~B（共享 s1）、B~C（共享 s2）→ 同一簇（§41.3）。
    let w1 = mkWarrantFor claimA scopeA Supports [ "s1" ]
    let w2 = mkWarrantFor claimA scopeA Supports [ "s1"; "s2" ]
    let w3 = mkWarrantFor claimA scopeA Supports [ "s2" ]
    let w4 = mkWarrantFor claimA scopeA Opposes [ "s3" ] // 独立来源

    let clusters = dependencyClusters [ w1; w2; w3; w4 ]
    checkEq "transitive cluster: 2 clusters" 2 clusters.Length
    check "w1/w2/w3 share one cluster" (clusters |> List.exists (fun c -> List.length c = 3))

    // 同来源三次改写 = 一个依赖簇（伪独立证据，§41.3）。
    let rewrites =
        [ mkWarrantFor claimA scopeA Supports [ "same-src" ]
          mkWarrantFor claimA scopeA Supports [ "same-src" ]
          mkWarrantFor claimA scopeA Supports [ "same-src" ] ]

    checkEq "three rewrites of one source = 1 cluster" 1 (dependencyClusters rewrites |> List.length)

    // ── 3. deduction 不升级 grade（G-deriv：结论逐维 ≤ 前提 meet，§41.4）。
    let premiseClaim = mkClaim "premise" scopeEarth

    let premiseWitness =
        VerifierWitness.issue Verifiers.observation VerifierKind.Observation "obs"

    let premiseBody =
        { Id = WarrantId "premise-w" // 占位；ofData 派生
          ClaimId = premiseClaim.Id
          Polarity = Supports
          Kind = Observation
          Rule = "observation/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "obs" "observation/v1"
          VerifierWitnesses = [ premiseWitness ]
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s-premise" ]
          IntroducedBy = "" }

    let premiseWarrant =
        { premiseBody with
            Id = EventCodec.warrantIdOfData premiseBody }
        |> Warrant.create
        |> function
            | Ok w -> w
            | Error e -> failwith e

    let acceptedPremise =
        Accepted.create Verifiers.observation [ premiseWitness ] premiseClaim
        |> function
            | Ok a -> a
            | Error e -> failwith e

    let conclusionClaim = mkClaim "conclusion" scopeEarth

    // P0-2（137 版）：P0 禁用 deduction——无命题 AST 时任何步骤检查都是伪验证，
    // 规则表为空，RuleEngine.verify 恒拒绝（诚实禁用而非伪验证）。
    match RuleEngine.verify "modus-ponens/v1" [ "premise implies conclusion"; "apply modus ponens" ] with
    | Error _ -> check "P0 deduction disabled (no registered rules)" true
    | Ok _ -> check "P0 deduction disabled (no registered rules)" false

    // A3 锚定（函数级）：推导 strength = 最弱前提 strength（grade 不升级的 strength 分量）。
    let weakPremiseBody =
        { Id = WarrantId "weak-premise-w" // 占位；ofData 派生
          ClaimId = premiseClaim.Id
          Polarity = Supports
          Kind = Observation
          Rule = "observation/v1"
          Strength = Weak
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "obs" "observation/v1"
          VerifierWitnesses = [ premiseWitness ]
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s-weak" ]
          IntroducedBy = "" }

    let weakPremiseWarrant =
        { weakPremiseBody with
            Id = EventCodec.warrantIdOfData weakPremiseBody }
        |> Warrant.create
        |> function
            | Ok w -> w
            | Error e -> failwith e

    checkEq
        "derivation strength = weakest premise (A3)"
        Weak
        (derivationStrength [ premiseWarrant; weakPremiseWarrant ])

    // ── 4. NoHit 不能关闭 unknown：SearchAttempted(NoHit) 不改变 UnknownRegions；
    //     UnknownRegionUpdated 拒绝 coverage 降级（等级序单调，§10.4）。
    let noHitLedger =
        fold
            MeditationLedger.Empty
            (SearchAttempted
                { ObligationId = "o"
                  Outcome = NoHit
                  Sequence = 0 })
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    checkEq "NoHit leaves UnknownRegions empty" 0 noHitLedger.UnknownRegions.Count
    checkEq "NoHit recorded in SearchAttempts" 1 noHitLedger.SearchAttempts.Length

    let unknown =
        { Id = UnknownId "u1"
          Description = "residual"
          Coverage = Open }

    let upgraded =
        fold
            noHitLedger
            (UnknownRegionUpdated
                { unknown with
                    Coverage = UnknownCoverage.VerifiedFinite "cert" })
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let downgradeAttempt =
        fold upgraded (UnknownRegionUpdated { unknown with Coverage = Open })

    match downgradeAttempt with
    | Error(InvalidTransition _) -> check "coverage downgrade rejected" true
    | _ -> check "coverage downgrade rejected" false

    // ── 5. duplicate EventId 拒绝（S2）：同字节 AlreadyCommitted，异字节 Conflict。
    let line =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 7 (CreditsConsumed 1)

    let same =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 7 (CreditsConsumed 1)

    let forged = replaceField line "D" "deadbeef" // 同 I 异 D
    checkEq "same bytes → AlreadyCommitted" AppendOutcome.AlreadyCommitted (runAppend (InMemoryJournal([ line ])) same)
    checkEq "forged bytes → Conflict" AppendOutcome.Conflict (runAppend (InMemoryJournal([ line ])) forged)

    // 同语义不同事件（不同 payload）→ 不同 EventId，允许共存。
    let other =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 7 (CreditsConsumed 2)

    checkEq "different payload → Committed" AppendOutcome.Committed (runAppend (InMemoryJournal([ line ])) other)

    // ── 6. 并行完成顺序不影响结果：mapBounded 结果按输入位置排列（ARCH-009）。
    let env = makeEnv (InMemoryJournal([]))

    let parallelM =
        mapBounded 3 [ 1..6 ] (fun i ->
            ofTask (fun ct ->
                task {
                    do! Task.Delay(max 5 (70 - i * 10), ct)
                    return i * 7
                }))

    match (parallelM env CancellationToken.None).GetAwaiter().GetResult() with
    | Ok values -> checkEq "mapBounded preserves input order" [ 7; 14; 21; 28; 35; 42 ] values
    | Error _ -> check "mapBounded preserves input order" false

    // ── 7. credit 严格下降（R1/R3）：负数/超额/非正消费均非法，不 clamp。
    let b0 = Budget.create 10

    let b1 =
        Budget.consume 1 b0
        |> function
            | Ok b -> b
            | Error e -> failwith e

    checkEq "consume 1: remaining 9" 9 b1.Remaining
    checkEq "consume 1: spent 1" 1 b1.Spent
    check "negative amount rejected" (Budget.consume -1 b0 |> Result.isError)
    check "over-consumption rejected" (Budget.consume 11 b0 |> Result.isError)
    check "zero consumption rejected" (Budget.consume 0 b0 |> Result.isError)
    check "negative allocation component rejected" (Budget.allocate [ 1; -2 ] b0 |> Result.isError)
    check "allocation exceeding parent-1 rejected" (Budget.allocate [ 5; 5 ] b0 |> Result.isError)

    let b2 =
        Budget.allocate [ 2; 3 ] b0
        |> function
            | Ok b -> b
            | Error e -> failwith e

    checkEq "allocate 2+3 from 10: remaining 5" 5 b2.Remaining
    check "potential strictly decreases after allocation" (Budget.potential [ 2; 3 ] < Budget.potential [ 10 ])

    // P1-3：Budget.restore 恢复 Spent（历史消耗不归零）；非法范围 fail closed。
    let restored = Budget.restore 10 3
    checkEq "restore: remaining 7" 7 restored.Remaining
    checkEq "restore: spent 3 (not zeroed)" 3 restored.Spent

    // P1-3：fold 拒绝非法 CreditsConsumed（负/零/溢出）——重放不可信日志不能绕过 Budget.consume。
    match fold MeditationLedger.Empty (CreditsConsumed -1) with
    | Error(InvalidTransition _) -> check "negative credit event rejected on fold" true
    | _ -> check "negative credit event rejected on fold" false

    match fold MeditationLedger.Empty (CreditsConsumed 0) with
    | Error(InvalidTransition _) -> check "zero credit event rejected on fold" true
    | _ -> check "zero credit event rejected on fold" false

    // 其他 1：MeditationCompleted 严格终态——第二条 MC 异 digest 拒绝；MC 后任何事件拒绝。
    let mcLedger0 =
        foldAll MeditationLedger.Empty [ MeditationRequested "r"; ClaimFramed targetClaim ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let mcLedger1 =
        fold mcLedger0 (MeditationCompleted "d1")
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    match fold mcLedger1 (MeditationCompleted "d2") with
    | Error(InvalidTransition _) -> check "second MeditationCompleted rejected" true
    | _ -> check "second MeditationCompleted rejected" false

    match fold mcLedger1 (ClaimFramed targetClaim) with
    | Error(InvalidTransition _) -> check "event after MeditationCompleted rejected" true
    | _ -> check "event after MeditationCompleted rejected" false

    match fold mcLedger0 (MeditationRequested "r2") with
    | Error(InvalidTransition _) -> check "second MeditationRequested rejected at fold" true
    | _ -> check "second MeditationRequested rejected at fold" false

    // ── 8. codec 往返：事件编码 → 解码 → 相等（确定性重放的三件套地基）。
    let sampleClaim = mkClaim "roundtrip" scopeEarth
    let sampleWarrant = mkWarrantFor sampleClaim scopeEarth Supports [ "s" ]

    let events: MeditationEvent list =
        [ MeditationRequested "abc"
          ClaimFramed sampleClaim
          OracleInvocationClaimed "key1"
          OracleInvocationAccepted("key1", "transcript")
          OracleAnswerRejected
              { InvocationKey = "key2"
                Reason = "schema" }
          ContributionAccepted sampleWarrant
          EvidenceObserved("ev1", "d1")
          HypothesisRecorded("h1", "d1")
          ConceptRecorded("n1", "d1")
          RelationRecorded("r1", "d1")
          CounterexampleRecorded("x1", "d1")
          UnknownRegionUpdated
              { Id = UnknownId "u1"
                Description = "desc"
                Coverage = Open }
          SearchAttempted
              { ObligationId = "o1"
                Outcome = NoHit
                Sequence = 0 }
          SearchAttempted
              { ObligationId = "o1"
                Outcome = Hit "d"
                Sequence = 1 }
          EpisodeRecorded
              { Id = EpisodeId "e1"
                MethodId = "m"
                ObligationId = "o"
                InputDigests = []
                CandidateDigests = []
                AcceptedDigests = []
                RejectedDigests = [] }
          AttemptRecorded
              { ObligationId = "o"
                LedgerDigest = "d"
                PolicyVersion = "p" }
          NoProgress("o", "k")
          CreditsConsumed 1
          SweepCompleted
          MeditationCompleted "digest"
          MeditationFailed "reason" ]

    let mutable codecOk = true

    events
    |> List.iteri (fun i e ->
        let line = EventCodec.encode EventSchemaVersion "p" "r" i e

        match EventCodec.decode line with
        | Error err ->
            codecOk <- false
            printfn "     decode failed at %d: %s" i err
        | Ok envelope ->
            if envelope.Payload <> e then
                codecOk <- false
                printfn "     roundtrip mismatch at %d" i)

    check "codec roundtrip for all event kinds" codecOk

    // P0-5：Unicode 往返——中文/emoji/组合字符/换行/U+001F/NUL 必须逐字节无损
    // （长度是 UTF-8 字节数，解析按字节切；UTF-16 Substring 会错位）。
    let unicodeStrings =
        [ "中文句子"
          "emoji 🧠 中"
          "组合字符 e\u0301"
          "换行\n与制表\t"
          "unit separator \u001F inside"
          "NUL \u0000 inside" ]

    let unicodeOk =
        unicodeStrings
        |> List.forall (fun s ->
            let scope =
                { Content = Some s
                  Time = Some s
                  Modality = Some s
                  Population = Some s }

            let claim =
                { mkClaim "unicode claim" scope with
                    Statement = s }

            let line = EventCodec.encode EventSchemaVersion "p" "r" 99 (ClaimFramed claim)

            match EventCodec.decode line with
            | Error err ->
                printfn "     unicode decode failed: %s" err
                false
            | Ok envelope ->
                match envelope.Payload with
                | ClaimFramed c ->
                    c.Statement = s
                    && c.Scope.Content = Some s
                    && c.Scope.Time = Some s
                    && c.Scope.Modality = Some s
                    && c.Scope.Population = Some s
                | _ -> false)

    check "unicode codec roundtrip" unicodeOk

    // P1-1：trailing bytes 拒绝——envelope 尾与事件负载尾都不允许残留。
    let trailingLine =
        EventCodec.encode EventSchemaVersion "p" "r" 0 (MeditationRequested "x")
        + "garbage"

    check "envelope trailing bytes rejected" (EventCodec.decode trailingLine |> Result.isError)

    let trailingPayload =
        EventCodec.encode EventSchemaVersion "p" "r" 0 (CreditsConsumed 1) + "x"

    check "payload trailing bytes rejected" (EventCodec.decode trailingPayload |> Result.isError)

    // 回归：畸形 option 编码（攻击者构造 I/D 自洽的恶意行）→ decode Error，
    // 不崩溃（noneOr fail closed）。
    let badScopePayload =
        "C"
        + EventCodec.field "id" "cid"
        + EventCodec.field "st" "statement"
        + EventCodec.field "ro" "ASR"
        + EventCodec.field "so" "OBS"
        + EventCodec.field "sc" "abc" // 畸形：非 \u0001 非 \u0002 开头
        + EventCodec.field "st" "\u0001"
        + EventCodec.field "sm" "\u0001"
        + EventCodec.field "sp" "\u0001"
        + EventCodec.field "b" ""

    let badId =
        EventCodec.sha256Hex (
            String.concat
                "\u001F"
                [ badScopePayload
                  string EventSchemaVersion
                  policyVersion
                  reducerVersion
                  "0" ]
        )

    let badDigest = EventCodec.sha256Hex badScopePayload

    let malformedLine =
        EventCodec.field "V" (string EventSchemaVersion)
        + EventCodec.field "P" policyVersion
        + EventCodec.field "R" reducerVersion
        + EventCodec.field "Q" "0"
        + EventCodec.field "I" badId
        + EventCodec.field "D" badDigest
        + EventCodec.field "E" badScopePayload

    match EventCodec.decode malformedLine with
    | Error e -> check "malformed option encoding fails closed (no crash)" (e.Contains "option")
    | Ok _ -> check "malformed option encoding fails closed (no crash)" false

    // P0-5：不完整转义（"\u0002\u0002" 不是任何合法编码的输出）→ fail closed。
    let badEscapePayload =
        "C"
        + EventCodec.field "id" "cid"
        + EventCodec.field "st" "statement"
        + EventCodec.field "ro" "ASR"
        + EventCodec.field "so" "OBS"
        + EventCodec.field "sc" "\u0002\u0002" // 畸形：前缀后无完整转义对
        + EventCodec.field "st" "\u0001"
        + EventCodec.field "sm" "\u0001"
        + EventCodec.field "sp" "\u0001"
        + EventCodec.field "b" ""

    let badEscapeId =
        EventCodec.sha256Hex (
            String.concat
                "\u001F"
                [ badEscapePayload
                  string EventSchemaVersion
                  policyVersion
                  reducerVersion
                  "0" ]
        )

    let malformedEscapeLine =
        EventCodec.field "V" (string EventSchemaVersion)
        + EventCodec.field "P" policyVersion
        + EventCodec.field "R" reducerVersion
        + EventCodec.field "Q" "0"
        + EventCodec.field "I" badEscapeId
        + EventCodec.field "D" (EventCodec.sha256Hex badEscapePayload)
        + EventCodec.field "E" badEscapePayload

    match EventCodec.decode malformedEscapeLine with
    | Error e -> check "incomplete escape fails closed" (e.Contains "option")
    | Ok _ -> check "incomplete escape fails closed" false

    // 旧 schema（v1）行在 replay 被版本校验干净拒绝（PERSIST-005：不猜测迁移）。
    let v1Line =
        EventCodec.encode 1 policyVersion reducerVersion 0 (MeditationRequested "x")

    let v1Env = makeEnv (InMemoryJournal([]))

    match replay v1Env [ v1Line ] with
    | Error e when e.Contains "schema version" -> check "old schema line rejected at replay (version check)" true
    | _ -> check "old schema line rejected at replay (version check)" false

    // 其他 3：嵌套 trailing bytes（outcome 内部字段带多余字节）→ round-trip 校验拒绝。
    let originalEvent =
        SearchAttempted
            { ObligationId = "obl"
              Outcome = Hit "dig"
              Sequence = 1 }

    let canonicalPayload = EventCodec.renderEvent originalEvent
    // 篡改 payload（outcome 内部加 EXTRA）——I/D 仍基于原事件：decode 解析出的事件与原事件相同
    // （嵌套 trailing 被旧 parser 忽略），但 round-trip 重编码 ≠ 篡改行 → non-canonical。
    let tamperedPayload =
        canonicalPayload.Replace(
            EventCodec.field "c" ("HI" + EventCodec.field "d" "dig"),
            EventCodec.field "c" ("HI" + EventCodec.field "d" "dig" + "EXTRA")
        )

    let nestedLine =
        EventCodec.field "V" (string EventSchemaVersion)
        + EventCodec.field "P" policyVersion
        + EventCodec.field "R" reducerVersion
        + EventCodec.field "Q" "0"
        + EventCodec.field
            "I"
            (eventIdText (EventCodec.eventId EventSchemaVersion policyVersion reducerVersion 0 originalEvent))
        + EventCodec.field "D" (EventCodec.payloadDigest originalEvent)
        + EventCodec.field "E" tamperedPayload

    match EventCodec.decode nestedLine with
    | Error e when e.Contains "non-canonical" -> check "nested codec trailing bytes rejected" true
    | Error e ->
        check "nested codec trailing bytes rejected" false
        printfn "     nested decode error: %s" e
    | Ok _ -> check "nested codec trailing bytes rejected" false

    // 其他 3：长度前导零（非规范数字）→ round-trip 拒绝。
    let zeroPaddedLine =
        EventCodec.field "V" "02"
        + (EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested "x"))
            .Substring(2)

    match EventCodec.decode zeroPaddedLine with
    | Error _ -> check "zero-padded length rejected (non-canonical)" true
    | Ok _ -> check "zero-padded length rejected (non-canonical)" false

    // 138 版：乐观并发控制——stale expected revision 拒绝（两个进程从同一 revision 追加）。
    let revJournal = InMemoryJournal([])

    let revLine0 =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested "x")

    let revOutcome0 =
        (revJournal :> IMeditationJournal).Append 0 revLine0 CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    checkEq "append at correct revision commits" AppendOutcome.Committed revOutcome0

    let revLine1 =
        EventCodec.encode EventSchemaVersion policyVersion reducerVersion 1 (CreditsConsumed 1)

    let revOutcome1 =
        (revJournal :> IMeditationJournal).Append 0 revLine1 CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    match revOutcome1 with
    | AppendOutcome.WrongExpectedRevision actual -> check "stale revision rejected (concurrent append)" (actual = 1)
    | _ -> check "stale revision rejected (concurrent append)" false

    // 139 版评审 #16：幂等优先——同事件已写入时以原 revision 重试 → AlreadyCommitted
    // （不是 WrongExpectedRevision；重试语义与幂等语义统一）。
    let retryOutcome1 =
        (revJournal :> IMeditationJournal).Append 0 revLine0 CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    checkEq "concurrent same-event retry is AlreadyCommitted" AppendOutcome.AlreadyCommitted retryOutcome1

    // P1-1：replay 拒绝 sequence 缺口与重排（Kernel.replay 校验 Q 连续性）。
    let gapLines =
        [ EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested "a")
          EventCodec.encode EventSchemaVersion policyVersion reducerVersion 2 (CreditsConsumed 1) ]

    let gapEnv = makeEnv (InMemoryJournal([]))

    match replay gapEnv gapLines with
    | Error e when e.Contains "sequence" -> check "replay rejects sequence gap" true
    | _ -> check "replay rejects sequence gap" false

    let reorderedLines =
        [ EventCodec.encode EventSchemaVersion policyVersion reducerVersion 1 (CreditsConsumed 1)
          EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested "a") ]

    match replay gapEnv reorderedLines with
    | Error e when e.Contains "sequence" -> check "replay rejects reordered lines" true
    | _ -> check "replay rejects reordered lines" false

    // review 回归：replay 首行必须是 MeditationRequested；多条 MR digest 必须一致。
    let notMrFirst =
        [ EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (CreditsConsumed 1) ]

    match replay gapEnv notMrFirst with
    | Error e when e.Contains "first line" -> check "replay rejects non-MR first line" true
    | _ -> check "replay rejects non-MR first line" false

    let twoMr =
        [ EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested "a")
          EventCodec.encode EventSchemaVersion policyVersion reducerVersion 1 (MeditationRequested "b") ]

    match replay gapEnv twoMr with
    | Error _ -> check "second MeditationRequested rejected (fold terminal-state)" true
    | Ok _ -> check "second MeditationRequested rejected (fold terminal-state)" false

    // P0-4：epistemic digest 与 operational 分离——bookkeeping 不算认识进展。
    let baseLedger = MeditationLedger.Empty
    let epistemic0 = epistemicDigest baseLedger

    let opLedger =
        foldAll
            baseLedger
            [ CreditsConsumed 5
              AttemptRecorded
                  { ObligationId = "o"
                    LedgerDigest = "d"
                    PolicyVersion = "p" }
              SweepCompleted
              NoProgress("o", "k") ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    checkEq "operational events do not change epistemic digest" epistemic0 (epistemicDigest opLedger)

    let p4Claim = mkClaim "p4-claim" scopeEarth

    let epLedger =
        fold baseLedger (ClaimFramed p4Claim)
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    check "epistemic event changes epistemic digest" (epistemicDigest epLedger <> epistemic0)

    // 连续无进展语义：NoProgress → 1；epistemic 进展重置 0；再 NoProgress 只到 1（不累计）。
    let s1 =
        fold baseLedger (NoProgress("o1", "k1"))
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    checkEq "stall = 1 after NoProgress" 1 s1.ResourceUsage.Stalls

    let s2 =
        fold s1 (ClaimFramed p4Claim)
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    checkEq "stall reset on epistemic progress" 0 s2.ResourceUsage.Stalls

    let s3 =
        fold s2 (NoProgress("o1", "k2"))
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    checkEq "stall = 1 after progress (consecutive, not cumulative)" 1 s3.ResourceUsage.Stalls

    // P1-9：空证据 grade 基准锚定（无证据 ≠ 高可靠性；基准值防实现漂移）。
    let emptyGrade = ledgerDerivedGrade MeditationLedger.Empty

    checkEq
        "empty ledger grade is baseline (not high evidence)"
        { Directness = Direct
          Reliability = Confirmed
          Independence = Clusters 1
          Coverage = OpenWorldCoverage
          Reproducibility = NotYetReplayed }
        emptyGrade

    // attempt key 稳定性：operational 事件不改变 key（去重不被绕过）。
    let k1 = attemptKey "o1" baseLedger "p"
    let k2 = attemptKey "o1" opLedger "p"
    checkEq "attempt key stable across operational events" k1.LedgerDigest k2.LedgerDigest

    // 其他 4：claim identity 在 fold 重验——Id 与 (statement, scope) 不符的 ClaimFramed 被拒。
    let mismatchedClaim =
        { mkClaim "statement-A" scopeEarth with
            Id = ClaimId.ofProposition "statement-B" scopeEarth } // Id 与 statement 不符

    match fold MeditationLedger.Empty (ClaimFramed mismatchedClaim) with
    | Error(InvalidTransition _) -> check "claim id mismatch rejected at fold" true
    | _ -> check "claim id mismatch rejected at fold" false

    // 其他 4：warrant scope 与 claim scope 不一致 → fold 拒绝。
    let identityClaim = mkClaim "identity-target" scopeEarth

    let identityLedger =
        foldAll MeditationLedger.Empty [ ClaimFramed identityClaim ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let crossScopeWarrant =
        mkWarrantFor identityClaim (scopeOf "mars") Opposes [ "s-mars" ]

    match fold identityLedger (ContributionAccepted crossScopeWarrant) with
    | Error(InvalidTransition _) -> check "cross-scope warrant rejected at fold" true
    | _ -> check "cross-scope warrant rejected at fold" false

    // 其他 4：跨 scope 反证不得用于反驳当前 scope——toStopProof 拒绝。
    let marsClaim = mkClaim "mars-target" (scopeOf "mars")
    let marsWarrant = mkWarrantFor marsClaim (scopeOf "mars") Opposes [ "s-mars" ]

    let marsLedger =
        foldAll MeditationLedger.Empty [ ClaimFramed marsClaim; ContributionAccepted marsWarrant ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let crossScopeProof =
        RefutationProof.create
            marsClaim.Id
            [ Warrant.id marsWarrant ]
            scopeEarth
            RefutationRule.LogicalCounterexample
            ClaimMorphology.Universal
        |> function
            | Ok p -> p
            | Error e -> failwith e

    match RefutationProof.toStopProof marsLedger crossScopeProof with
    | Error e when e.Contains "scope" -> check "cross-scope refutation rejected" true
    | _ -> check "cross-scope refutation rejected" false

    // P0-3：义务未解除时 ContractSatisfied 被拒（停证不得证明弱于当前义务集的契约）。
    let emptyProof = buildProof claimTestIntent.Request MeditationLedger.Empty

    let undischargedDecision =
        ExitDecision.ContractSatisfied(
            ContractSatisfactionProof.create (contractDigest claimTestIntent.Request) emptyProof
        )

    match verifyExitDecision claimTestIntent.Request MeditationLedger.Empty undischargedDecision with
    | Error e when e.Contains "obligations" -> check "stop proof must discharge actual obligations" true
    | _ -> check "stop proof must discharge actual obligations" false

    // P0-3：grade 必须等于 ledger 机械派生值——装配随意填的 grade 被拒。
    let p3Ledger =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed targetClaim
              ContributionAccepted supportingWarrant
              ContributionAccepted opposingWarrant ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let goodProof = buildProof claimTestIntent.Request p3Ledger

    let wrongGradeProof =
        match
            StopProof.create
                (StopProof.discharged goodProof)
                (StopProof.unknowns goodProof)
                (StopProof.coverage goodProof)
                { StopProof.grade goodProof with
                    Reliability = Tentative }
                (StopProof.prohibited goodProof)
                (StopProof.eventDigests goodProof)
        with
        | Ok p -> p
        | Error e -> failwith e

    let wrongGradeDecision =
        ExitDecision.ContractSatisfied(
            ContractSatisfactionProof.create (contractDigest claimTestIntent.Request) wrongGradeProof
        )

    match verifyExitDecision claimTestIntent.Request p3Ledger wrongGradeDecision with
    | Error e when e.Contains "grade" -> check "stop grade must equal ledger-derived grade" true
    | _ -> check "stop grade must equal ledger-derived grade" false

    // P0-3：unknowns 不完整（缺失 ledger 中的未知区域）→ 拒绝。
    let unknownRegionLedger =
        fold
            p3Ledger
            (UnknownRegionUpdated
                { Id = UnknownId "u-extra"
                  Description = "d"
                  Coverage = Open })
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let incompleteProof = buildProof claimTestIntent.Request unknownRegionLedger // unknowns=[] 但 ledger 有 u-extra

    let incompleteDecision =
        ExitDecision.ContractSatisfied(
            ContractSatisfactionProof.create (contractDigest claimTestIntent.Request) incompleteProof
        )

    match verifyExitDecision claimTestIntent.Request unknownRegionLedger incompleteDecision with
    | Error e when e.Contains "unknowns" -> check "stop proof unknowns must be complete" true
    | _ -> check "stop proof unknowns must be complete" false

    // P0-4（138 版）：任意 assertion 不能满足他请求——账本只有 sky claim，目标声明为 moon → 拒绝。
    let moonTargetRequest =
        { claimTestIntent.Request with
            Contract =
                Some
                    { claimTestIntent.Request.Contract.Value with
                        TargetStatement = Some "The moon is cheese" } }

    let moonTargetDecision =
        ExitDecision.ContractSatisfied(ContractSatisfactionProof.create (contractDigest moonTargetRequest) goodProof)

    match verifyExitDecision moonTargetRequest p3Ledger moonTargetDecision with
    | Error e when e.Contains "target" -> check "arbitrary assertion cannot satisfy another request" true
    | _ -> check "arbitrary assertion cannot satisfy another request" false

    // P0-5（138 版）：义务按 subject 派生——多 claim 各条携带 subject；ID 与 discharge 一致。
    let multiLedger =
        foldAll MeditationLedger.Empty [ ClaimFramed claimA; ClaimFramed claimB ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let multiObligations = deriveObligations claimTestIntent.Request multiLedger

    check
        "per-claim obligations carry subject ids"
        (multiObligations
         |> List.exists (fun o ->
             o.Id = $"GenerateOpposition:{claimIdText claimA.Id}"
             && o.SubjectIds = [ claimIdText claimA.Id ])
         && multiObligations
            |> List.exists (fun o ->
                o.Id = $"GroundEvidence:{claimIdText claimB.Id}"
                && o.SubjectIds = [ claimIdText claimB.Id ]))

    // 义务 ID 与 discharge ID 同源（证明链闭合）：格式一致——逐义务、含 claim-id subject。
    let dischargeIds =
        match deriveDischargeProof claimTestIntent.Request p3Ledger with
        | Ok proofs -> proofs |> List.map (fun p -> p.ObligationId) |> Set.ofList
        | Error _ -> Set.empty

    let obligationIds = multiObligations |> List.map (fun o -> o.Id) |> Set.ofList

    check
        "obligation and discharge ids share subject format"
        (obligationIds
         |> Set.forall (fun id ->
             id = "FrameClaim:"
             || id.EndsWith($":{claimIdText claimA.Id}")
             || id.EndsWith($":{claimIdText claimB.Id}"))
         && dischargeIds
            |> Set.forall (fun id ->
                id.StartsWith "FrameClaim:"
                || id.StartsWith "GenerateOpposition:"
                || id.StartsWith "GroundEvidence:"))

    // 139 版评审 #11：带 TargetStatement 的 ClaimTest——初始 FrameClaim 义务 ID 与
    // discharge 精确相等（不再是"格式相似"）。
    match deriveObligations claimTestIntent.Request MeditationLedger.Empty with
    | [] -> check "initial frame obligation id equals discharge id (exact)" false
    | first :: _ ->
        let targetIdText = claimIdText (ClaimId.ofProposition "The sky is blue" scopeEarth)

        check
            "initial frame obligation id equals discharge id (exact)"
            (first.Id = $"FrameClaim:{targetIdText}" && first.SubjectIds = [ targetIdText ])

        match deriveDischargeProof claimTestIntent.Request p3Ledger with
        | Ok proofs ->
            check
                "discharge contains the exact frame obligation id"
                (proofs |> List.exists (fun p -> p.ObligationId = $"FrameClaim:{targetIdText}"))
        | Error _ -> check "discharge contains the exact frame obligation id" false

    // 139 版 review blocking 回归：无 TargetStatement 的请求不产生结构上不可满足的
    // FrameClaim 义务（subject 空串无法被任何 ClaimFramed 匹配——fold 强制 claim id 非空）。
    let noTargetRequest =
        { claimTestIntent.Request with
            Contract =
                Some
                    { claimTestIntent.Request.Contract.Value with
                        TargetStatement = None } }

    let noTargetObligations = deriveObligations noTargetRequest MeditationLedger.Empty

    check
        "no-target request has no unsatisfiable FrameClaim obligation"
        (not (noTargetObligations |> List.exists (fun o -> o.Kind = FrameClaim)))

    // 139 版评审 #15：MethodHints 不进请求身份——hint 改变不使旧 journal 失效（身份稳定）。
    let reqWithHints =
        { claimTestIntent.Request with
            MethodHints = [ "GroundEvidence" ] }

    checkEq
        "method hints do not enter request identity"
        (canonicalRequest claimTestIntent.Request)
        (canonicalRequest reqWithHints)

    // P0-5：Empirical 契约的 ContractSatisfied → UnsupportedContractMode（Kernel 机械判定，不依赖场景 prover）。
    let empiricalContractRequest =
        { claimTestIntent.Request with
            Contract =
                Some
                    { claimTestIntent.Request.Contract.Value with
                        RequestedEvidenceMode = Empirical } }

    let empiricalDecision =
        ExitDecision.ContractSatisfied(
            ContractSatisfactionProof.create (contractDigest empiricalContractRequest) goodProof
        )

    match verifyExitDecision empiricalContractRequest p3Ledger empiricalDecision with
    | Error e when e.Contains "unsupported" -> check "empirical contract rejects qualitative-only ledger" true
    | _ -> check "empirical contract rejects qualitative-only ledger" false

    // P0-5：跨 scope claim 满足合同 → 拒绝（义务空的 mars ledger）。
    let marsClaim2 = mkClaim "mars-only" (scopeOf "mars")
    let marsSupport2 = mkWarrantFor marsClaim2 (scopeOf "mars") Supports [ "s1" ]
    let marsOppose2 = mkWarrantFor marsClaim2 (scopeOf "mars") Opposes [ "s2" ]

    let marsLedger2 =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed marsClaim2
              ContributionAccepted marsSupport2
              ContributionAccepted marsOppose2 ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let marsProof = buildProof claimTestIntent.Request marsLedger2

    let marsDecision =
        ExitDecision.ContractSatisfied(
            ContractSatisfactionProof.create (contractDigest claimTestIntent.Request) marsProof
        )

    match verifyExitDecision claimTestIntent.Request marsLedger2 marsDecision with
    | Error e when e.Contains "scope" -> check "claims outside contract scope rejected" true
    | _ -> check "claims outside contract scope rejected" false

    // P0-6：空 discharged（装配自由构造）→ 拒绝。
    let emptyDischargeProof =
        match
            StopProof.create
                []
                (StopProof.unknowns goodProof)
                (StopProof.coverage goodProof)
                (StopProof.grade goodProof)
                (StopProof.prohibited goodProof)
                (StopProof.eventDigests goodProof)
        with
        | Ok p -> p
        | Error e -> failwith e

    let emptyDischargeDecision =
        ExitDecision.ContractSatisfied(
            ContractSatisfactionProof.create (contractDigest claimTestIntent.Request) emptyDischargeProof
        )

    match verifyExitDecision claimTestIntent.Request p3Ledger emptyDischargeDecision with
    | Error e when e.Contains "discharged" -> check "discharged must match derived obligations" true
    | _ -> check "discharged must match derived obligations" false

    // P0-5（137 版）：逐义务 discharge——ID 含 subject，不再是总括证明。
    match deriveDischargeProof claimTestIntent.Request p3Ledger with
    | Ok proofs ->
        let ids = proofs |> List.map (fun p -> p.ObligationId) |> Set.ofList

        check
            "per-obligation discharge ids (FrameClaim/GenerateOpposition/GroundEvidence)"
            (ids.Contains $"FrameClaim:{claimIdText targetClaim.Id}"
             && ids.Contains $"GenerateOpposition:{claimIdText targetClaim.Id}"
             && ids.Contains $"GroundEvidence:{claimIdText targetClaim.Id}")
    | Error _ -> check "per-obligation discharge ids (FrameClaim/GenerateOpposition/GroundEvidence)" false

    // P0-7：ClosedWorld certificate 未 ledger 派生 → 拒绝；coverage 与 grade 不一致 → 拒绝。
    let fakeClosedProof =
        match
            StopProof.create
                (StopProof.discharged goodProof)
                (StopProof.unknowns goodProof)
                (ClosedWorld(VerifiedFinite { Source = "trust me"; Digest = "fake" }))
                (StopProof.grade goodProof)
                (StopProof.prohibited goodProof)
                (StopProof.eventDigests goodProof)
        with
        | Ok p -> p
        | Error e -> failwith e

    let fakeClosedDecision =
        ExitDecision.ContractSatisfied(
            ContractSatisfactionProof.create (contractDigest claimTestIntent.Request) fakeClosedProof
        )

    match verifyExitDecision claimTestIntent.Request p3Ledger fakeClosedDecision with
    | Error e when e.Contains "coverage" -> check "closed-world certificate must be ledger-derived" true
    | _ -> check "closed-world certificate must be ledger-derived" false

    // review 回归：finding 的 claim 必须被其引用 warrant 支持/反对（claim-warrant 绑定）。
    // 138 版：报告验证测试改用 scopeEarth 账本——scope 隔离账本的 claim 是 scopeA/B，
    // 会被新的 out-of-scope finding 检查拒绝（报告声明 earth 却引用 scopeA claim）。
    let reportClaim = mkClaim "report-target" scopeEarth
    let reportSupportW = mkWarrantFor reportClaim scopeEarth Supports [ "s1" ]
    let reportOtherClaim = mkClaim "report-other" scopeEarth
    let reportOtherW = mkWarrantFor reportOtherClaim scopeEarth Supports [ "s2" ]
    let reportOutOfScopeClaim = mkClaim "report-out-of-scope" scopeA
    let reportOutOfScopeW = mkWarrantFor reportOutOfScopeClaim scopeA Supports [ "s3" ]

    let reportLedger =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed reportClaim
              ContributionAccepted reportSupportW
              ClaimFramed reportOtherClaim
              ContributionAccepted reportOtherW
              ClaimFramed reportOutOfScopeClaim
              ContributionAccepted reportOutOfScopeW ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let reportProof = buildProof claimTestIntent.Request reportLedger

    let reportFinding =
        { Text = reportClaim.Statement
          ClaimIds = [ reportClaim.Id ]
          WarrantIds = [ Warrant.id reportSupportW ]
          EvidenceIds = []
          Polarity = Supports
          Grade = gradeOfWarrant reportSupportW
          Qualification = { ScopeNote = None; Caveats = [] } }

    let unbackedFinding =
        { Text = "unbacked"
          ClaimIds = [ reportClaim.Id ] // reportClaim 由 reportSupportW 支持，但 finding 引用 reportOtherW（属于 reportOtherClaim）
          WarrantIds = [ Warrant.id reportOtherW ]
          EvidenceIds = []
          Polarity = Supports // 与 reportOtherW 极性一致，确保 polarity 检查先行通过
          Grade = gradeOfWarrant reportOtherW
          Qualification = { ScopeNote = None; Caveats = [] } }

    let unbackedCanonicalProof = proofForFindings reportLedger [ unbackedFinding ]

    let unbackedCanonical =
        CanonicalReport.compile
            "intent"
            scopeEarth
            [ unbackedFinding ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade unbackedCanonicalProof))
            unbackedCanonicalProof

    match verifyCanonicalReport claimTestIntent.Request reportLedger unbackedCanonicalProof unbackedCanonical with
    | Error e when e.Contains "not backed" -> check "finding claim must be backed by referenced warrant" true
    | _ -> check "finding claim must be backed by referenced warrant" false

    // P1-2：finding Text 必须等于引用 claim 的 statement。
    let textMismatchFinding =
        { Text = "完全不同的话"
          ClaimIds = [ reportClaim.Id ]
          WarrantIds = [ Warrant.id reportSupportW ]
          EvidenceIds = []
          Polarity = Supports
          Grade = gradeOfWarrant reportSupportW
          Qualification = { ScopeNote = None; Caveats = [] } }

    let textMismatchCanonicalProof =
        proofForFindings reportLedger [ textMismatchFinding ]

    let textMismatchCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ textMismatchFinding ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade textMismatchCanonicalProof))
            textMismatchCanonicalProof

    match
        verifyCanonicalReport claimTestIntent.Request reportLedger textMismatchCanonicalProof textMismatchCanonical
    with
    | Error e when e.Contains "text" -> check "finding text cannot exceed referenced claim" true
    | _ -> check "finding text cannot exceed referenced claim" false

    // P1-3：unknowns 必须等于 reportLedger 未知区域描述；recommendation index 必须在范围。
    let reportFinding =
        { Text = reportClaim.Statement
          ClaimIds = [ reportClaim.Id ]
          WarrantIds = [ Warrant.id reportSupportW ]
          EvidenceIds = []
          Polarity = Supports
          Grade = gradeOfWarrant reportSupportW
          Qualification = { ScopeNote = None; Caveats = [] } }

    let wrongUnknownsCanonicalProof = proofForFindings reportLedger [ reportFinding ]

    let wrongUnknownsCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ reportFinding ]
            []
            []
            [ "ghost-unknown" ]
            []
            (ReportGrade.Graded(StopProof.grade wrongUnknownsCanonicalProof))
            wrongUnknownsCanonicalProof

    match
        verifyCanonicalReport claimTestIntent.Request reportLedger wrongUnknownsCanonicalProof wrongUnknownsCanonical
    with
    | Error e when e.Contains "unknowns" -> check "report unknowns must equal reportLedger unknowns" true
    | _ -> check "report unknowns must equal reportLedger unknowns" false

    let badRecCanonicalProof = proofForFindings reportLedger [ reportFinding ]

    let badRecCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ reportFinding ]
            []
            []
            []
            [ { Text = "rec"
                BasedOnFindingIndexes = [ 5 ] } ]
            (ReportGrade.Graded(StopProof.grade badRecCanonicalProof))
            badRecCanonicalProof

    match verifyCanonicalReport claimTestIntent.Request reportLedger badRecCanonicalProof badRecCanonical with
    | Error e when e.Contains "index" -> check "recommendation indexes must be in range" true
    | _ -> check "recommendation indexes must be in range" false

    // P1-1：UnacceptableClaims 不得出现在报告；RequiredSections 必须存在。
    let strictRequest =
        { claimTestIntent.Request with
            Contract =
                Some
                    { claimTestIntent.Request.Contract.Value with
                        UnacceptableClaims = [ "forbidden-claim" ]
                        RequiredSections = [ ReportSection.Recommendations ] } }

    let forbiddenCanonicalProof =
        proofForFindings
            reportLedger
            [ { reportFinding with
                  Text = "this is a forbidden-claim statement" } ]

    let forbiddenCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ { reportFinding with
                  Text = "this is a forbidden-claim statement" } ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade forbiddenCanonicalProof))
            forbiddenCanonicalProof

    match verifyCanonicalReport strictRequest reportLedger forbiddenCanonicalProof forbiddenCanonical with
    | Error e when e.Contains "unacceptable" -> check "unacceptable claim must not appear in report" true
    | _ -> check "unacceptable claim must not appear in report" false

    let missingSectionCanonicalProof = proofForFindings reportLedger [ reportFinding ]

    let missingSectionCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ reportFinding ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade missingSectionCanonicalProof))
            missingSectionCanonicalProof

    match verifyCanonicalReport strictRequest reportLedger missingSectionCanonicalProof missingSectionCanonical with
    | Error e when e.Contains "sections" -> check "required sections must exist in report" true
    | _ -> check "required sections must exist in report" false

    // security_review：禁止主张藏在 Qualification（ScopeNote/Caveats——自由文本无 reportLedger 绑定）→ 拒绝。
    let hiddenInQualificationCanonicalProof =
        proofForFindings
            reportLedger
            [ { reportFinding with
                  Qualification =
                      { ScopeNote = Some "forbidden-claim"
                        Caveats = [ "also forbidden-claim here" ] } } ]

    let hiddenInQualificationCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ { reportFinding with
                  Qualification =
                      { ScopeNote = Some "forbidden-claim"
                        Caveats = [ "also forbidden-claim here" ] } } ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade hiddenInQualificationCanonicalProof))
            hiddenInQualificationCanonicalProof

    match
        verifyCanonicalReport
            strictRequest
            reportLedger
            hiddenInQualificationCanonicalProof
            hiddenInQualificationCanonical
    with
    | Error e when e.Contains "unacceptable" -> check "unacceptable claim hidden in qualification rejected" true
    | _ -> check "unacceptable claim hidden in qualification rejected" false

    // P0-7：报告级 grade——有证据 reportLedger 期望 Graded，NoEvidence 报告被拒（空证据不得伪报）。
    let noEvidenceCanonicalProof = proofForFindings reportLedger [ reportFinding ]

    let noEvidenceCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ reportFinding ]
            []
            []
            []
            []
            ReportGrade.NoEvidence
            noEvidenceCanonicalProof

    match verifyCanonicalReport claimTestIntent.Request reportLedger noEvidenceCanonicalProof noEvidenceCanonical with
    | Error e when e.Contains "grade" ->
        check "report grade must be reportLedger-derived (NoEvidence rejected for evidence reportLedger)" true
    | _ -> check "report grade must be reportLedger-derived (NoEvidence rejected for evidence reportLedger)" false

    // P1-5：ScopeNote None 与 Some "\u0001" 的 digest 必须不同（option 转义复用，不重新发明哨兵）。
    let mkDigestReport (f: ReportFinding) : MeditationReport =
        { Title = "t"
          ExecutiveSummary = "s"
          Scope = scopeEarth
          Findings = [ f ]
          Counterpoints = []
          Dependencies = []
          EvidenceLimitations = []
          Unknowns = []
          Recommendations = []
          EpistemicGrade =
            ReportGrade.Graded
                { Directness = Direct
                  Reliability = Confirmed
                  Independence = Clusters 1
                  Coverage = OpenWorldCoverage
                  Reproducibility = NotYetReplayed }
          StopReason = ReportStopReason.ContractSatisfied
          Provenance = [] }

    let scopeNoteNone =
        { reportFinding with
            Qualification = { ScopeNote = None; Caveats = [] } }

    let scopeNoteSome1 =
        { reportFinding with
            Qualification =
                { ScopeNote = Some "\u0001"
                  Caveats = [] } }

    check
        "ScopeNote None differs from Some U+0001"
        (ReportCodec.digest (mkDigestReport scopeNoteNone)
         <> ReportCodec.digest (mkDigestReport scopeNoteSome1))

    // P0-3（137 版）：TargetRefuted 专用出口——非 ClaimTest 契约/Contested 目标/scope 不匹配全部拒绝。
    let refuteTarget = mkClaim "The sky is blue" scopeEarth
    let refuteW = mkWarrantFor refuteTarget scopeEarth Opposes [ "s-ref" ]

    let refuteLedger =
        foldAll MeditationLedger.Empty [ ClaimFramed refuteTarget; ContributionAccepted refuteW ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let refuteProof =
        RefutationProof.create
            refuteTarget.Id
            [ Warrant.id refuteW ]
            scopeEarth
            RefutationRule.LogicalCounterexample
            ClaimMorphology.Universal
        |> function
            | Ok p -> p
            | Error e -> failwith e

    let decisionRequest =
        { claimTestIntent.Request with
            Contract =
                Some
                    { claimTestIntent.Request.Contract.Value with
                        Goal = Decision } }

    match verifyExitDecision decisionRequest refuteLedger (ExitDecision.TargetRefuted refuteProof) with
    | Error _ -> check "TargetRefuted rejected for non-ClaimTest contract" true
    | Ok _ -> check "TargetRefuted rejected for non-ClaimTest contract" false

    let supportW = mkWarrantFor refuteTarget scopeEarth Supports [ "s-sup" ]

    let contestedRefuteLedger =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed refuteTarget
              ContributionAccepted refuteW
              ContributionAccepted supportW ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    match verifyExitDecision claimTestIntent.Request contestedRefuteLedger (ExitDecision.TargetRefuted refuteProof) with
    | Error e when e.Contains "RefutedOnly" -> check "TargetRefuted rejected for contested target" true
    | _ -> check "TargetRefuted rejected for contested target" false

    let marsRefuteProof =
        RefutationProof.create
            refuteTarget.Id
            [ Warrant.id refuteW ]
            (scopeOf "mars")
            RefutationRule.LogicalCounterexample
            ClaimMorphology.Universal
        |> function
            | Ok p -> p
            | Error e -> failwith e

    match verifyExitDecision claimTestIntent.Request refuteLedger (ExitDecision.TargetRefuted marsRefuteProof) with
    | Error _ -> check "TargetRefuted rejected for mismatched scope" true
    | Ok _ -> check "TargetRefuted rejected for mismatched scope" false

    // 139 版评审 #13：重复/额外 warrant 不扩大最小停证——每义务一条确定性最小 proof。
    let dupClaim = mkClaim "dup-target-139" scopeEarth
    let dupW1 = mkWarrantFor dupClaim scopeEarth Supports [ "s1" ]
    let dupW2 = mkWarrantFor dupClaim scopeEarth Supports [ "s2" ]
    let dupOpp = mkWarrantFor dupClaim scopeEarth Opposes [ "s3" ]

    let dupLedger =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed dupClaim
              ContributionAccepted dupW1
              ContributionAccepted dupW2
              ContributionAccepted dupOpp ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    match deriveDischargeProof claimTestIntent.Request dupLedger with
    | Ok proofs ->
        check
            "duplicate warrants do not duplicate obligation proof"
            (proofs
             |> List.filter (fun p -> p.ObligationId.StartsWith "GroundEvidence:")
             |> List.length = 1
             && proofs
                |> List.filter (fun p -> p.ObligationId.StartsWith "GenerateOpposition:")
                |> List.length = 1)
    | Error _ -> check "duplicate warrants do not duplicate obligation proof" false

    // 139 版评审 #13：最小停证每义务恰一个 witness digest（确定性最小证明）。
    match deriveDischargeProof claimTestIntent.Request dupLedger with
    | Ok proofs ->
        check
            "minimal stop proof has one witness per obligation"
            (proofs |> List.forall (fun p -> List.length p.DischargeEventDigests = 1))
    | Error _ -> check "minimal stop proof has one witness per obligation" false

    // 140 版评审 §5：单个反例不得反驳统计性命题——StatisticalRefutation 未注册 →
    // create 拒绝；TargetRefuted 不能仅由极性位产生（rule 是强制字段）。
    match
        RefutationProof.create
            refuteTarget.Id
            [ Warrant.id refuteW ]
            scopeEarth
            RefutationRule.StatisticalRefutation
            ClaimMorphology.Statistical
    with
    | Error e when e.Contains "not registered" ->
        check "single counterexample cannot refute a statistical claim (rule registry)" true
    | _ -> check "single counterexample cannot refute a statistical claim (rule registry)" false

    match
        RefutationProof.create
            refuteTarget.Id
            [ Warrant.id refuteW ]
            scopeEarth
            RefutationRule.LogicalCounterexample
            ClaimMorphology.Universal
    with
    | Ok _ -> check "logical counterexample rule is registered (P0)" true
    | Error _ -> check "logical counterexample rule is registered (P0)" false

    // 140 版 review should-fix：形态-规则机械匹配——统计形态 + 逻辑反例规则 → create 拒绝
    //（"全称/存在形态"是强制参数，不是文档承诺）。
    match
        RefutationProof.create
            refuteTarget.Id
            [ Warrant.id refuteW ]
            scopeEarth
            RefutationRule.LogicalCounterexample
            ClaimMorphology.Statistical
    with
    | Error e when e.Contains "does not apply" ->
        check "rule-morphology mismatch rejected (statistical claim needs statistical refutation)" true
    | _ -> check "rule-morphology mismatch rejected (statistical claim needs statistical refutation)" false

    // 138 版 review：TargetRefuted 目标必须匹配请求 TargetStatement——反驳其他 Assertion 不能通过出口。
    let mismatchedTarget = mkClaim "some-other-claim-not-requested" scopeEarth
    let mismatchedW = mkWarrantFor mismatchedTarget scopeEarth Opposes [ "s-other" ]

    let mismatchedLedger =
        foldAll MeditationLedger.Empty [ ClaimFramed mismatchedTarget; ContributionAccepted mismatchedW ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let mismatchedProof =
        RefutationProof.create
            mismatchedTarget.Id
            [ Warrant.id mismatchedW ]
            scopeEarth
            RefutationRule.LogicalCounterexample
            ClaimMorphology.Universal
        |> function
            | Ok p -> p
            | Error e -> failwith e

    match verifyExitDecision claimTestIntent.Request mismatchedLedger (ExitDecision.TargetRefuted mismatchedProof) with
    | Error e when e.Contains "TargetStatement" -> check "TargetRefuted must match request target statement" true
    | _ -> check "TargetRefuted must match request target statement" false

    // 138 版：grade 污染修复——TargetRefuted 的 grade 只由目标反方决定，无关证据不参与。
    let unrelatedClaim = mkClaim "unrelated-target" scopeEarth

    let unrelatedWeakW =
        let body =
            { Id = WarrantId ""
              ClaimId = unrelatedClaim.Id
              Polarity = Supports
              Kind = Observation
              Rule = "observation/v1"
              Strength = Weak
              Scope = scopeEarth
              Origin = Provenance.create fixedClock "obs" "observation/v1"
              VerifierWitnesses = [ mkWitness ]
              DependencyWarrantIds = []
              UltimateSourceIds = [ SourceId "s-unrelated" ]
              IntroducedBy = "" }

        { body with
            Id = EventCodec.warrantIdOfData body }
        |> Warrant.create
        |> function
            | Ok w -> w
            | Error e -> failwith e

    let trLedger2 =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed refuteTarget
              ContributionAccepted refuteW
              ClaimFramed unrelatedClaim
              ContributionAccepted unrelatedWeakW ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    match RefutationProof.toStopProof trLedger2 refuteProof with
    | Error e -> check "TargetRefuted grade uses target opposition only (proof builds)" false
    | Ok proof ->
        check "TargetRefuted grade uses target opposition only (proof builds)" true
        checkEq "TargetRefuted grade excludes unrelated evidence" (gradeOfWarrant refuteW) (StopProof.grade proof)

        match verifyExitDecision claimTestIntent.Request trLedger2 (ExitDecision.TargetRefuted refuteProof) with
        | Ok _ -> check "TargetRefuted succeeds with unrelated ledger evidence" true
        | Error _ -> check "TargetRefuted succeeds with unrelated ledger evidence" false

    // 138 版：报告拒绝 out-of-scope finding——报告 scope=earth 却引用 scopeA 的真实 claim。
    let outOfScopeCanonicalProof =
        proofForFindings
            reportLedger
            [ { Text = reportOutOfScopeClaim.Statement
                ClaimIds = [ reportOutOfScopeClaim.Id ]
                WarrantIds = [ Warrant.id reportOutOfScopeW ]
                EvidenceIds = []
                Polarity = Supports
                Grade = gradeOfWarrant reportOutOfScopeW
                Qualification = { ScopeNote = None; Caveats = [] } } ]

    let outOfScopeCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ { Text = reportOutOfScopeClaim.Statement
                ClaimIds = [ reportOutOfScopeClaim.Id ]
                WarrantIds = [ Warrant.id reportOutOfScopeW ]
                EvidenceIds = []
                Polarity = Supports
                Grade = gradeOfWarrant reportOutOfScopeW
                Qualification = { ScopeNote = None; Caveats = [] } } ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade outOfScopeCanonicalProof))
            outOfScopeCanonicalProof

    match verifyCanonicalReport claimTestIntent.Request reportLedger outOfScopeCanonicalProof outOfScopeCanonical with
    | Error e when e.Contains "out-of-scope" -> check "report rejects out-of-scope finding" true
    | _ -> check "report rejects out-of-scope finding" false

    // 138 版 review blocking 回归：停证引用未在报告展示的证据 → 拒绝（grade 注水防御——
    // 装配不能把强 warrant 塞进 digest 集抬高 grade）。
    let paddedDigests =
        [ (Warrant.data reportSupportW).IntroducedBy
          (Warrant.data reportOtherW).IntroducedBy ]

    let paddedProof =
        let placeholderGrade =
            StopProof.grade (proofForFindings reportLedger [ reportFinding ])

        match
            StopProof.create
                [ { ObligationId = "contract"
                    DischargeEventDigests = paddedDigests } ]
                []
                OpenWorld
                placeholderGrade
                []
                paddedDigests
        with
        | Ok p -> p
        | Error e -> failwith e

    let paddedCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ reportFinding ]
            []
            []
            []
            []
            (ReportGrade.Graded(StopProof.grade paddedProof))
            paddedProof

    match verifyCanonicalReport claimTestIntent.Request reportLedger paddedProof paddedCanonical with
    | Error e when e.Contains "not in report findings" ->
        check "stop proof cannot reference unshown evidence (grade padding blocked)" true
    | _ -> check "stop proof cannot reference unshown evidence (grade padding blocked)" false

    // 140 版评审 #9：grade 语义 = 报告保证等级（按引用集）——未引用的弱证据不拖低报告 grade；
    // 引用了弱证据则诚实下降（报告选择了更弱的保证）。
    let weakSupportClaim = mkClaim "weak-target-140" scopeEarth
    let strongSupport = mkWarrantFor weakSupportClaim scopeEarth Supports [ "s-strong" ]

    let weakSupport =
        mkWarrantFor weakSupportClaim scopeEarth Supports [ "s-weak" ]
        |> fun w ->
            let body =
                { (Warrant.data w) with
                    Strength = Weak }

            { body with
                Id = EventCodec.warrantIdOfData body }
            |> Warrant.create
            |> function
                | Ok x -> x
                | Error e -> failwith e

    let weakOppose = mkWarrantFor weakSupportClaim scopeEarth Opposes [ "s-opp" ]

    let weakLedger =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed weakSupportClaim
              ContributionAccepted strongSupport
              ContributionAccepted weakSupport
              ContributionAccepted weakOppose ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    // 报告只引用强支持：grade = 强支持的保证等级（弱证据被排除在引用集外）。
    let strongOnlyFinding =
        { Text = weakSupportClaim.Statement
          ClaimIds = [ weakSupportClaim.Id ]
          WarrantIds = [ Warrant.id strongSupport; Warrant.id weakOppose ] // 双侧引用强侧与反方
          EvidenceIds = []
          Polarity = Supports
          Grade = gradeOfWarrant strongSupport
          Qualification = { ScopeNote = None; Caveats = [] } }

    let strongOnlyCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ strongOnlyFinding
              { Text = weakSupportClaim.Statement
                ClaimIds = [ weakSupportClaim.Id ]
                WarrantIds = [ Warrant.id weakOppose ]
                EvidenceIds = []
                Polarity = Opposes
                Grade = gradeOfWarrant weakOppose
                Qualification = { ScopeNote = None; Caveats = [] } } ]
            []
            []
            []
            []
            (ReportGrade.Graded(gradeOfWarrant strongSupport))
            (proofForFindings reportLedger [ strongOnlyFinding ])

    // 引用集含弱支持时保证等级诚实下降。
    let weakIncludedFinding =
        { strongOnlyFinding with
            WarrantIds = [ Warrant.id weakSupport; Warrant.id weakOppose ]
            Grade = gradeOfWarrant weakSupport }

    let weakIncludedCanonical =
        CanonicalReport.compile
            claimTestIntent.Request.Intent
            scopeEarth
            [ weakIncludedFinding
              { Text = weakSupportClaim.Statement
                ClaimIds = [ weakSupportClaim.Id ]
                WarrantIds = [ Warrant.id weakOppose ]
                EvidenceIds = []
                Polarity = Opposes
                Grade = gradeOfWarrant weakOppose
                Qualification = { ScopeNote = None; Caveats = [] } } ]
            []
            []
            []
            []
            (ReportGrade.Graded(gradeOfWarrant weakSupport))
            (proofForFindings reportLedger [ weakIncludedFinding ])

    // 双向锚定：排除弱证据 → 保证等级（可靠性）保持强侧；引用弱证据 → 诚实降级。
    // （Independence 维度 = 依赖簇数，是增强维度，不参与 meet——见 EPISTEMICS.md §2。）
    let strongGrade =
        StopProof.grade (proofForFindings weakLedger [ strongOnlyFinding ])

    let weakGrade =
        StopProof.grade (proofForFindings weakLedger [ weakIncludedFinding ])

    check "unreferenced weak evidence does not lower report grade" (strongGrade.Reliability = Corroborated)
    check "referenced weak evidence honestly lowers report grade" (weakGrade.Reliability = Tentative)
    let refuteClaim = mkClaim "refute-target" scopeEarth
    let refuteWarrant = mkWarrantFor refuteClaim scopeEarth Opposes [ "s-refute" ]

    let refuteLedger =
        foldAll MeditationLedger.Empty [ ClaimFramed refuteClaim; ContributionAccepted refuteWarrant ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let ghostWarrantId = WarrantId "not-in-ledger"

    match
        RefutationProof.create
            refuteClaim.Id
            [ Warrant.id refuteWarrant; ghostWarrantId ]
            scopeEarth
            RefutationRule.LogicalCounterexample
            ClaimMorphology.Universal
    with
    | Error e -> check "refutation proof construction succeeds" false
    | Ok proof ->
        check "refutation proof construction succeeds" true

        match RefutationProof.toStopProof refuteLedger proof with
        | Error _ -> check "second invalid warrant not ignored (toStopProof rejects)" true
        | Ok _ -> check "second invalid warrant not ignored (toStopProof rejects)" false

    // P0-1：权柄不公开——issue/fromFields/create 必须非 public（internal），
    // 外部代码（非 TestHarness 程序集）无法签发 witness 或恢复任意 verifier ID。
    let asm = typeof<VerifierWitness>.Assembly

    let staticMethodsNamed (name: string) =
        asm.GetTypes()
        |> Array.collect (fun t ->
            t.GetMethods(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic)
            |> Array.filter (fun m -> m.Name = name))

    let issueMethods =
        staticMethodsNamed "issue"
        |> Array.filter (fun m -> m.DeclaringType.FullName.Contains "VerifierWitness")

    let fromFieldsMethods =
        staticMethodsNamed "fromFields"
        |> Array.filter (fun m -> m.DeclaringType.FullName.Contains "VerifierWitness")

    let createMethods =
        staticMethodsNamed "create"
        |> Array.filter (fun m ->
            m.DeclaringType.FullName.Contains "+Accepted"
            || m.DeclaringType.FullName.Contains "+Validated"
            || m.DeclaringType.FullName.Contains "+Constructed")

    check
        "VerifierWitness.issue is internal"
        (issueMethods.Length > 0
         && issueMethods |> Array.forall (fun m -> not m.IsPublic))

    check
        "VerifierWitness.fromFields is internal"
        (fromFieldsMethods.Length > 0
         && fromFieldsMethods |> Array.forall (fun m -> not m.IsPublic))

    check
        "Accepted/Validated/Constructed.create are internal"
        (createMethods.Length >= 3
         && createMethods |> Array.forall (fun m -> not m.IsPublic))

    // P0-2：witness 不出程序集——Accepted.witnesses/Warrant.witnesses 必须非 public。
    let witnessesAccessors =
        asm.GetTypes()
        |> Array.collect (fun t -> t.GetMethods(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic))
        |> Array.filter (fun m ->
            m.Name = "witnesses"
            && (m.DeclaringType.FullName.Contains "+Accepted"
                || m.DeclaringType.FullName.Contains "+Warrant"
                || m.DeclaringType.FullName.Contains "+Validated"))

    check
        "witness accessors are internal (no laundering)"
        (witnessesAccessors.Length >= 2
         && witnessesAccessors |> Array.forall (fun m -> not m.IsPublic))

    // 139 版评审 #6：conclude/appendAndFold 不得公开——meditate 是唯一写入入口
    // （外部不能构造自洽但未经验证的 proof/canonical 直接写完成事件）。
    let reportModuleType =
        typeof<Meditator.Report.CanonicalReport>.Assembly.GetType("Meditator.Report")

    let concludeMethod =
        reportModuleType.GetMethod(
            "conclude",
            System.Reflection.BindingFlags.Static
            ||| System.Reflection.BindingFlags.Public
            ||| System.Reflection.BindingFlags.NonPublic
        )

    check
        "conclude is not public (meditate is the only write entry)"
        (not (isNull concludeMethod) && not concludeMethod.IsPublic)

    let kernelModuleType = typeof<MeditationStop>.Assembly.GetType("Meditator.Kernel")

    let appendMethod =
        kernelModuleType.GetMethod(
            "appendAndFold",
            System.Reflection.BindingFlags.Static
            ||| System.Reflection.BindingFlags.Public
            ||| System.Reflection.BindingFlags.NonPublic
        )

    check "appendAndFold is not public" (not (isNull appendMethod) && not appendMethod.IsPublic)

    // P0-1：Warrant.data（WarrantData 含 VerifierWitnesses）也必须 non-public——
    // 反射测试此前只查名为 witnesses 的访问器，漏掉 data 这条旁路。
    let dataAccessors =
        asm.GetTypes()
        |> Array.collect (fun t -> t.GetMethods(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic))
        |> Array.filter (fun m -> m.Name = "data" && m.DeclaringType.FullName.Contains "+Warrant")

    check
        "Warrant.data is internal (no witness leakage)"
        (dataAccessors.Length >= 1
         && dataAccessors |> Array.forall (fun m -> not m.IsPublic))

    // P0-1（137 版）：EventCodec.decode/decodePayload 必须 non-public——公开即
    // "反序列化铸造口"（手工拼 canonical 行经 fromFields 获得 Warrant）。
    let decodeMethods =
        asm.GetTypes()
        |> Array.collect (fun t -> t.GetMethods(BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.NonPublic))
        |> Array.filter (fun m ->
            (m.Name = "decode" || m.Name = "decodePayload")
            && m.DeclaringType.FullName.Contains "EventCodec")

    check
        "EventCodec.decode/decodePayload are internal (no codec minting)"
        (decodeMethods.Length >= 2
         && decodeMethods |> Array.forall (fun m -> not m.IsPublic))

    // P0-2：observation witness 不能造 Derivation warrant（缺 Inference witness）——
    // Witness 不能洗白为另一种认识权力的证明。
    let obsW =
        VerifierWitness.issue Verifiers.observation VerifierKind.Observation "obs"

    let p2Claim = mkClaim "p2" scopeEarth

    let derivationFromObs =
        { Id = WarrantId "w-deriv"
          ClaimId = p2Claim.Id
          Polarity = Supports
          Kind = Derivation
          Rule = "rule/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "p" "protocol/v1"
          VerifierWitnesses = [ obsW ] // 只有 observation witness，无 inference
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s" ]
          IntroducedBy = "" }
        |> Warrant.create

    match derivationFromObs with
    | Error e when e.Contains "incompatible" -> check "observation witness cannot create derivation warrant" true
    | _ -> check "observation witness cannot create derivation warrant" false

    // P0-2：observation witness 不能造 SourceSpan warrant。
    let sourceSpanFromObs =
        { Id = WarrantId "w-src"
          ClaimId = p2Claim.Id
          Polarity = Supports
          Kind = SourceSpan
          Rule = "rule/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "p" "protocol/v1"
          VerifierWitnesses = [ obsW ]
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s" ]
          IntroducedBy = "" }
        |> Warrant.create

    match sourceSpanFromObs with
    | Error e when e.Contains "incompatible" -> check "observation witness cannot create source-span warrant" true
    | _ -> check "observation witness cannot create source-span warrant" false

    // fold 重验为纵深防御：create 拦截后不兼容 warrant 无法进入账本（构造即拒）。
    match sourceSpanFromObs with
    | Error _ -> check "incompatible warrant cannot reach ledger (create intercepts)" true
    | Ok _ -> check "incompatible warrant cannot reach ledger (create intercepts)" false

    // P0-3：Schema witness 不能单独造 Observation/SourceSpan warrant（主要 witness 强制）。
    let schemaW =
        VerifierWitness.issue Verifiers.schema VerifierKind.Schema "schema-check"

    let observationFromSchema =
        { Id = WarrantId "w-obs"
          ClaimId = p2Claim.Id
          Polarity = Supports
          Kind = Observation
          Rule = "rule/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "p" "protocol/v1"
          VerifierWitnesses = [ schemaW ] // 只有 schema witness，无 observation
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s" ]
          IntroducedBy = "" }
        |> Warrant.create

    match observationFromSchema with
    | Error e when e.Contains "incompatible" -> check "schema witness cannot create observation warrant" true
    | _ -> check "schema witness cannot create observation warrant" false

    let sourceSpanFromSchema =
        { Id = WarrantId "w-src2"
          ClaimId = p2Claim.Id
          Polarity = Supports
          Kind = SourceSpan
          Rule = "rule/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "p" "protocol/v1"
          VerifierWitnesses = [ schemaW ]
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s" ]
          IntroducedBy = "" }
        |> Warrant.create

    match sourceSpanFromSchema with
    | Error e when e.Contains "incompatible" -> check "schema witness cannot create source-span warrant" true
    | _ -> check "schema witness cannot create source-span warrant" false

    // P0-4：verifierId 白名单——kind/verifierId 不匹配的恢复 witness 被 Warrant.create 拒绝。
    let forgedWitness =
        VerifierWitness.fromFields VerifierKind.Observation "hacker/v1" "dig"

    let forgedWarrant =
        { Id = WarrantId "w-forged"
          ClaimId = p2Claim.Id
          Polarity = Supports
          Kind = Observation
          Rule = "rule/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "p" "protocol/v1"
          VerifierWitnesses = [ forgedWitness ]
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s" ]
          IntroducedBy = "" }
        |> Warrant.create

    match forgedWarrant with
    | Error e when e.Contains "whitelist" -> check "forged verifierId rejected at create" true
    | _ -> check "forged verifierId rejected at create" false

    // P0-9：WarrantId 必须由内容派生——fold 重验拒绝任意指定 ID 的 warrant（create 因编译
    // 顺序不校验，账本路径兜底；codec 恢复路径同规则）。
    let badIdBody =
        { Id = WarrantId "arbitrary-id"
          ClaimId = p2Claim.Id
          Polarity = Supports
          Kind = Observation
          Rule = "observation/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "obs" "observation/v1"
          VerifierWitnesses = [ obsW ]
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s" ]
          IntroducedBy = "" }

    let badIdWarrant =
        Warrant.create badIdBody
        |> function
            | Ok w -> w
            | Error e -> failwith e

    let badIdLedger =
        foldAll MeditationLedger.Empty [ ClaimFramed p2Claim ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    match fold badIdLedger (ContributionAccepted badIdWarrant) with
    | Error(InvalidTransition _) -> check "warrant id must be derived (fold rejects arbitrary id)" true
    | _ -> check "warrant id must be derived (fold rejects arbitrary id)" false

    // P0-1（138 版）：提交后 WarrantId 仍匹配身份——ProducedAt/IntroducedBy 不参与 ID；
    // 集合字段排序去重（重复来源/顺序无关）。
    let p01Claim = mkClaim "p01-identity" scopeEarth

    let p01Body =
        { Id = WarrantId "p01-w"
          ClaimId = p01Claim.Id
          Polarity = Supports
          Kind = Observation
          Rule = "observation/v1"
          Strength = Strong
          Scope = scopeEarth
          Origin = Provenance.create "2024-01-01T00:00:00Z" "obs" "observation/v1"
          VerifierWitnesses = [ obsW ]
          DependencyWarrantIds = []
          UltimateSourceIds = [ SourceId "s1"; SourceId "s1" ]
          IntroducedBy = "" }

    let p01Warrant =
        { p01Body with
            Id = EventCodec.warrantIdOfData p01Body }
        |> Warrant.create
        |> function
            | Ok w -> w
            | Error e -> failwith e

    let p01Ledger2 =
        foldAll MeditationLedger.Empty [ ClaimFramed p01Claim ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let p01Committed =
        fold p01Ledger2 (ContributionAccepted p01Warrant)
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let stored = p01Committed.Warrants.[Warrant.id p01Warrant]

    checkEq
        "committed warrant id still matches identity"
        (Warrant.id stored)
        (EventCodec.warrantIdOfData (Warrant.data stored))

    let otherTimeBody =
        { p01Body with
            Origin = Provenance.create "2025-05-05T00:00:00Z" "obs" "observation/v1" }

    checkEq
        "ProducedAt does not change WarrantId"
        (EventCodec.warrantIdOfData p01Body)
        (EventCodec.warrantIdOfData otherTimeBody)

    let dedupedBody =
        { p01Body with
            UltimateSourceIds = [ SourceId "s1" ] }

    checkEq
        "duplicate sources canonicalize (same id)"
        (EventCodec.warrantIdOfData p01Body)
        (EventCodec.warrantIdOfData dedupedBody)

    // P0-2（138 版）：安全公共验证路径——外部（无 IVT）经 PublicVerification + warrantFromObservation
    // 完成合法 ClaimTest 观察 warrant（不经 internal API）。
    let publicClaim = mkClaim "public-path" scopeEarth

    match PublicVerification.observe "protocol-executed" publicClaim with
    | Error e -> check "public observe succeeds" false
    | Ok accepted ->
        check "public observe succeeds" true

        match warrantFromObservation fixedClock accepted [ SourceId "s-pub" ] scopeEarth with
        | Error e -> check "public warrant construction succeeds" false
        | Ok w ->
            check "public warrant construction succeeds" true
            check "public warrant strength is verifier-fixed (Moderate)" (Warrant.strength w = SupportStrength.Moderate)

            let pubLedger =
                foldAll MeditationLedger.Empty [ ClaimFramed publicClaim ]
                |> function
                    | Ok l -> l
                    | Error e -> failwith $"%A{e}"

            match fold pubLedger (ContributionAccepted w) with
            | Ok _ -> check "public warrant folds (valid ClaimTest path)" true
            | Error _ -> check "public warrant folds (valid ClaimTest path)" false

    // 139 版评审 #4：同一 Accepted 复制成多个 source 仍落同一依赖簇（witness 分组）——
    // 同一次观察不能伪装为多个独立证据簇。
    match PublicVerification.observe "protocol-x" publicClaim with
    | Error _ -> check "same accepted cannot mint multiple independent sources" false
    | Ok sharedAccepted ->
        match
            warrantFromObservation fixedClock sharedAccepted [ SourceId "fake-1" ] scopeEarth,
            warrantFromObservation fixedClock sharedAccepted [ SourceId "fake-2" ] scopeEarth
        with
        | Ok w1, Ok w2 ->
            check
                "same accepted cannot mint multiple independent sources"
                (List.length (dependencyClusters [ w1; w2 ]) = 1)
        | _ -> check "same accepted cannot mint multiple independent sources" false

    // 139 版评审 #3/#5：公共 opposing 路径（双侧 ClaimTest）+ producedAt 由调用方时钟注入。
    match PublicVerification.observe "protocol-opp" publicClaim with
    | Error _ -> check "public opposing path works" false
    | Ok oppAccepted ->
        match warrantFromObservationOpposing fixedClock oppAccepted [ SourceId "s-opp" ] scopeEarth with
        | Error _ -> check "public opposing path works" false
        | Ok oppW ->
            check "public opposing path works" (Warrant.polarity oppW = Opposes)

            check
                "public warrant provenance carries injected clock"
                (Provenance.producedAt (Warrant.data oppW).Origin = fixedClock)

    // 封闭验证操作可用：经 Verification.observe 提交材料获得 Accepted（无需权柄）。
    let observedClaim = mkClaim "observed" scopeEarth

    match Verification.observe "protocol-executed" observedClaim with
    | Ok accepted ->
        check "Verification.observe yields Accepted" (Accepted.value accepted = observedClaim)

        check
            "observe witness is observation kind"
            (Accepted.witnesses accepted
             |> List.forall (fun w -> VerifierWitness.kind w = VerifierKind.Observation))
    | Error _ -> check "Verification.observe yields Accepted" false

    // P0-2：oracle transcript 与事件账本统一管线——store 冻结 + 事件由 Kernel 统一 fold。
    let invocation =
        { MethodId = "test-method"
          MethodVersion = "1"
          PromptTemplateVersion = "1"
          CanonicalInput = "input"
          EvidenceSnapshotHash = "ev"
          ModelProfile = "m"
          PolicyVersion = policyVersion }

    let (InvocationKey oracleKey) = OracleInvocation.key invocation
    let transcript = "raw-oracle-transcript-中文"

    let validateTranscript (raw: string) : Result<ValidatedAnswer<string>, string> =
        if raw = transcript then
            Ok
                { Value = raw
                  TranscriptDigest = EventCodec.sha256Hex raw }
        else
            Error "unexpected transcript"

    let mutable askCount = 0

    let oracleAsk (_ct: CancellationToken) : Task<string> =
        task {
            askCount <- askCount + 1
            return transcript
        }

    let oracleJournal = InMemoryJournal([])
    let oracleEnv = makeEnv oracleJournal

    let firstCall =
        (ensureOracleAnswer oracleAsk validateTranscript invocation) oracleEnv CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    check "oracle first call succeeds" (Result.isOk firstCall)
    check "transcript frozen in store" (oracleEnv.TranscriptStore.TryGet oracleKey = Some transcript)

    // 138 版：分隔符碰撞消除——分量内出现 \u001F 时不同组合必须得不同 invocation key。
    let collisionA =
        { invocation with
            MethodId = "a\u001Fb"
            CanonicalInput = "c" }

    let collisionB =
        { invocation with
            MethodId = "a"
            CanonicalInput = "b\u001Fc" }

    check
        "invocation key separator collision eliminated"
        (OracleInvocation.key collisionA <> OracleInvocation.key collisionB)

    // 138 版：set 语义字段 canonical 化——顺序/重复不影响 request 身份。
    let setReqA =
        { claimTestIntent.Request with
            Contract =
                Some
                    { claimTestIntent.Request.Contract.Value with
                        RequiredSections =
                            [ ReportSection.Findings; ReportSection.Counterpoints; ReportSection.Findings ]
                        UnacceptableClaims = [ "x"; "y"; "x" ] } }

    let setReqB =
        { claimTestIntent.Request with
            Contract =
                Some
                    { claimTestIntent.Request.Contract.Value with
                        RequiredSections = [ ReportSection.Counterpoints; ReportSection.Findings ]
                        UnacceptableClaims = [ "y"; "x" ] } }

    checkEq "set-like contract fields canonicalize order" (canonicalRequest setReqA) (canonicalRequest setReqB)

    // review 回归：缓存命中路径的 digest 机械校验——validate 返回不一致 digest → fail closed。
    let lyingValidate (raw: string) : Result<ValidatedAnswer<string>, string> =
        if raw = transcript then
            Ok
                { Value = raw
                  TranscriptDigest = "wrong-digest" }
        else
            Error "unexpected transcript"

    let lyingCall =
        (ensureOracleAnswer oracleAsk lyingValidate invocation) oracleEnv CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    match lyingCall with
    | Error(MeditationStop.Blocked _) -> check "cached transcript digest mismatch fails closed" true
    | _ -> check "cached transcript digest mismatch fails closed" false

    // 139 版评审 #15：MethodHints 不进请求身份（身份稳定；P0 无方法选择层消费它）。
    let hintRequestA =
        { claimTestIntent.Request with
            MethodHints = [ "a\u001Fb" ] }

    let hintRequestB =
        { claimTestIntent.Request with
            MethodHints = [ "a"; "b" ] }

    check "MethodHints do not enter request identity" (canonicalRequest hintRequestA = canonicalRequest hintRequestB)

    let secondCall =
        (ensureOracleAnswer oracleAsk validateTranscript invocation) oracleEnv CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult()

    check "cache hit returns same answer" (Result.isOk secondCall)
    checkEq "no re-ask on cache hit" 1 askCount

    // 事件进账本（executor 批次 → Kernel 统一 fold；同一运行与重放一致）。
    let oracleEvent =
        OracleInvocationAccepted(oracleKey, EventCodec.sha256Hex transcript)

    let oracleLedger =
        foldAll MeditationLedger.Empty [ oracleEvent ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    checkEq "OracleInvocationAccepted folded (OracleCalls=1)" 1 oracleLedger.ResourceUsage.OracleCalls

    // P0-5：同一 invocation key 两个不同 digest 在 fold 层被拒（journal 与 store 分叉防线）。
    let transcriptLedger0 =
        foldAll MeditationLedger.Empty [ OracleInvocationAccepted(oracleKey, "d1") ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    match fold transcriptLedger0 (OracleInvocationAccepted(oracleKey, "d2")) with
    | Error(InvalidTransition _) -> check "same invocation different digest rejected" true
    | _ -> check "same invocation different digest rejected" false

    // 同 digest 幂等：不冲突（OracleCalls 计事件折叠次数，重放历史事实）。
    match fold transcriptLedger0 (OracleInvocationAccepted(oracleKey, "d1")) with
    | Ok _ -> check "same invocation same digest idempotent" true
    | Error _ -> check "same invocation same digest idempotent" false

    // P1-8：同 key 同 digest 完全 no-op——OracleCalls 不递增（唯一 oracle 调用数）。
    match fold transcriptLedger0 (OracleInvocationAccepted(oracleKey, "d1")) with
    | Ok l -> checkEq "duplicate OracleAccepted does not increment OracleCalls" 1 l.ResourceUsage.OracleCalls
    | Error _ -> check "duplicate OracleAccepted does not increment OracleCalls" false

    // store 冲突检测：同 key 异字节 → TranscriptConflict（同一 invocationKey 两个 accepted transcript）。
    let conflictJournal = InMemoryJournal([])
    conflictJournal.SeedAccepted(oracleKey, "t1")
    let store = conflictJournal :> IAcceptedTranscriptStore

    checkEq
        "PutIfAbsent same bytes → AlreadyStored"
        TranscriptPutOutcome.AlreadyStored
        (store.PutIfAbsent(oracleKey, "t1"))

    checkEq
        "PutIfAbsent diff bytes → TranscriptConflict"
        TranscriptPutOutcome.TranscriptConflict
        (store.PutIfAbsent(oracleKey, "t2"))

    // Kernel 集成：oracle 型 executor 的完整 meditate 运行，重放后 OracleCalls 一致、不重问。
    let oracleExecute: ObligationExecutor =
        fun obligation ->
            meditation {
                match obligation.Kind with
                | FrameClaim ->
                    let! answer = ensureOracleAnswer oracleAsk validateTranscript invocation

                    return
                        [ OracleInvocationAccepted(oracleKey, answer.TranscriptDigest)
                          ClaimFramed targetClaim ]
                | GenerateOpposition -> return [ ContributionAccepted opposingWarrant ]
                | GroundEvidence -> return [ ContributionAccepted supportingWarrant ]
                | _ -> return [ SweepCompleted ]
            }

    let integratedJournal = InMemoryJournal([])
    let integratedEnv = makeEnv integratedJournal

    match runMeditation integratedEnv claimTestIntent oracleExecute scenarioProvers compileCanonical with
    | Error stop ->
        check "oracle integrated meditation succeeds" false
        printfn "     stop = %A" stop
    | Ok _ ->
        check "oracle integrated meditation succeeds" true
        // 重放账本：oracle 事件确实被统一 fold。
        match replay integratedEnv integratedJournal.Lines with
        | Ok(ledger, _) -> checkEq "oracle event in replayed ledger" 1 ledger.ResourceUsage.OracleCalls
        | Error e ->
            check "oracle event in replayed ledger" false
            printfn "     %s" e

        // 重启恢复：store 保留 transcript → 不重问（askCount 不再增加）。
        let restoredJournal = InMemoryJournal(integratedJournal.Lines)
        restoredJournal.SeedAccepted(oracleKey, transcript)
        let restoredEnv = makeEnv restoredJournal
        let askCountBeforeRestored = askCount

        match runMeditation restoredEnv claimTestIntent oracleExecute scenarioProvers compileCanonical with
        | Error _ -> check "restored run succeeds without re-ask" false
        | Ok _ -> check "restored run succeeds without re-ask" (askCount = askCountBeforeRestored)

        // P0-5：transcript store 丢失（重启后未恢复）且重新询问得到不同 transcript →
        // journal 已有 OA(key, d1)，fold 拒绝 OA(key, d2) → 恢复被阻断（fail closed，不静默接受分叉）。
        // 单元级验证：部分运行的 journal（只有 OA 行）→ ensure 从空 store 重问 → append 异 digest 事件被拒。
        let forkJournal = InMemoryJournal([])
        let forkEnv = makeEnv forkJournal

        let mrLine =
            EventCodec.encode EventSchemaVersion policyVersion reducerVersion 0 (MeditationRequested "fork-request")

        let d1Line =
            EventCodec.encode
                EventSchemaVersion
                policyVersion
                reducerVersion
                1
                (OracleInvocationAccepted(oracleKey, "d1"))

        (forkJournal :> IMeditationJournal).Append 0 mrLine CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult() |> ignore

        (forkJournal :> IMeditationJournal).Append 1 d1Line CancellationToken.None
        |> fun t -> t.GetAwaiter().GetResult() |> ignore

        let divergentOracleAsk (_ct: CancellationToken) : Task<string> =
            task { return "a-completely-different-transcript" }

        let divergentValidate (raw: string) : Result<ValidatedAnswer<string>, string> =
            Ok
                { Value = raw
                  TranscriptDigest = EventCodec.sha256Hex raw }

        let forkOk =
            match replay forkEnv [ mrLine; d1Line ] with
            | Error _ -> false
            | Ok(ledgerF, _) ->
                let ensureResult =
                    (ensureOracleAnswer divergentOracleAsk divergentValidate invocation) forkEnv CancellationToken.None
                    |> fun t -> t.GetAwaiter().GetResult()

                match ensureResult with
                | Error _ -> false
                | Ok _ ->
                    let digestT2 = EventCodec.sha256Hex "a-completely-different-transcript"

                    let appendResult =
                        (appendAndFold forkEnv ledgerF (OracleInvocationAccepted(oracleKey, digestT2)))
                            forkEnv
                            CancellationToken.None
                        |> fun t -> t.GetAwaiter().GetResult()

                    match appendResult with
                    | Error(MeditationStop.Blocked _) -> true
                    | Error(MeditationStop.Inconsistent _) -> true
                    | _ -> false

        check "lost transcript store blocks recovery (no silent fork)" forkOk

    // 确定性：同参数两次编码 → 同 EventId 同行。
    let l1 = EventCodec.encode EventSchemaVersion "p" "r" 0 (CreditsConsumed 1)
    let l2 = EventCodec.encode EventSchemaVersion "p" "r" 0 (CreditsConsumed 1)
    checkEq "same input → same canonical line" l1 l2

    failures = failuresAtStart
