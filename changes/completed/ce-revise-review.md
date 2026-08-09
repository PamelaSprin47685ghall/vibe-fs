**Status:** Completed (archived review that drove ce-student-teacher-collapse)

---

我按新快照重新审了一遍。**这次比上一版扎实很多，我会撤销上次大约一半的判红；但还不能给"状态机余孽清零 / completed 全部可信"盖章。**

这份新文件确实是整个仓库的 Repomix 合并快照，可用于直接做代码级复审。

### 先说已经真修掉的

**1. DSL ownership 漏扫 `Infrastructure/Journal`：已修。**

现在 `dsl-ownership.mjs` 从整个 `src/Wanxiangshu` 递归取所有 `.fs`，不再用原来的 `PROGRAM_DIRS` 半仓扫描。

我把新 Repomix 解出来直接跑了：

```text
dsl-ownership: OK — 246 Program/Domain files
architecture: OK — 246 文件
dsl ownership + ratchet 单测：65/65 pass
ratchet baseline：OK
```

这里输出字符串还叫 `Program/Domain files`，只是旧文案；实际扫描确实是 **246 个生产 F# 文件**。

所以我上次说的"44.5% 源码盲区"现在可以正式撤销。

**2. `TurnUnknown`：这次是真 Clean Break。**

当前 `TurnOutcome` 只剩 InProgress / NeedsContinuation / Completed / Aborted / Failed；`TurnUnknown` 被降成 reconciliation-private 的 `SnapshotObservation`，而 `PublishTurn.Outcome` 只能是正式 `TurnOutcome`。

这是我上次要求的结构性修复，不是加一个 if。

**3. Executor 的 100ms timer re-probe：也真拆了。**

现在 FamilyWaiting 后不会 `timerTask 100 → probe → timerTask 100`。它先记录 Journal revision，重试条件来自 `AwaitJournalChangeFrom`，wall clock 只与总 deadline race。

这个现在属于：

```text
真实 Journal event
        ↓
重新检查 permit
```

而不是：

```text
100ms
 ↓
看看好了没
 ↓
100ms
```

我接受这个整改。

而且 `fix-revise.md` 后来主动撤销了"DSL 静态门已经能识别 timer re-probe"这个 overclaim，没有继续硬吹自动证明，这点反而是好的。

---

## 但还有两个问题，我认为一个是明确未闭环，一个是状态机核心嫌疑

### P0/P1：`corrective.md` 的完整 E2E close 条件其实仍然没完成

原冻结验收明确写的是：

> `build / unit / integration / e2e / architecture+spec gates 全绿`。

而 `package.json` 非常明确：

```text
npm run check
= lint
+ build
+ unit
+ integration

test:e2e 是另外一条命令
check:release 才会执行 test:e2e -- --repeat 3
```

精确事实（`package.json:22-37`）：
- `check` = `lint` + `build` + `test`(unit) + `test:integration`，不含 e2e。
- `check:release` = `warmup:opencode` + `check` + `test:e2e -- --repeat 3` + `test:package` + `npm pack --dry-run`。
- e2e 套件现状：`tests/e2e/cases/` 下 27 个 `*.test.mjs`、`tests/e2e/scenarios/` 下 31 个 `.toml`；provider 为 strict scripted mock（`tests/e2e/support/strict-mock-responses.js`），不依赖外部网络；`--repeat` 合法取值 1–3（`tests/e2e/run.mjs:36-45`）。
- 无 CI 配置（`.github/workflows` 等均不存在），门禁只在本地显式触发——这加重"必须真跑一次 check:release 才能封板"的必要性。

更关键的是，`corrective.md` / `fix.md` 最后自己已经承认：

> 本次 `npm run check` 不包含完整 `test:e2e`；close 证据只有 `manager-unhappy-path` 和 `devops-mechanical-repair-loop` 两条定向 canary，**并没有跑完整 E2E suite**。

因此这里逻辑非常简单：

```text
冻结条件：
e2e gate 全绿

实际证据：
2 个 targeted e2e 全绿

2 targeted e2e ≠ e2e gate 全绿
```

这次比上一版好在**它没有继续隐瞒，而是在文末诚实澄清**。

但诚实澄清并不能让验收条件自动消失。

所以 `corrective.md` / `fix.md` 现在仍存在：

> **"Final outcome 宣布 close，但自己又承认一个冻结 close 条件实际上没执行。"**

如果按仓库自己"批准范围不得自行缩水"的规则，这仍然不能算严格 Completed。

