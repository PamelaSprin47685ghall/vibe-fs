我把这份 Repomix 按“`completed/` 的承诺 → 当前生产代码 → 静态门禁 → 测试/验收口径”对了一遍。结论不是“全是假完成”，而是：

**核心 CE 迁移确实做了不少真活，旧式显式状态机大体被拆掉了；但 `completed/` 里至少存在一处可以明确判定的“验收口径事后缩水”，以及一个非常严重的 DSL 审计盲区。更麻烦的是，这个盲区恰好罩住了 Finality 等关键时序代码，所以现在不能凭 `DSL ownership threshold=0` 宣布“状态机余孽已清零”。**

我的总裁决是：**`ce.md` 本体约可判“主体完成，但 proof 不可信到足以宣布 Clean Break”；`corrective.md` 应当重新打开；整个 completed 状态机清算不能算完全闭环。**

### 1. 最硬的一条：`corrective.md` 的确有“先定死验收标准，后面自己降级”的现象

仓库自己的 lifecycle 规则非常严格：

`active → completed` 的条件是“正式 docs、实现与 proof 闭环”；`completed/` 明确定义为“已经实现并验证完成”。更重要的是，Original proposal 启动后被冻结；范围变化只有**用户明确修改批准范围**才能 Amendment，执行者发现 blocker 时“不得缩减、扩大或重写批准范围”。 

而 `corrective.md` 的冻结验收标准写得非常具体：Manager Idle 要在现有 manager e2e 加“第二个独立 idle occasion”；DevOps 必须有“真实行为闭环测试，不限于 prompt regex”；manager-unhappy-path 至少覆盖第二个独立 idle；最终还要求 build/unit/integration/e2e/architecture+spec 全绿。文末甚至说，**满足这些 durable、race、behavior 与 e2e 证明，“本 Change 才算完成”**。 

但归档时它仍写：

> Scope (frozen)：按 Original proposal §0–§22 全量实施

然后马上把三个原验收项改成：

* DevOps 行为闭环测试 → Deferred；
* 第二个独立 Manager idle e2e → Deferred；
* 全量 unit 的 `EXEC_025_three_teacher` 挂起 → “不阻塞 close”。

同时明确写 `npm run check` **未作为 close 条件**。 

这已经不是普通“诚实披露已知问题”。**原 frozen scope 明说是 close 条件，之后执行者自己宣布 Deferred 不阻塞 close。** 在我检索到的这份 `corrective.md` 范围内，也没有看到相应的用户 Amendment 记录。

所以这一项我会直接判：

**确认存在验收标准漂移。按仓库自己的治理规则，它不应该进 `completed/`。**

而且 irony 很重：Proposal 自己还把“DevOps 只改 prompt、没有行为测试”列为禁止的伪修复。

---

### 2. `ce.md` 最大的问题：所谓 DSL “threshold=0”只查了约一半生产 F# 文件

这是这次审计里最危险的结构性问题。

`dsl-ownership.mjs` 的 `PROGRAM_DIRS` 只有：

`Agent / Application / Domain / Kernel / Process / Session`

**没有 `Infrastructure/`，也没有 `Journal/`。** 

我按这份 Repomix 实际机械统计了一遍：

| 范围                           |    F# 文件数 |
| ---------------------------- | --------: |
| 全部 `src/Wanxiangshu/**/*.fs` |       245 |
| DSL ownership 实际扫描           |       136 |
| 没进入该门禁                       |   **109** |
| 覆盖率                          | **55.5%** |

其中恰好有 **77 个 `Infrastructure` 文件、27 个 `Journal` 文件**。

更要命的是，这不是偶然漏扫。单测专门构造了：

```text
Infrastructure/Foo.fs
let mutable counter = 1
```

先验证 `scanText` 能认出这是违规，然后再验证整个 ratchet **必须返回成功**，理由明确写着：

> Infrastructure/ is outside PROGRAM_DIRS: never scanned, never reported.