最简单的修法不是再写文档：**跑一次完整 `npm run test:e2e`；如果你们真正的发布 proof 要求 repeat 3，则直接跑 `check:release`。**

---

# 最关键：Student–Teacher 仍然有"分布式状态机"的实质嫌疑

这是我这轮最不满意的地方。

新正式 DSL 规则自己写得非常好：

> 多个长期 registry / Dictionary / HashSet 的 presence，如果被同一个 `HandleTurn` / `observe` 联合用于决定下一步业务动作，就是**隐式程序计数器，等价于单 record 状态机**。

但是 Student–Teacher 的 manual proof 宣称六个 registry：

```text
runs
teacherOwners
teacherCalls
teacherCompletions
studentFinalCompletions
skillMutations
```

都是 physical mailbox，因此"不构成 lifecycle stage PC"。

我重新对了真实 `HandleTurn`。

Student 路径实际上是：

```text
tryRun presence
  ↓
tryFinalCompletion presence
  ↓
currentStudentRequestKind
  ↓
选择：
  final complete / release
  StudentLearn → sendCompile
  StudentCompile → sendCompile nudge
```

生产代码就是这样。

Teacher 路径更加明显：

```text
tryTeacherCall presence
        ↓
tryTeacherCompletion presence
        ↓
如果 Some:
    consume completion
    remove teacherCompletions
    remove teacherCalls
    complete parent waiter

如果 None:
    sendTeacherNudge
```

这和它自己的 DSL 判据存在非常直接的张力。

换句话说，现在只是把原来的：

```fsharp
State = WaitingTeacherReturn
State = WaitingTeacherCompletion
State = Compile
```

变成：

```text
teacherCalls exists?
teacherCompletions exists?
studentFinalCompletions exists?
PromptAuthority says Compile?
```

然后在 `HandleTurn` 里重新推导"程序现在在哪一步"。

### 我还专门打了一个反作弊实验

我用当前 `scanText` 构造了与这里同形的最小代码：

```text
registry A
registry B

tryA()
tryB()

handle:
    match tryA() with
    | Some ->
        match tryB() with
        | Some -> sendComplete
        | None -> sendNudge
```

当前 DSL gate 返回：

```text
[]
```

**完全不报。**

原因也已被精确定位：自动 `registry-joint-branch` 只在 `scripts/checks/dsl-ownership.mjs:601-634` 实现，`:617` 只匹配以 `match`/`if` 开头的单行，`:622` 要求同行 ≥2 个 registry direct probe；当前 probe 被包进 `tryTeacherCall` / `tryTeacherCompletion` 两个辅助函数（`StudentTeacherRuntime.fs:146-156`），两个条件都不满足。这正好就是当前生产代码的写法。

---

## 所以 Student–Teacher 到底算不算状态机？

我会判：

**目前不能接受"已人工证明不是状态机"的结论。**

这和上次不同：上次我是说"门禁证明不了，所以有风险"；这次把代码和它自己新写的判据放在一起后，问题更明确了。

特别是 Teacher 这一段：

```text
teacherCalls = Some
teacherCompletions = None
→ send nudge

teacherCalls = Some
teacherCompletions = Some
→ consume completion / complete waiter
```

这里 `teacherCompletions` 的 **presence 本身就在选择下一业务动作**。

它当然携带真实物理 payload，这一点没错。但：

> **"里面装着真实 payload"并不能证明"它的 presence 没有同时充当程序 counter"。**

一个对象完全可以既是 mailbox，又被滥用为 stage bit。

这就是现在 manual proof 最偷换概念的地方。

我不会把它定性为故意"阳奉阴违"，因为文档已经诚实披露自动 detector 看不见跨 helper 的组合；但是：

**manual proof 给自己的核心嫌疑代码判了无罪，而它引用的正式判据反而很像应该判有罪。**

这项我会给 **REVISE**。

---

# 怎么彻底解决 Student–Teacher，而不是再加强 regex

我不建议继续做：

```text
registry-joint-branch-v2
try* helper scanner
更多字符串规则
```

那会进入猫鼠游戏。

**先补一处原文漏掉、且比 joint presence 更硬的违规点**：

`TeacherCompletionScope.CompletionRun: ProviderRunIdentity option`（`src/Wanxiangshu/Session/StudentTeacherRuntime.fs:42`）是显式 stage bit，被三个 Host 边界接力写读：`Return` 置 `None`（`:390`）→ `TextComplete` 补 `Some`（`:437-439`）→ `HandleTurn` 校验 `IsSome`（`:541`）。一个 option 字段编码"已 return / 已见固定 completion"两阶段，删掉它可用两次 `let!` 表达同样顺序——正是 DSL-002 判据的直接反例。