我也直接在从 Repomix 解出的源码快照上执行了现有静态门：

```text
dsl-ownership: OK — 136 Program/Domain files
architecture: OK — 245 文件
direct-ce / dsl-ownership / ratchet: 48/48 pass
```

也就是说，**所有门全绿和“109 个生产 F# 文件根本没接受 DSL ownership 检查”可以同时成立，而且这是测试锁死的预期行为。**

这就足以否定：

> threshold=0 ⇒ 全生产代码没有状态机余孽

最多只能证明：

> threshold=0 ⇒ 被选中的 136 个文件没有被当前规则抓到。

---

### 3. 这个盲区不是无关基础设施：2N Finality 核心控制流就藏在里面

`ce.md` 最终声称 Reviewer continuation 已有唯一 owner、Finality 只等 durable witness，完成了 Direct CE clean break。

可是 2N 的核心编排在：

`Infrastructure/OpenCode/Tools/FinalityController.fs`

架构 gate 虽然会单独读这个文件，但检查的只是几个明确 token，例如禁止 `HostReviewGuard`、`ReviewChallenge`、`nudgeReviewer` 等 continuation writer。

它**没有对这个文件执行完整的 DSL/state-product/program-counter 审计**。

而这个文件确实含长期/并发控制状态，例如 `CancelToken` 自己持有：

```fsharp
let mutable cancelled = false
TaskCompletionSource<unit>
```

以及并发短路执行中的：

```fsharp
let remaining = ref (List.length tasks)
let results = ResizeArray<'a>()
```

 

这里我**不会把这些本身判成状态机**——它们很可能是正当的 cancellation / algorithm scratch。

真正的问题是：

**你现在没有机器证明能区分“正当物理状态”和“藏在 Infrastructure 里的第二运行时”。**

而测试还确保该目录不要被扫。

所以这是很典型的“门禁绿了，但门根本没装在关键房间门口”。

---

### 4. 状态乘积检测本身也比“字段名无关结构审计”弱很多

`fsharp-dsl-governance.md` 的 Final outcome 宣称已经获得“字段名无关的结构化识别”。它实际定义的自动检查对象是：

> 一个 record 内至少两个状态轴，本地 DU / option / bool。



实际 `stateType` 代码只有：

```text
bool
xxx option
ref
当前文件 locally-defined DU
```

才算状态轴。

因此至少这些东西天然看不见：

**跨文件 DU、Dictionary/HashSet 的 presence/absence、多个并行 registry、整数/字符串 phase tag、对象里的 TCS/lease/capability 组合，以及分散在两个 record/两个 module 里的联合程序状态。**

这不是理论问题。Student–Teacher 虽然真的删除了原来的大一统 `StudentRunCell`，当前代码已经明显干净很多，但现在仍有：

```text
runs
teacherOwners
teacherCalls
teacherCompletions
studentFinalCompletions
skillMutations
```

六个长期 Dictionary。代码注释主动声明“每个 registry 只代表一个 physical lifetime；没有一个编码 Student lifecycle stage”。

这可能完全正确。

但**目前的 DSL 门禁证明不了这句话**。它甚至没有规则可以问：

> `teacherCalls × teacherCompletions × studentFinalCompletions × current request kind` 的 presence 组合，是否事实上被 `HandleTurn` 用作一个隐式程序计数器？

所以我的判断是：

**旧状态机已经从“一个 record 六个字段”升级成了更容易辩称为 physical registry 的结构。当前看不出它已经重新构成非法状态机，但 proof 明显不足以排除这一点。**

这正是应该重点追的“残留余孽”。

---

### 5. 我还找到一个更具体的时序余孽：号称 B 类事件等待“零轮询”，Executor 实际又做了 100ms polling

正式 DSL 文档现在把等待分成：

| 类别                   | 要求            |
| -------------------- | ------------- |
| A 业务状态探测             | 有界因果重读，不得墙钟退避 |
| B 事件等待               | **事件驱动，零轮询**  |
| C deadline/watchdog  | 可以墙钟          |
| D cross-process lock | 另行裁决          |

而且明确说：

> Executor 定向等待属 B 类。

`reconciler-event-driven-de-polling.md` 也明确把 Executor 从 join-any `while + stash` 迁移到 targeted await，并写着 clean break。

但是现在实际生产 `ExecutorSummarize.awaitAgentWithPermit`：

```fsharp
let rec loop remainingMs =
    task {
        let! joined = runtime.AwaitAgentWithPermit(...)

        match joined with
        | Error ForkError.TimedOut when remainingMs > 0 ->
            let delayMs = min 100 remainingMs
            do! PtyTiming.timerTask delayMs
            return! loop (remainingMs - delayMs)
        ...
    }
```

即 `FamilyWaiting` 时**每 ≤100ms 重新探测一次**，总预算 600 秒。单测甚至要求至少调用三次。 

这在语义上就是：

**timer → probe → timer → probe**

只是从 `while` 换成了递归 CE。

它不是持久状态机，但它绝对是“把旧轮询换了个函数式皮肤”的时序残留，而且与当前“B 类事件等待，零轮询”的正式文档存在直接张力。

更巧的是 `ExecutorSummarize.fs` 又位于 `Infrastructure/`，所以刚才那个 DSL ownership 门禁不会管它。

这一项我会判 **P1，需要修**。

---

### 6. `TurnUnknown` 也没有按最初 ChatGPT 提案真正“消灭”，但原来的黑洞确实修掉了

最初《ChatGPT-时序控制流修复提案》的思路很激进：不要让 Unknown 穿过稳定化边界。

当前 `ReconcileProgram.TurnOutcome` 仍然公开包含：

```fsharp
| TurnUnknown
```

而 `ReconcileEvidence` 也仍有 `Unknown`。

所以如果按“Clean Break：业务稳定 outcome 里根本不存在 Unknown”这个标准，**没有完全做到。**

不过必须公平地说，原来最危险的 bug 已经实质修掉：

在 `IdleWake` 下，因果重读耗尽仍 Unknown 会进入 `RepairMissingFinalReport`，明确禁止静默 `StopPass`；Retry/Failure/Abort 因为没有 idle 权限才 `StopPass` 等下一真实 signal。

所以这里不是“原 bug 还在”。

我的分类是：

**概念残留/技术债，不是当前 P0 黑洞。**

---

### 7. 也不能把所有 Projection/DU 都当“状态机余孽”

例如当前：

```fsharp
FinalityResolution =
    Open
    | Rejected ...
    | Blessed ...
    | Undecided
```

以及 `LifeProjection` 的 `ActiveFinality / LastBlessing / Completed` 等。 

这类东西描述的是“世界上已经发生了什么”，从 durable facts fold 出来；它们本身不是程序计数器。

最初提案也正确区分过：

> Projection 是世界状态，不是程序 counter。

所以不能为了“零 state”把正常领域状态也删除。

我判断状态机余孽的标准是：

> **如果一个长期保存的字段/registry/DU 的主要用途是回答“代码下一步跑哪里”，而不是回答“世界发生了什么/哪个物理资源现在存在”，它就是程序计数器。**

---

## completed/ 各主要 Change 的审计评级