**然后做方向修正**。原文要求 Student compile 链同样 collapse 到 CE，这里**拒绝**该项：

Student 没有父 tool call 提供调用栈。要 collapse 就必须在 `ObserveChatMessage` 创建 run 时 fork 一个跨多个 Host turn 的长跑 detached task，而那个 task 的"跑到第几步"没有任何 durable 对应物——它死了没人知道，而 `AcceptedContinuationIds` 仍指向 Compile。这不是消灭 PC，是把 PC 藏到一个连 fail-closed 都做不到的地方，违反 DSL-004。

**原文陈述的"重启后必须恢复未完成 teacher call"前提也不成立**：

- in-flight teacher call 零 durable 证据。Journal 侧只有 `StudentTeacherLinked` / `StudentTeacherClosed`（`src/Wanxiangshu/Kernel/Fact.fs:440-446`），无 pending-call 标记。
- 等待者是父 `teacher` 工具调用本身（`Infrastructure/OpenCode/Tools/StudentTeacherTools.fs:20` → `StudentTeacherRuntime.fs:362` `let! answer = waiter.Task`），活在同一 OpenCode 进程内；进程死亡后，要被唤醒的对象已不存在。
- 仓库既有重启语义就是 fail-closed（`Infrastructure/OpenCode/Host/SessionQuiescenceGate.fs:21-26`，HOST-007）。
- 反向路径被条款否决：PERSIST-011（`docs/what/persist.md:75-77`）禁止 Journal 保存问题、回答、推测阶段；只记控制身份则重启后仍无法唤醒已死 tool call，收益为零。

因此 collapse 到 CE 调用栈**不会**把隐式 PC 搬到"重启时重新推导恢复点"。重启后的 durable authority 分工已定：`PromptAuthority.AcceptedContinuationIds` 决定 Learn/Compile、QA.md 存在性决定 run 是否收尾、`SessionAssociationProjection` 决定 Teacher satellite 身份（EXEC-026，`docs/shape/execution.md:37`）。

**Teacher 侧保留并强化原文方向，给出目标签名骨架**：

```fsharp
type private TeacherCall =
    { Student: StudentRun
      Teacher: SessionId
      Returned: TaskCompletionSource<TeacherAnswer>
      Completion: TaskCompletionSource<Result<unit, string>> }

and private TeacherAnswer =
    { Answer: string
      ToolRun: ProviderRunIdentity }

type private PendingCompletionText =
    { Text: string
      ToolRun: ProviderRunIdentity }
```

`InvokeTeacher` 收敛为单一调用栈：

```fsharp
task {
    use call = beginTeacherCall ...
    do! sendTeacherPrompt call question

    let! returned = call.Returned.Task

    let! confirmed = call.Completion.Task

    return confirmed |> Result.map (fun () -> answerResult returned.Answer)
}
```

Host 边界退化为三个纯 resolve/查询，各自 fail-closed、不做分支推导：

```text
tool return event
→ 找到 call → QA append → 武装 pending text → resolve Returned；找不到 → Error

text.complete event
→ 查 pending text 命中则改写 output?text；未命中不动

reconciled TurnCompleted
→ turn payload == 固定 TeacherReturnCompletion 串 ? resolve Completion : sendTeacherNudge
```

关键差别写清：Teacher 分支判据从"`teacherCompletions` 有没有"变成"**这个 turn 的 payload 是不是那句固定 completion**"——turn 自带物理载荷，不是 registry presence。

**Student 侧改为分支只读 durable 事实**：

- Learn→Compile：读 `currentStudentRequestKind`（`StudentTeacherRuntime.fs:119-130`，源自 durable `AcceptedContinuationIds`，`Domain/PromptAuthority.fs:176`，由 `Journal/PromptAuthorityLedger.fs:138-145` fold NDJSON）。已经如此，不动。
- Compile 完成 vs Compile idle：改读 **QA.md 存在性** 替代 `tryFinalCompletion` presence。EXEC-026（`docs/shape/execution.md:53`）明文要求核对 StudentCompile attempt 且 QA 已不存在才 retire run；QA 删除发生在 `Return`（`:407`），原子、durable、fail-closed。
- 明写代价：`HandleTurn` 多一次 `qa.Path` + `existsSync`，Student turn 频率分钟级，开销无关紧要。