| Change                                  | 我的裁决                | 主要问题                                                                                              |
| --------------------------------------- | ------------------- | ------------------------------------------------------------------------------------------------- |
| `ChatGPT-时序控制流修复提案.md`                  | **B+，主体真完成**        | 2N、Reviewer hidden、Manager owner、idle 黑洞等真有实现；但后来语义又被 corrective 修改，且 `TurnUnknown` 没 Clean Break |
| `ce.md`                                 | **B，不能宣称状态机清零**     | 显式 RunState 等确实清理；最大问题是 DSL proof 只覆盖 136/245 文件                                                  |
| `corrective.md`                         | **D / 应 reopen**    | frozen completion criteria 后自行 Deferred，且 full check 不再作为 close；违反 changes lifecycle 自己的规则        |
| `fsharp-dsl-governance.md`              | **B-**              | record 内局部 DU/option/bool 的识别是真的，但被宣传成“结构化状态乘积识别”容易高估；跨 registry 完全盲                              |
| `dsl-structured-program-gap.md`         | **B-**              | Blogger 双写/影子状态确实清掉；自己承认类型级自动组合证明仍 NOT IMPLEMENTED，靠人工 proof 闭环                                   |
| `reconciler-event-driven-de-polling.md` | **C+**              | Reconciler 去墙钟 polling 做得对；但 Executor B 类等待重新出现 100ms timed re-probe                              |
| `projection-algebra-gap.md`             | **C+ / 有范围缩水但披露透明** | Completion criteria 原说 PROJ-008 剩余生产路径全迁，结果 `SuppressTransportOnly` 生产接线被“正式降级出本 Change”；至少没有隐瞒   |
| `Student & Teacher.md`                  | **B+**              | 原巨型 RunState cell 真拆了；但多 registry 组合仍需要更强 proof                                                   |
| `enforcer.md`                           | **A-/B+**           | 本来主要是 docs rebase，边界和未跑全 e2e 写得比较诚实                                                               |
| `cache.md`                              | **A-/B+**           | 从 Final outcome 看完成度较扎实，full check/e2e 有实际闭环                                                      |
| `canary-unbend.md`                      | **B**               | 主目标绿，但仍明确留 reviewer nudge coverage、AABB_TRACE_OUT 两个观察                                            |
| `waitfact-causal-renewal.md`            | **A-**              | 范围窄、clean break 明确、反作弊测试思路相对完整                                                                    |

---

## 最终裁决

**有“阳奉阴违/偷工减料”，但不是“整个 CE 重构都是假的”。**

最值得定性的三件事：

**第一，`corrective.md` 是实锤的生命周期违规。**
Frozen scope 里的验收条件没完成，又由执行方自行改成 Deferred，再移动到 `completed/`。按项目自己的治理规则，这个 close 无效。

**第二，`ce.md` 的 “DSL ownership threshold=0”具有误导性。**
它只检查 136/245 个 F# 生产文件，而且测试明确锁定 `Infrastructure/` 的违规“不报告”。而 Finality/Executor 等最关键的时序编排恰恰就在 Infrastructure。**这是 proof 层面的重大偷工。**

**第三，显式状态机大体清除了，但存在“隐式状态机重新藏进 registry/capability/timed recursion”的风险。**
其中 Executor `FamilyWaiting → 100ms timer → re-probe` 已经是一个具体残留；Student–Teacher 六 registry 目前则属于“没有证据证明违法，但现有 gate 完全无能力证明安全”。

反过来说，Manager/Reviewer 的核心迁移不是摆拍：`ManagerWorkflow` 当前明确成为 Manager terminal sequencing 的 sole owner；ReviewerWorkflow 也明确只从 durable witness 做决定，并声明没有 stored State/Stage counter。  TurnCompletion 还有静态 forbidden-reference gate，明确禁止重新塞回 Manager/Reviewer/StudentTeacher/Finality 业务。 所以这部分应该承认是真整改。

### 我建议现在直接开的“清余孽”修复

1. **立即把 `corrective.md` 从 completed reopen**，除非能给出用户明确批准缩减范围的 Amendment 证据；补 DevOps behavioral e2e、第二 idle e2e，并恢复 full gate close 条件。
2. **DSL ownership 改为扫描全部 245 个生产 F# 文件**。`Infrastructure`/`Journal` 不允许按目录整体豁免；只能对具体 physical/projection 类型用结构化 annotation 豁免。
3. 给 `Infrastructure/Foo.fs` 那个测试**反转断言**：在那里放 `let mutable counter` 必须 RED。再专门给 `FinalityController.fs`、`ExecutorSummarize.fs` 做 adversarial fixture。
4. **把 state-product gate 从“单 record”扩展到 runtime owner**：Dictionary/HashSet/TCS/option/cross-file DU 都算 axis；尤其扫描“多个 registry presence 被同一 HandleTurn/observe 函数联合 match”的情况。
5. **修 Executor FamilyWaiting**：B 类既然正式规定 event-driven zero polling，就应该让 recovery readiness 提供真实 waiter/pulse，而不是每 100ms `timerTask` 重新 `RequireFamilyRecovery`。
6. **进一步收口 `TurnUnknown`**：把它降为 reconciliation 私有的 `SnapshotObservation`，不要继续作为可 publish 的 `TurnOutcome`。这才能真正实现原提案的“Unknown 不穿过稳定化边界”。

如果按“君子不立危墙”的标准，我现在**不会批准“completed/ 已完成、CE 状态机清算完毕”这个总命题**。比较准确的状态应该是：

> **主干迁移成功；显式状态机清理基本成功；但验收治理有一次实锤违规，DSL proof 存在 44.5% 源码覆盖盲区，并至少发现一处 timer-driven polling 余孽。需要再做一轮真正的 Clean Break 审计后才能封板。**

---

# Active work

> 本文件是变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。

## Work origin

本文件原为对 `completed/` 与 DSL ownership 的审计报告（无独立 Proposal，据 GOV-008 标注 Work
origin，不伪造 Original proposal）。当前用户本轮明确要求将审计发现转为 Active 工作并授权启动
（GOV-007：用户明确请求即充分授权）。

## Scope (frozen)

按本审计「建议现在直接开的清余孽修复」六项：

1. `changes/active/corrective.md` 恢复 Deferred 关闭条件与 full gate 关闭条件（见其 Reopen 记录）。
2. `scripts/checks/dsl-ownership.mjs` 全量扫描 245 个生产 F# 文件；`Infrastructure/`、`Journal/`
   禁止目录级整体豁免，仅允许结构化 annotation 对具体 physical/projection 类型豁免。
3. 反转 ratchet 对 `Infrastructure/Foo.fs`「违规不报告」的断言（放 `let mutable counter` 必须 RED）；
   为 `FinalityController.fs`、`ExecutorSummarize.fs` 建 adversarial fixture。
4. `state-product` 门禁从「单 record」扩展到多 registry presence 联合 match 的隐式程序计数判定。
5. 修 Executor `FamilyWaiting`：B 类事件等待改事件驱动 waiter，去除 100ms `timerTask` re-probe。
6. 收口 `TurnUnknown`：降为 reconciliation 私有观察，不作为可 publish 的 `TurnOutcome`。

## Remaining work

1. corrective.md 三项 Deferred 关闭条件 + full gate 关闭条件（见 corrective.md Reopen）— **OPEN**。
2. ~~dsl-ownership.mjs 全量扫描全部生产 `.fs`；移除目录级豁免~~ — **DONE（HEAD）**：100% 生产
   `.fs` 扫描；无 Infrastructure/Journal 目录级豁免。
3. ~~反转 ratchet 对 Infrastructure 的豁免断言~~ — **DONE（HEAD）**：Infrastructure/Journal bare
   mutable 必须 RED。~~`FinalityController.fs` / `ExecutorSummarize.fs` 同构 adversarial
   fixture~~ — **DONE**：`dsl-ownership-ratchet.test.mjs` 永久 RED fixture + annotated positive control。
4. `state-product` 多 registry 分散 presence 分类 — **PARTIAL**：`StudentTeacherRuntime` 六 registry
   （`runs` / `teacherOwners` / `teacherCalls` / `teacherCompletions` / `studentFinalCompletions` /
   `skillMutations`）已登记为 audited manual-proof classification（注释）；仅靠
   `registry-joint-branch` 仍不足；分散 presence 自动化仍 OPEN。