PromptAuthority 继续是 durable authority evidence；registry 继续存在作为**事件投递地址**。但 registry presence 不应决定 program counter。这才是真正符合 `ce.md` 最初那句话：

> **F# 调用栈就是流程栈。**

（在 Teacher 侧被完整实现；在 Student 侧被正确地拒绝——Student 的流程栈在 Host 的 turn 循环里，不在我们的进程里，durable authority 才是它的栈。）

可直接沿用的既有 capability 范式：`Session/Companion.fs:107-139`、`Tools/OneShotAgentTool.fs:141-189`、`Tools/FinalityController.fs:188-200`、`Journal/AgentJournal.fs:291`。

---

## registry 去留裁定

| registry | 裁定 | 依据 |
| --- | --- | --- |
| `runs` | 保留为物理 lifetime + 投递地址 | `ObserveChatMessage:289` / `ValidateTool:452` 是独立 Host hook，无 CE 栈可挂 |
| `teacherOwners` | 删除 | 纯 cache，`tryOwner:138-144` 已有 `SessionAssociationProjection.tryOwnerOf` durable 后备；删除后单一真理源 |
| `teacherCalls` | 保留为投递地址，语义降级 | 只用于"找到就 resolve / 找不到就 fail closed"，不再参与 branch；兼 EXEC-027 单飞声明（`:112`） |
| `teacherCompletions` | 删除 | 内容进 `TeacherCall.Returned` + CE 栈局部变量，`CompletionRun` stage bit 随之消失 |
| `studentFinalCompletions` | 降级为 `pendingFinalText` | 只留 `TextComplete` 需要的正文 + provider run，不再持 Call 反向引用、不再决定分支 |
| `skillMutations` | 保留 | write/edit gate（`:486-488`）累积、`validateTouchedSkills:170` 消费的观测证据，本来就不是 PC |

**结构性证明目标**（替代人工无罪论证）：重构后不存在任何函数同时 probe 两个 registry 的 presence 来选 effect branch——`HandleTurn` 只 probe `runs`（Student）或 `teacherCalls`（Teacher），`TextComplete` 只 probe pending-text 表。joint-branch 判据（`docs/how/dsl-structured-program.md:127-142`）在结构上不可能成立，不再依赖 `docs/proof/dsl-structured-program.md:66-90` 的人工分类兜底。这比现状"manual proof 给自己判无罪"强得多。

---

## 量化事实校正

- 原文"dsl ownership + ratchet 单测 65/65 pass"：源码事实为 `tests/unit/verify/dsl-ownership.test.mjs` 44 个顶层 test 块 + `tests/unit/verify/dsl-ownership-ratchet.test.mjs` 10 个 = **54 个**顶层 test 块（65 疑为含参数化子断言的运行时计数）。按源码事实计，注明差异来源。
- `scripts/checks/dsl-ownership-ratchet-baseline.json` 中 `Session/StudentTeacherRuntime.fs` 被冻结 `mutable-record-field: 8`（防回归阈值，非完全豁免）。实测当前该文件无 `mutable`/`ref` 字段声明，实际计数 0，远低于基线 8。本重构预期使该 baseline 计数保持下降/不上升，应同步收紧而非留存虚高基线。
- `StudentTeacherRuntime.fs` 在 `scripts/checks/dsl-ownership.mjs:122` 的 `HOST_BOUNDARY_OPEN_BASENAMES` 白名单内（DSL-010）。

---

## 风险清单与必须新增的测试

**风险**：
- `beginTeacherCall` 作用域退出注销若漏写，会把现状手工 `Remove`（`:359`）换成泄漏，需 `use` + 单元测试锁死。
- Student 改读 QA 存在性后，`qa.Path` 非法 segment 与 `existsSync` 抛异常必须收敛为 typed 分支，否则 `HandleTurn` 抛出会卡死 reconcile 的 `Running` 标志（`HostSignalBootstrap.fs:209-211` 已有同类告警）。
- nudge 判据改 payload 比对后，provider 若返回带前后空白的固定串会比对失败并退化为无限 nudge，需显式 normalize + 预算上界。EXEC-027（`docs/what/execution.md:90-118`，尤其 `:105`）要求预算耗尽后父调用失败；但现状 `sendTeacherNudge` / `sendCompile` 只校验 `QuiescencePermit`（`:227` / `:251`），未找到该预算实现——对比 `Session/EnforcerHost.fs:1344/1361`、`Session/DurableFallback.fs:79-83` 均用 `AgentPairCursor.DefaultAutoRecoveryBudget`（`Domain/AgentPairCursor.fs:79`）。这是本重构要顺带补的真实缺口，不是推断。
- `TextComplete` 两表合一/降级改动 Host `experimental.text.complete` 契约面，属 DSL-011 测试可见面，需 contract 级测试。