5. ~~ExecutorSummarize 去除 100ms timer re-probe~~ — **DONE（HEAD）**：事件驱动
   `awaitChangeFrom` / permit pulse；C 类 deadline race 允许。
6. `TurnUnknown` 类型降级为 reconciliation 私有 `SnapshotObservation`（不得为 `TurnOutcome`
   case）— **OPEN**（正式 how/what 已要求；生产 DU 尚未 demote）。
7. FinalityRejected / LWR record-ready Closing work（见下文 Blocker）— **OPEN / in progress**。

## Completion criteria

- corrective.md 三项 Deferred 关闭条件已恢复并逐一实现 + 验证。
- `npm run check` 全量（build/unit/integration/e2e/architecture+spec）判绿作为 close 条件。
- ~~dsl-ownership 扫描 100% 生产 `.fs`；目录级豁免逃逸判红~~（HEAD 已满足）；~~
  FinalityController / ExecutorSummarize adversarial fixture/反例落盘~~（DONE）。
- ~~Executor B 类等待零轮询（无 timer-driven re-probe）~~（HEAD 已满足；docs/how 已列条款）。
- registry 联合 / 分散 presence：StudentTeacher 六 registry 已 manual-proof 分类；分散 presence
  自动化仍 OPEN。
- `TurnUnknown` 已从可 publish `TurnOutcome` 结构移除，仅为私有 `SnapshotObservation`。
- FinalityRejected / LWR blocker Closing work 完成且不宣称此前已关闭。

## Blockers

- 全量扫描可能暴露既有 Infrastructure/Journal pattern（leak/mutable/while），须逐项分类
  physical 或 remediation，不得批量豁免冲绿（见 `docs/how/dsl-structured-program.md`）。

---

## Blocker update 2026-08-09：FinalityRejected / LWR 因果竞态

已实证 `manager-unhappy-path` e2e 5 次中 1 次：`FinalityRejected` 先于 rejecting Reviewer 的
`BlogEntryCommitted` durable，永久 `WorkRecordRef` 因而缺 `# Work log`。这直接落在本 Change 对
`FinalityController.fs` 的审计/反例要求（Scope 3）和 B 类零轮询等待要求（Scope 5）内；不是可忽略的
canary 偶发失败。

### Closing work（未实现）

1. 依 GLORY-044/072/073 实现两段收束：durable REVISE 立即关闭 Reviewer continuation/cohort；
   `FinalityRejected` 仅在同一 journal snapshot 证明 `BlogEntryCommitted` coverage 已越过 terminal
   frontier 且 canonical LWR 已物化后落盘。
2. `FinalityController` 的 record-ready 等待使用 `AgentJournal.awaitChangeFrom`，禁止 timer/sleep
   re-probe；crash 或本地 waiter disposal 后从 durable evidence 续等。`BloggerRequestAbandoned` 只废弃
   一次 Blogger attempt，不能写 partial rejection。
3. 建立 adversarial / e2e 回归：延迟 Blog commit 时 cohort 已关闭但无 `FinalityRejected`；commit 后唯一
   record 含 `# Work log`；覆盖 crash recovery、Blogger abandonment 和 timer-polling 禁止。
   **PARTIAL**：`manager-unhappy-path` rejection 断言已收紧为必须 `/# Work log\n\S/`（禁止缺 Work log
   的 OR 假绿）；延迟 commit / crash / abandonment 分支仍缺。

### Additional closing criteria

- 任何 rejection blob 都绑定 record-ready 的同 snapshot coverage/materialization；不得留下缺少
  `# Work log` 的 WorkRecordRef。
- REVISE 后不再发 Reviewer continuation 或等待 sibling terminal；record-ready 仅由 Journal event 唤醒。
- 本 blocker 的实现与回归完成后，仍须满足既有全部 Completion criteria；当前仅完成语义归档，不宣称
  production/test 或 full gate 已完成。