**必须新增测试**（现状仅 happy path：`tests/e2e/cases/student-teacher.test.mjs:151`；`tests/unit/student-teacher/` 下 6 个用例无一直接 import `StudentTeacherRuntime`，tool-loop/idle-return/restart 经外部可执行插件驱动；`docs/proof/execution.md:44-55` 无重启/取消/重复投递行）：
- teacher call 作用域泄漏（send 失败 / 并发第二调用 / dispose）
- `Returned` 与 `Completion` 重复投递幂等
- payload 不匹配 → nudge 且有界
- `CancelSession` 在两个 await 点各自到达
- 重启后 `AcceptedContinuationIds` 落在 Claimed-but-not-Accepted 窗口不误判 Learn
- QA 存在性作为完成判据的 fail-closed 路径

---

## 其他几个整改点，我目前没有再判红

Finality 这一轮看起来比之前健康很多。`FinalityController` 的 record-ready 已经用 `RecordReady | AwaitJournal | RecordUnavailable` 临时 decision + `AgentJournal.awaitChangeFrom` 递归等待，没有看到重新持久化 `ReviewRound/NextReviewerIndex` 之类程序位置；并发 `remaining/ref/results` 仍是函数内部 algorithm scratch，我仍判合法。

`TurnUnknown` 修复也合格。

Executor 的 deadline + Journal waiter 我现在也判合格：timer 只结束总 deadline，不负责推进业务状态。

DevOps behavioral e2e 也确实不只是 prompt regex：场景要求真实 executor/read/coder、RED→GREEN、文件内容最终变化，而且 DevOps 自己不能拿 write/edit。虽然 provider 是 strict scripted mock，它至少证明了 runtime/tool contract 能跑完整闭环，不再是纯提示词测试。

---

# 新版总评级

| 项目                        |  上次 |                      本次 |
| ------------------------- | --: | ----------------------: |
| DSL 全仓覆盖                  |  🔴 |               **🟢 已修** |
| Infrastructure/Journal 逃逸 |  🔴 |               **🟢 已修** |
| `TurnUnknown` 稳定态         |  🟠 |    **🟢 已 Clean Break** |
| Executor 100ms polling    |  🔴 |           **🟢 已改事件驱动** |
| DevOps 行为测试               |  🔴 |               **🟢 已补** |
| 第二 Manager idle E2E       |  🔴 |            **🟢 看实现已补** |
| EXEC_025 无界等待             |  🔴 |             **🟢 已有界化** |
| Finality LWR race         | 新发现 |            **🟢 看起来闭环** |
| 完整 E2E close gate         |   — |         **🔴 仍未满足冻结条件** |
| Student–Teacher 隐式状态机     |  🟠 | **🔴 REVISE：方向拆分为 Teacher=CE collapse / Student=durable evidence** |
| `CompletionRun` stage bit    |   — | **🔴 新增，原文漏掉，随 teacherCompletions 删除** |
| Teacher nudge 预算缺失        |   — | **🔴 新增，EXEC-027:105 未实现** |

### 最终裁决

这版已经不能再说"主要是表面修复"。**大量整改是真的，而且针对性很强。**

但如果你的问题仍然是：

> "现在能不能宣布 completed/ 已经彻底消灭状态机余孽？"

我的答案仍然是：

**不能。**

现在剩下的核心不再是显眼的 `RunState` / `CurrentStage` / bool 地狱，而是更高级的一种：

> **用若干真实 physical mailbox/registry 的 presence 组合，重新推导程序位置。**

Student–Teacher 当前就是最值得继续开刀的一处，且 Teacher 侧已收敛到可执行的具体方案（capability collapse + 固定 completion 载荷判据 + 结构性证明），Student 侧应改为读 durable 事实而非强行 collapse。

此外，`corrective/fix` 的完整 E2E gate 还差一次真正的全套执行；文件自己已经承认只跑了两条 targeted canary，因此在治理意义上也还不应完全封板。

**我的建议：下一刀不要再改静态 regex，直接按本报告的 Teacher capability-collapse + Student durable-evidence 方案落地 Student–Teacher；然后跑完整 `test:e2e`（最好直接 `check:release`）再封板。**
