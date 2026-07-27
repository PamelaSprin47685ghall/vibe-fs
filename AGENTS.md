# KISS Agent DSL 新架构 — 索引

| Field | Value |
| :--- | :--- |
| **Status** | 最终批准 · 新实现唯一蓝图 |
| **Scope** | OpenCode first；关闭官方 compaction；不兼容旧控制状态 |
| **核心** | 结构化程序 + 极小工具 DSL + 投影式伴随博客 + 异步 fork/join |

> 源码直接表达过程。`let!/do!/use!/while/尾递归` 负责控制流；运行时只保存真实资源、少量缓存和外部事实，不把程序计数器人工展开成 Stage/Phase/Lease/Owner/Generation。

## 零、设计精神

本文件不是"谁说了什么"的聊天记录筛选，而是对一年多工程探索中反复出现的**决策意图**的凝练。每条规则都应问：**这条规则替我们避免了哪种复杂性？**

### 0.1 结构化程序是本体论的，不是风格选择

Stage/Phase/Lease/Owner/Generation 本质上是把 `await` 点手动展开成表驱动的状态转换。语言运行时已经用调用栈、continuation、局部变量和 `CancellationToken` 表达了这些东西。再抄一份到业务层不会增加安全性，只会让代码跳转五层才能拼出真相。

- **正确**：`let! c = fork Coder p; let! r = joinAny (); return r`
- **错误**：`InsertRegistry(Stage.Waiting); Coordinator.Transition(Phase.Forked); LeaseManager.Acquire(Owner.Coder)`

### 0.2 事实来源拒绝二阶猜测

万象术不负责从碎片事件还原事实。OpenCode 宿主在产生每一段完整 assistant、每一次 provider retry、每一个 abort 的源码位置**已经知道完整答案**。万象术只应在那里（插件 Hook 或 SDK API 返回时）读取完整 typed 事实。

- **SSE 是碎片**：`message.updated` 在同一个 message 上可能触发多次；`part.delta` 只反映增量；`session.idle` 不携带完整上下文。
- **Reconcile 是策略**：idle 只是"可能变脏了"的信号，从 API 重读完整消息才是事实。
- **Unknown ≠ Empty**：API 还没追上事件时，等下一轮信号。不要自己发明"空输出"论。

### 0.3 LLM 前缀缓存不是可牺牲的优化

Provider KV-cache 匹配的是逐字节 token 前缀。模型输入中最早出现的消息一旦内容变化，整个缓存失效。这不是"如果时间允许"，而是**每失效一次就损失数十秒 prefix 计算时间，直接影响用户往返体验**。

- B 合成消息必须使用 `ActivePrefixEpoch.FrozenB`（冻结快照），而不是 `LatestB`（Y 最新累积）。
- 只有上下文阈值到达时才 epoch 切换——这是唯一不可避免的冷边界。
- 平常回合：X 请求前缀保持逐字节不变。Y 的 LatestB 可以继续增长，但 X 不受影响。

### 0.4 极小模型 DSL 防止 Manager 越权

Manager 只有 `fork/join/list` 不是为了让 Manager "更专业"，而是为了**防止它钻进文件细节而忽略协调责任**。同样，Orchestrator 只有 `fork/join` 是为了让它只调兵不亲征。工具权限是静态角色表，不由 prompt 劝阻管理。

### 0.5 局部状态可接受，全局状态机不可接受

允许的局部状态：正在运行的 `Task`、完成 `Channel`、累计失败计数 `int`、模型选择 `A|B`。这些是**真实资源的引用或简单计数器**。

禁止的全局状态：`ReviewPhase`、`FallbackStage`、`NudgeLease`、`JoinOwner`。这些是**人工展开的程序计数器**。

区别在于：前者直接对应物理事实（有没有进程在跑、信箱里有没有信、总共失败了几次），后者只回答"代码执行到了第几步"。

### 0.6 A/A/B/B 不是状态机，是计数器 + 映射表

Fallback 不需要状态图。它只需要：

```text
累加失败数 F
当前侧 S ∈ {A, B}
映射：F<2→A, F=2→切B, F<4→B, F≥4→Dead
```

这就是全部。`F` 和 `S` 加上 `match` 就是 Fallback。没有任何 Stage/Phase。

### 0.7 Review 双 PERFECT 是避免假阳性确认

一次 PERFECT 可能是模型随口的肯定。要求两次不同 ToolCallId 的 PERFECT 增加了虚假确认的成本，杜绝了"看起来对了"的风险。Git tree hash 绑定确保审查针对的是当前实际代码状态。Post-rebase 必须重新审查，因为 ancestry 变化可能改变合并后的语义。

### 0.8 不修改 OpenCode 本体是生存策略

每次 fork 增加维护负担；上游可能拒绝 patch；用户升级 OpenCode 时会丢失功能。万象术只在现有 Hook 和 SDK API 边界内工作。对于缺少的 `turn.finished` 和 `retry.decide` Hook，用"signal+reconcile"模式替代：事件只唤醒，完整状态从 API 读取。

### 0.9 所有决策的最终检验

当你添加任何新模块、字段或控制流时，问：

1. 这是物理世界的事实（文件、进程、session、Git tree、模型输出），还是"程序下一步去哪"的信息？
2. 能否用 `let!/do!/use!/match/while/tailrec` 直接写出来？
3. 这条规则是在减少总系统熵（删除旧路径、减少概念数量），还是在增加一层新的编排？

如果答案指向"用一段程序就能写清楚但你把它拆成了三个文件加五个 DU"，回退重来。

## 一、卷表

|卷|主题|
|---|---|
|`KISS-N00.md`|第一原理：两层 DSL，删除状态机平台|
|`KISS-N01.md`|Structured Program 内核与 computation expression 语法糖|
|`KISS-N02.md`|Projection / Companion Blogger / A、B 工作记录|
|`KISS-N03.md`|异步 `fork / join / list` 与完成邮箱|
|`KISS-N04.md`|角色、能力矩阵与同步局部子程序|
|`KISS-N05.md`|Executor / Process / Output Summary / PTY|
|`KISS-N06.md`|每 Session 四次失败的 A/B 角 Fallback|
|`KISS-N07.md`|Manager Guard、Reviewer Guard、双 PERFECT|
|`KISS-N08.md`|Orchestrator / Worktree / Rebase / Review / FF|
|`KISS-N09.md`|OpenCode Host Adapter、投影管线、工具 Schema|
|`KISS-N10.md`|保姆式实施、测试、迁移与删除清单|

## 二、已经冻结的产品语义

1. OpenCode 官方 compaction 关闭。
2. 每个有伴随的 Session `X` 拥有廉价 Blogger Session `Y`。
3. `A` = X 的正式模型输出，不含 reasoning；`B` = Y 当前投影中所有正式 assistant 输出，不含 Y 输入和 reasoning。
4. X 的 ProjectedInputTokens + ReservedOutputTokens 超过 ContextLimit 且 BlogBase coverage proof 通过后，投影层用 B 等价替换已被 B 覆盖的前缀；此后每次投影继续替换。Cutoff 必须位于完整 semantic turn 边界；CoveredPrefixDigest 必须在投影前重新验证。Estimator 不可用时不切换 epoch。
5. Delta 在 canonical JSON 投影层计算；Y 忙时不打断、不排插件队列、不推进 delta 基线，下一次自然包含跳过期间的全部变化。
6. Y 自身接近上限时，把旧 B 作为唯一正文输入重投影；Y 新输出 B' 替代旧 B。
7. Manager 无 read/write/edit/grep/glob 等普通工具；只有 `fork / join / list`。
8. `fork(role, prompt)` 创建异步子代理；`fork(existingId, prompt)` 是 fire-and-forget nudge/continue。Busy existing agent 不创建新 RunId、不安装新 listener、不创建新 completion；nudge 归属于当前 active Run。**原因**：如果 busy nudge 替换 active RunId，原始 fork 对应的 completion 会被覆盖而永久丢失。nudge 只是 "在当前运行的尾部追加提醒"，其结果属于同一次 completion。Nudge 成功 = Host 已确认接受 prompt。Busy→idle 竞态以 Host AcceptPrompt 返回的 run identity 为归属依据。若 Host 不支持 busy append，返回 BusyNudgeUnsupported。
9. `join()` 等任意一个完成项；不指定对象。每个 RunIdentity 对应 single-assignment completion cell。Terminal/SendFailure/Cancel 竞争 TrySetResult，首个成功者唯一生效。join 消费后永久删除 completed handle。
10. `list()` 统一显示 Agent 与 PTY，但内部资源实现保持独立。
11. Inspector 同步调用 Executor Tool；Coder 每次同步 Inspector 调用创建一次性 Inspector Session，并可并行。
12. Executor Agent 只负责命令输出摘要；无工具、无伴随。Executor Tool 负责真实进程。
13. `3 × estimated_running_secs` 是进程唯一时限；无其他 timeout 层。模型允许用巨大 estimate 主动申请巨大预算。
14. `actual_output_bytes > 3 × estimated_output_bytes` 时触发 spool；摘要按 200KB 块做在线 ripple-carry reduce（fan-in=8，按 chunk index 排序，Executor ID 由 processId+level+range hash 生成），不积存全部 map summary。
15. `estimated_mem_usage=large` 全 OpenCode 进程同时最多一个；medium 不限并发。
16. Fallback：retry event #1 → A 重试；#2 → 永久切 B；#3 → B 重试；#4 → Session 真死。成功不把 Session 切回 A。唯一写 durable fallback 事实的入口是 `session.status=retry`；空/XML-only 不进入 A/B 计数。`currentUserMessageId` 必须是 Host run root user message，不包括插件 synthetic 消息。
17. Review：REVISE 一次立即生效；PERFECT 必须来自不同 ProviderRunIdentity 且第二次的 user 输入包含第一次后的确认请求。每个审查屏障要求两个不同的 ProviderRunIdentity。
18. ReviewGuard 同时守 Manager 结束和 Reviewer 未给出有效 verdict。
19. PTY 仍复用 `fork` 表面，但 signal 使用结构化 enum，不使用魔法字符串。PTY completion 只由 backend `onExit` 触发；Signal/Close 不提前完成。
20. Orchestrator 只有 `fork / join`；fork Manager 自动建 worktree、进入 ReviewGuard；发布前 rebase、复审、串行 ff。Rebase 后的审查必须是全新的双 PERFECT（两个新 ToolCallId），不得复用 rebase 前的确认。
21. 用户向 Orchestrator 发消息时，目标工作区 dirty 则拒绝。
22. 万象阵、todowrite SSOT、select_methodology、通用 nudge、fuzzy 工具与同步 subagent 伪工具全部删除。
23. **B 前缀缓存保护**：`CurrentB` 拆分为 `LatestB`（Y 最新工作记忆）与 `ActivePrefixEpoch`（冻结的 B 快照）。Epoch 切换算法：ProjectedInputTokens + ReservedOutputTokens > ContextLimit。**`companion-b-head` 合成消息在两次 epoch 切换之间必须保持逐字节不变。** Synthetic ID = hash(sessionId + epochId + semanticKind)，禁止随机值。
24. **SSE 只是唤醒信号**：业务事实不从碎片事件拼装。`session.status=idle` 建立 Dirty latch，触发 single-flight reconcile；`session.status=retry` 是唯一 durable fallback 入口。`message.updated`/`message.part.updated`/`part.delta`/`session.updated` 被直接丢弃。Unknown 协议：单次 idle 后最多 3 次因果重读，仍 Unknown 则保持 Dirty 等下一信号。
25. **Host 事实通过 SDK API 读取**：completion、ReviewGuard、continuation、abort 都先从 reconcile 得到完整的 `ReconciledTurn` 再决策。Unknown（API 尚未反映当前 Run 的 assistant）= 不产生副作用，保持 PendingRun。
26. **不修改 OpenCode 本体**：生产功能只在现有插件 Hook（`chat.message`/`experimental.chat.messages.transform`/`tool.execute.before`/`after`/`event`）和 SDK API 边界内工作。
27. **稳定性门禁**：scenario-local 因果 Watchdog 超时 2 秒；只认因果进展。Watchdog deadline > 被测动作 deadline + cleanup allowance。Event-stagger 规则：第一个 canary 立即启动，canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）。Release gate 恰好运行 3 轮。最多重复 3 次。
28. **Provider-visible projection**：缓存比较只使用真正进入模型的字段（role/text/reasoning/tool call/result），排除 timestamp/cost/usage/runtimeId 等非模型 metadata。
29. **Fallback identity**：`sessionId + currentUserMessageId + providerAttempt`。`currentUserMessageId` 是 Host run root user message。Single-flight `retry` 事件到达后 append `FallbackFailureRecorded`。空/XML-only terminal 触发 InteractionRepairIdentity 去重（最多一次）。

## 三、总形状

```text
User → Orchestrator DSL
          └─ fork ManagerJob ── worktree ── Manager DSL
                                  ├─ fork Coder
                                  ├─ fork Inspector
                                  ├─ fork Browser
                                  ├─ fork Meditator
                                  ├─ fork Reviewer
                                  ├─ join any completion
                                  └─ list live handles

Any companion-enabled Session X
    └─ projection delta → cheap Blogger Y → B record
       X context projection: B replaces covered prefix

Coder
    └─ one-shot Inspector(s)
         └─ Executor Tool
              └─ Process / optional Executor Agent summary
```

## 四、最重要的代码审查问题

看到任何新增模块或字段时，只问：

1. 这是用户/宿主/Git/进程真实存在的事实，还是“程序运行到哪一步”？
2. 能否放回 `let! / do! / use! / match / while / tail recursion`？
3. 能否由真实 `Task / Process / Session / Git tree` 直接派生，而无需缓存状态？
4. 若必须有缓存，它是否只是一个值或一个正在运行的 Task，而不是第二套调度器？

若答案指向程序计数器，删除字段，重写为结构化程序。


---

# KISS-N00 — Structured Agent Program 第一原理

[NORMATIVE] 本卷是新架构总纲。旧 KISS 中“结构化程序替代手写状态机”的原则保留；旧 Todo、Review State、Prompt Lease、Subsession Actor、Squad DAG 等具体实现不继承。

---

## 一、断根：不要把 await 展平成治理平台

一个真实流程本来是：

```text
fork 两个调查者
→ join 先完成者
→ 根据结果 fork coder
→ join coder
→ fork reviewer
→ reviewer REVISE 则继续 coder
→ reviewer 连续两次 PERFECT
→ finish
```

[FORBIDDEN] 把它改写成：

```text
ManagerStage
ChildPhase
PendingJoinOwner
ReviewLease
VerdictGeneration
ContinuationRegistry
NudgeCoordinator
```

这些名词没有增加任何用户事实，只是把语言运行时原本维护的 continuation、局部变量、调用栈和 await 点搬进业务数据。

[NORMATIVE] 新实现必须让源码重新长成顺序程序：

```fsharp
agent {
    let! inspector = fork Inspector "调查根因"
    let! coder = fork Coder "实现修复"
    let! first = joinAny ()
    do! react first
    let! review = reviewUntilConfirmedPerfect ()
    return review
}
```

真实可靠性放进动词内部：`fork` 完成注册，`joinAny` 完成邮箱消费，`use!` 完成资源释放，`reviewUntilConfirmedPerfect` 完成确认循环。调用者不拼装状态机。

---

## 二、两层 DSL

### 2.1 模型可见 DSL

模型只看极小工具语言：

```text
Manager:      fork / join / list
Orchestrator: fork / join
Reviewer:     verdict(PERFECT|REVISE) + 角色允许的只读工具
```

这是一门动态 Agent DSL。它不是工作流 JSON，也不是持久化 AST。每次工具调用直接映射为一个真实动词。

### 2.2 实现者 DSL

F# 源码使用 computation expression：

```text
agent { ... }
companion { ... }
process { ... }
review { ... }
orchestrator { ... }
```

它们都是同一个可执行闭包内核的类型别名或薄 Builder，不是五套解释器。

[NORMATIVE]

- `let!`：等待一个领域动词并取得值。
- `do!`：执行一个无返回值领域动词。
- `use!`：获取并异步释放真实资源。
- `match`：显式处理领域结果。
- `while`：条件循环；条件每轮重新读取真实投影。
- 尾递归：找到即停、有界重试、确认循环。
- `parallel`：对独立动作并发，返回全部结果。

[FORBIDDEN]

- Flow AST。
- 动态 Stage Registry。
- continuation 序列化。
- 通用 Workflow Engine。
- 为了“可观察”而持久化程序计数器。

---

## 三、什么可以有局部状态

KISS 不等于禁止变量。禁止的是把控制位置伪装成领域。

### 3.1 允许

|值|原因|
|---|---|
|`Task<AgentCompletion>`|真实正在运行的异步工作|
|`Channel<Completion>`|真实完成邮箱|
|`Process` / PTY handle|真实 OS 资源|
|`BlogText`|真实 B 版缓存|
|`BlogBaseJson`|JSON delta 的真实基线|
|`PrefixReplacementEnabled`|投影策略已启用的配置事实|
|`ModelSide = A | B`|当前 Session 已永久选择的模型角色|
|`FailureCount`|每 Session 真实失败预算消耗|
|`perfectConfirmations` 局部变量|本次 reviewer 确认函数的递归参数|

### 3.2 禁止

|字段|原因|
|---|---|
|`CurrentStage`|程序计数器|
|`NextAction`|第二调度器|
|`JoinOwner`|Channel 已表达消费者语义|
|`ReviewPhase`|递归和 verdict 工具已表达|
|`FallbackPhase`|一个函数和两个值足够|
|`NudgeLease`|nudge 是直接 prompt，不是领域资源|
|`CompactionGeneration`|投影替换由当前值直接计算|
|`SquadWaveState`|Orchestrator 动态 fork/join，不维护固定 DAG|

---

## 四、源码应呈现的五条主程序

### 4.1 Companion

```text
canonical projection
→ JSON delta against last successful blogger base
→ blogger free ? start one step : skip
→ maybe remember prefix replacement
→ emit X projection
```

### 4.2 Manager

```text
LLM turn
→ fork / nudge / join / list
→ assistant terminal
→ ManagerGuard accepts ? finish : append guard prompt and continue
```

### 4.3 Reviewer

```text
review files
→ verdict(REVISE) : immediate
→ verdict(PERFECT) : ask confirmation
→ second PERFECT : confirmed
→ terminal without verdict : append reviewer guard prompt and continue
```

### 4.4 Process

```text
spawn with pumps installed
→ run until exit or 3× estimated seconds
→ timeout: SIGKILL process tree
→ drain
→ if actual > 3× estimate: summarize 200KB chunks
→ return
```

### 4.5 Orchestrator

```text
clean gate
→ fork manager worktrees in parallel
→ join completions
→ for each publish candidate under serial integration gate:
     rebase latest target
     resolve conflict through same manager
     review again
     ff
```

---

## 五、恢复与持久化边界

新架构不复活旧 NDJSON 控制平台。

[NORMATIVE]

- OpenCode Session transcript 是对话事实源。
- X 与 Y 的伴随关系优先写入宿主 Session metadata；宿主不支持时才用一个极小 association store。
- B 可从 Y 当前投影的 assistant 正文重建；缓存丢失只影响性能，不改变事实。
- Git commit/tree/worktree 是代码事实源。
- 正在运行的 Process、PTY、LLM 请求是进程资源；进程退出后不伪装成仍可恢复的 Running State。
- 活跃 fork 列表是运行期资源表，不写成事件溯源平台。
- Per-runtime NDJSON 只保存跨进程仍成立的领域事实；不保存调用栈、程序计数器、未完成 Task。
- 崩溃后通过 Boot Fold 恢复最新的领域事实，然后用普通程序逻辑决定下一步。不是恢复暂停的协程。

## 六、Host 边界：事件是信号，不是数据

### 6.0 为什么？

碎片事件有以下固有缺陷：

1. **一个完整 turn 被拆成多次事件**：`message.updated` 在 streaming 期间可能触发数十次；`part.delta` 只反映增量；`session.type` 与 `session.status` 可能以任意顺序到达。
2. **拼装是 CPU 密集且不确定的**：每收到一条碎片都要更新内存中的 parts 聚合器、比较 message ID、检查是否 final。并且即使全部拼好，也可能因竞态而拼错。
3. **宿主已经在产生完整事实时知道答案**：Streaming 结束后，Host 已经拥有完整的 assistant parts、finish reason、outcome。万象术不应等碎片再来还原它已经知道的东西。
4. **Unknown ≠ Empty**：API 查询尚未反映最新状态时，正确行为是等待下一轮 idle 信号，不是自己编造一个"空输出"论。

所以：事件只应做**唤醒信号**——"某个 session 可能变脏了，请去 API 读取完整状态"。业务事实由 Reconciler 从 SDK API 读取。

### 6.1 分层

```text
碎片事件（message.updated / part.delta / session.updated）
    ↓ 丢弃
粗粒度信号（session.status idle/retry, session.deleted）
    ↓ single-flight
Reconciler 从 SDK 读取完整消息
    ↓
ReconciledTurn（完整 parts / outcome / role / model）
    ↓
纯策略（completion / ReviewGuard / continuation / abort）
```

[NORMATIVE]

- 唯一允许进入生产业务层的事件类型：`session.status`（idle/retry）、`session.deleted`。
- `message.part.delta`、`message.part.updated`、`message.updated`、`session.updated`、`session.diff` 在最早边界丢弃。
- `idle` 不直接代表 terminal 完成，只触发一次 connected reconcile loop。
- `retry` 是唯一写持久 fallback 的入口。
- Unknown（API 尚未显示当前 Run 的 assistant）不产生副作用，不判为 Empty/Failed。
- 同一事件重复到达不重复消费：`Reconciler` 是 single-flight，`Fallback` 由 stable identity 去重。

Same Session 的 event 来源二选一（插件 `event` Hook 或 global SSE），不双投递。

## 七、LLM 前缀缓存保护

缓存是重要的，不是可以随意破坏的优化。

[NORMATIVE]

- `LatestB`（Y 最新工作记忆）与 `ActivePrefixEpoch`（X 当前使用的冻结 B 快照）分离。
- 平常回合 LatestB 累积，X 请求前缀保持逐字节不变。
- 只有达到下一次上下文阈值时才 epoch 切换，产生一次必要冷边界。
- `caps-head`、system、tools 在 replacement 区域外固定不变。
- Provider-visible projection 只包含真正进入模型的字段（role/text/reasoning/tool call ID/name/arguments/result），排除 timestamp/cost/usage/runtimeId 等非模型 metadata。
- Transform Hook 必须是幂等的：检测已有 `caps-head` 则不再重复注入。
- 同一模型、同一 epoch 下：tools(N+1) == tools(N)、system(N+1) == system(N)、messages(N) 是 messages(N+1) 的逐字节前缀。

### companion-b-head 不可变性

`companion-b-head` synthetic message 在两次 epoch 切换之间**必须保持逐字节不变**。不能因为 LatestB 累积、Blogger 新段落或任何其他原因修改它的 content。

```fsharp
// 正确：FrozenB 是 epoch 冻结时的快照，epoch 切换前不变
let head = injectEpochHead epoch.FrozenB

// 错误：LatestB 仍在增长，每轮 B head 内容都不同，破坏缓存
let head = injectBHead memory.LatestB
```

Epoch 切换是唯一可预期冷边界。切换后新 FrozenB 再次冻结。

[FORBIDDEN]

- 每次 blogger 成功后修改 X 最早消息的正文。
- 把 LatestB 直接放入 X 的 active prefix。
- 重建 Blogger 后不发送 full reset frame 就使用旧 projection baseline。
- 使用非幂等 transform Hook 导致重复注入 caps/prepend。
- 使用 Host runtimeId/timestamp 等非模型字段参与 canonical equality 比较。
- 同一 session 同时导出两个非幂等的 transform Hook 实现。

## 八、不修改 OpenCode 本体

[NORMATIVE] 生产功能只在现有插件 Hook 和 SDK API 边界内工作。宿主不改，不要求增加新 Hook。

- `chat.message` — 确认新 turn 被接受，清理上一轮 pending。
- `experimental.chat.messages.transform` — Companion 唯一输入。
- `tool.definition` / `tool.execute.before` / `tool.execute.after` — 权限与结果处理。
- `experimental.session.compacting` / `experimental.compaction.autocontinue` — 控制 compaction。
- `event` — 接收极少数粗粒度信号（idle/retry/deleted）。
- SDK `client.session.create` / `session.messages` / `prompt_async` — 主动创建、读取、发送。

不依赖的边界：

- 无 `turn.finished` Hook。
- 无 `retry.decide` Hook。
- 不监听 `message.updated` / `part.updated` / `part.delta`。
- 不依赖 events.listen 或 /global/event 的完整 payload 结构。

Adapter 层负责：把现有 Hook 参数转成 typed input，调用领域层 Flow。

---

## 九、命名原则

允许同一用户表面出现 `executor` 角色与 `executor` 工具，因为语境清楚；实现中必须由类型命名空间区分：

```fsharp
type AgentRole = ... | Executor
module Tool =
    let executor : ToolDefinition = ...
```

不得为了避免同名引入 Translator、Governor、Broker 等无价值中间层。

---

## 十、终极验收

打开生产主流程文件，读者应在一分钟内看见：

- 谁 fork 谁；
- 谁 join；
- 哪些资源 `use!`；
- 哪些失败递归重试；
- Reviewer 如何确认；
- Orchestrator 如何发布。

若必须跳转五个 Registry 才知道下一步，重构失败。


---

# KISS-N01 — Structured Program 内核与语法糖

本卷定义真实可编译形状。示例使用 F# / Fable；具体 Task/Promise 适配只在 Host Adapter。

---

## 一、唯一内核 [NORMATIVE]

```fsharp
type Flow<'ctx, 'error, 'a> =
    private
    | Flow of ('ctx -> CancellationToken -> Task<Result<'a, 'error>>)
```

它是闭包，不是 AST。

```fsharp
module Flow =
    val run:
        'ctx ->
        CancellationToken ->
        Flow<'ctx, 'error, 'a> ->
        Task<Result<'a, 'error>>

    val fail: 'error -> Flow<'ctx, 'error, 'a>

    val liftTask:
        ('ctx -> CancellationToken -> Task<'a>) ->
        Flow<'ctx, 'error, 'a>
```

`Bind` 只执行左侧、错误短路、成功传值。它不自动重试、不写日志、不刷新投影、不解释错误。

---

## 二、Builder [NORMATIVE]

```fsharp
type FlowBuilder<'ctx, 'error>() =
    member Return: 'a -> Flow<'ctx, 'error, 'a>
    member ReturnFrom: Flow<'ctx, 'error, 'a> -> Flow<'ctx, 'error, 'a>
    member Bind:
        Flow<'ctx, 'error, 'a> *
        ('a -> Flow<'ctx, 'error, 'b>) ->
        Flow<'ctx, 'error, 'b>
    member Zero: unit -> Flow<'ctx, 'error, unit>
    member Delay: (unit -> Flow<'ctx, 'error, 'a>) -> Flow<'ctx, 'error, 'a>
    member Combine:
        Flow<'ctx, 'error, unit> *
        Flow<'ctx, 'error, 'a> ->
        Flow<'ctx, 'error, 'a>
    member TryWith: ...
    member TryFinally: ...
    member Using:
        'resource * ('resource -> Flow<'ctx, 'error, 'a>) ->
        Flow<'ctx, 'error, 'a>
        when 'resource :> IAsyncDisposable
    member While: ...
    member For: ...
```

[NORMATIVE]

- `Using` 必须 await `DisposeAsync`。
- 通用 Flow 不吞 `OperationCanceledException`；领域 run 再映射。
- 可预见分支使用成功通道 DU，不要把所有分支都塞进 `'error`。
- `for` 体不承担非局部 early return；找到即停使用尾递归。

---

## 三、五个领域别名，不是五套内核

```fsharp
type AgentFlow<'a> = Flow<AgentContext, AgentError, 'a>
type CompanionFlow<'a> = Flow<CompanionContext, CompanionError, 'a>
type ProcessFlow<'a> = Flow<ProcessContext, ProcessError, 'a>
type ReviewFlow<'a> = Flow<ReviewContext, ReviewError, 'a>
type OrchestratorFlow<'a> = Flow<OrchestratorContext, OrchestratorError, 'a>

let agent = FlowBuilder<AgentContext, AgentError>()
let companion = FlowBuilder<CompanionContext, CompanionError>()
let process = FlowBuilder<ProcessContext, ProcessError>()
let review = FlowBuilder<ReviewContext, ReviewError>()
let orchestrator = FlowBuilder<OrchestratorContext, OrchestratorError>()
```

领域差异在 Context、Error 和动词模块，不在 Builder。

---

## 四、结构化组合子

### 4.1 并行

```fsharp
val parallel:
    Flow<'ctx, 'error, 'a> list ->
    Flow<'ctx, 'error, 'a list>
```

[NORMATIVE]

- 每个动作共享只读 Context，禁止共享可变请求对象。
- 父取消传播。
- 全部 Task 被观察，不产生无人收割异常。
- 结果顺序与输入顺序一致；完成事件若需要按物理先后，使用 `joinAny`，不要复用 `parallel`。

Coder 并行 Inspector 示例：

```fsharp
coder {
    let! findings =
        parallel [
            inspectOnce "定位写路径"
            inspectOnce "定位测试缺口"
            inspectOnce "检查宿主投影 hook"
        ]

    return! implement findings
}
```

### 4.2 尾递归

Fallback、双 PERFECT、Reviewer 无 verdict 继续都写成局部递归：

```fsharp
let rec confirmPerfect confirmations =
    review {
        match! nextVerdictOrTerminal () with
        | Revise report -> return Revision report
        | Perfect when confirmations = 0 ->
            do! requestSecondConfirmation ()
            return! confirmPerfect 1
        | Perfect ->
            return ConfirmedPerfect
        | TerminalWithoutVerdict ->
            do! nudgeReviewer ()
            return! confirmPerfect confirmations
    }
```

这段代码本身就是控制流，不需要 `ReviewStage`。

### 4.3 资源作用域

```fsharp
process {
    use! child = spawn request
    return! runToCompletion child
}
```

```fsharp
orchestrator {
    use! worktree = createWorktree target
    let! result = runManager worktree
    return! publish worktree result
}
```

资源释放由 `use!` 覆盖成功、领域失败、异常、取消。

---

## 五、Script 语法糖

Context 不应暴露胖对象。为每个流程提供窄 Script：

```fsharp
type AgentScript =
    { Fork: ForkRequest -> AgentFlow<ForkAck>
      JoinAny: unit -> AgentFlow<Completion>
      List: ListFilter -> AgentFlow<HandleView list>
      PromptSelf: string -> AgentFlow<unit>
      CurrentTree: unit -> AgentFlow<GitTreeHash> }
```

调用者写：

```fsharp
agent {
    let! child = s.Fork (NewAgent(Coder, prompt))
    let! completion = s.JoinAny()
    return completion
}
```

而不是：

```fsharp
agent {
    do! s.Registry.Insert(...)
    do! s.Actor.Dispatch(...)
    do! s.Stage.SetWaiting(...)
    return! s.EventBus.Wait(...)
}
```

---

## 六、错误模型

建议最小 DU：

```fsharp
type AgentError =
    | HostFailure of string
    | SessionDead of string
    | InvalidFork of string
    | ParentCancelled

type ProcessError =
    | SpawnFailed of string
    | TimedOut of ProcessDiagnostics
    | Killed of ProcessDiagnostics
    | PumpFailed of ProcessDiagnostics

type ReviewError =
    | ReviewerFailed of string
    | ParentCancelled

type OrchestratorError =
    | DirtyWorkspace of string list
    | RebaseFailed of string
    | PublishFailed of string
```

普通命令非零退出码是值，不默认 ProcessError。

---

## 七、禁止扩建 CE

[FORBIDDEN]

- 自定义 `goto`。
- `BreakSignal` AST。
- `Stage` 解释器。
- 为每种工具建一个新 Builder。
- 为了观察流程而让每个 `Bind` 发事件。

观察通过正常日志与真实资源快照完成，不污染控制结构。


---

# KISS-N02 — Projection 与 Companion Blogger

本卷定义 X/Y、A/B、JSON delta、忙时跳过、X 前缀替换与 Y 自压缩。

---

## 一、术语 [NORMATIVE]

- `X`：主 Session。
- `Y`：X 的伴随 Blogger Session，使用便宜模型，无工具。
- `A(X)`：X 的 assistant 正式输出正文，不含 reasoning。
- `B(X)`：Y 当前有效投影中所有 assistant 正式输出正文，不含 Y 的输入和 reasoning。
- `CanonicalProjection`：Host transcript 经纯规范化后的 JSON。
- `BlogBase`：最近一次成功被 Y 消化的 CanonicalProjection。
- `Delta`：`JsonDelta(BlogBase, CurrentCanonicalProjection)`。
- `PrefixReplacementEnabled`：X 已进入 B 前缀替换模式。

B 是摘要缓存，不是 Session 状态机。

---

## 二、消息协议

### 2.1 Y 初始系统消息

```text
You are the blogger of a coding agent session.
See below for the session content.
Write dense, factual work-log prose.
Do not call tools.
Do not reproduce raw code or stream of consciousness.
```

新建 X 的 Y 时，再提供父 Session 的 B 作为背景；若无 B，省略。

### 2.2 普通增量轮

Y 收到两个 user message：

```yaml
kind: session_delta
messages:
  - ...
```

```text
You are the blogger of a coding agent session.
Write one new paragraph for these delta messages.
Avoid raw code or stream of consciousness and maximize information density.
```

Y 的本轮 assistant 正文直接追加进 B。

### 2.3 Y 自压缩轮

Y 的投影接近自身上限时，旧 Y 上下文前缀被替换为一条 user message：

```yaml
kind: existing_blog
content: |-
  <old B>
```

随后请求：

```text
Rewrite the supplied existing blog as a standalone, denser work record.
Preserve concrete decisions, results, failures, paths and unresolved work.
```

新 assistant 输出是 `B'`。旧 B 此时是 Y 的输入，不再是 Y 的输出，所以 `B(X) := B'`，无需额外删除算法。

---

## 三、Canonical JSON 投影

[NORMATIVE] Delta 不基于事件流，不做文本 diff。每次 Host 即将构造模型输入时：

```fsharp
let canonical = CanonicalProjection.ofHostInput hostInput
let delta = JsonDelta.between memory.BlogBase canonical
```

Canonical JSON 只保留 Blogger 需要的语义：

```fsharp
type CanonicalMessage =
    { MessageId: string
      Role: string
      Kind: string
      Content: JsonValue
      ToolName: string option
      ToolCallId: string option }
```

包括：

- 用户输入；
- 工具输入和输出；
- assistant 正文；
- Host 可公开提供的 reasoning；
- 错误和退出结果。

不包括：

- UI 临时字段；
- token 计数噪音；
- Blogger 自己的 synthetic replacement message；
- 与语义无关的时间抖动字段。

JSON 规范化必须稳定：属性排序、空字段策略、字符串换行策略固定。这样结构相同就不会产生伪 delta。

---

## 四、忙时跳过自然实现

```fsharp
type CompanionMemory =
    { mutable LatestB: string                         // Y 最新累积工作记忆
      mutable BlogBase: JsonValue                     // 最近被 Y 消化的 canonical projection
      mutable InFlight: Task<BlogStepResult> option

      // X 前缀缓存保护：ActivePrefixEpoch 冻结后不随 LatestB 改变
      mutable PrefixReplacementEnabled: bool
      mutable ActivePrefixEpoch: ActiveEpoch option }  // 冻结的 B 快照，epoch 切换时才变

type ActiveEpoch =
    { EpochId: string
      FrozenB: string
      CutoffMessageId: string
      CoveredPrefixDigest: string }
```

### Cutoff 证明算法

[NORMATIVE]

```text
Cutoff 只能位于完整 semantic turn 边界。
CoveredPrefixDigest = hash(provider-visible messages[0..cutoffExclusive]).
投影前必须重新计算该 digest。
不匹配时禁止替换，保留原始上下文。

Semantic turn 边界定义：
  - 用户输入 + 对应的完整 assistant 输出（含所有 tool call/result 来回）
  - 完整的 user→assistant 轮次，不得在 tool call pending 状态截断
  - pending→completed 的 tool part 更新不破坏已 seal 的前缀
  - message ID 相同但 content 变化（retry/revert/undo）→ 原 cutoff 无效
```

[NORMATIVE] 不建立 Blogger 队列。LatestB 累积不影响 X 的 active prefix。

```fsharp
let offerDelta current =
    companion {
        match memory.InFlight with
        | Some task when not task.IsCompleted ->
            // 不打断、不排队、不推进 BlogBase
            // X 的 active prefix 不受影响
            return ()

        | _ ->
            let delta = JsonDelta.between memory.BlogBase current
            if JsonDelta.isEmpty delta then
                return ()
            else
                let baseAfterSuccess = current
                let task =
                    runBloggerStep memory.LatestB delta
                    |> Task.bind (fun paragraph -> task {
                        // LatestB 累积，但不修改 X 的 active prefix
                        memory.LatestB <- memory.LatestB + "\n\n" + paragraph
                        memory.BlogBase <- baseAfterSuccess
                    })
                memory.InFlight <- Some task
                observeFailureWithoutQueue task
                return ()
    }```
```

[NORMATIVE] 唯一原子 durable fact：

```text
CompanionAdvanced {
  sessionId
  bloggerId
  previousBaseDigest
  newBaseCanonical
  completeLatestB
}
```

只有 append `CompanionAdvanced` 成功后才更新内存。
重启只 fold journal；宿主 metadata 只能是可重建索引，不能和 journal 平级。

Crash window 恢复：
- LatestB 已写，BlogBase 未写 → 以 journal 为准，回退 BlogBase
- BlogBase 已写，LatestB 未写 → 以 journal 为准，重算 LatestB
- association 已写，B 未写 → 删除 association，视为 Blogger 未创建

---

## 五、X 的 B 前缀替换（缓存保护）

### 5.0 为什么需要 Epoch 分离？

Provider KV-cache 匹配的是**逐字节 token 前缀**。模型输入中最早出现的消息内容一旦变化（即使身份 ID 相同），从该消息开始所有后续消息的 KV-cache 全部失效。

旧设计把 B 直接放在 X 的上下文开头，每轮 B 随 LatestB 增长而改变内容。结果：**每次 Blogger 成功都让 X 的整个前缀缓存报废**。这是 P0 级性能 Bug。

修复：把"Y 的最新工作记忆"（`LatestB`）与"X 已冻结的前缀快照"（`ActivePrefixEpoch.FrozenB`）拆成两个独立概念。平常回合 LatestB 增长但 X 前缀不动；只有上下文阈值到达时才切换 Epoch，产生一次不可避免的冷边界。

### 5.1 关闭官方 Compaction

OpenCode 官方 compaction 必须关闭。

[NORMATIVE] Epoch 切换算法：

```text
ProjectedInputTokens =
  tokens(system + tools + caps + active messages + current request)

SwitchEpoch iff:
  ProjectedInputTokens + ReservedOutputTokens > ContextLimit
  AND tokens(FrozenCandidate) + tokens(rawTail) < tokens(current active messages)
  AND BlogBase coverage proof succeeds

Estimator 不可用时，使用保守估算：
  按 UTF-8 bytes ÷ 3 估算 token 数
  ReservedOutputTokens = max(2048, estimated_output_tokens)
  ContextLimit = min(provider_max, model_max, host_max)
  三者任一不可知 → fail-closed，不切换 epoch
```

```fsharp
if shouldActivateEpoch memory then
    memory.ActivePrefixEpoch <-
        Some { EpochId = deterministicEpochId memory
               FrozenB = memory.LatestB
               CutoffMessageId = currentCutoffMessageId
               CoveredPrefixDigest = hash current }
    memory.PrefixReplacementEnabled <- true
```

此后每次投影：

```fsharp
let projectForX canonical =
    match memory.ActivePrefixEpoch with
    | None -> canonical                     // 未启用替换前，原始输出
    | Some epoch ->
        Prefix.replaceCoveredPart
            coveredBy = epoch.FrozenB
            replacement = syntheticBlogMessage epoch.FrozenB
            current = canonical
```

[NORMATIVE]

- **LatestB 与 ActivePrefixEpoch 严格分离**：平常回合 LatestB 持续累积，X 请求的前缀逐字节不变。只有 epoch 切换才引发一次冷缓存。
- 切换后 X 请求形状变为 `frozenB + rawTail`，后续 LatestB 继续增长但 X 的前缀冻结。
- 下一次 epoch 切换：当前 `frozenB + rawTail` 接近上限 → 新的 `frozenB'` 替换前缀 → rawTail 变短。
- `BlogBase` 本身就是"B 已覆盖到哪里"的 JSON 证据，不另造 cursor/generation。
- replacement message 标记内部 provenance，CanonicalProjection 计算 delta 时过滤，避免 Y 总结自己的 B。
- 一旦启用，本 Session 生命周期内持续启用；不来回开关。

投影输出形状：

```text
System instructions
Tools
Fixed caps-head (不参与 replacement)
Frozen companion epoch (只在 epoch 切换时变化)
Raw append-only tail (只追加，不回写)
Current request
```

### Seal Barrier（追加冻结）

[NORMATIVE]

```text
Provider request R_n 发出后，R_n 中的所有 provider-visible bytes 永久 sealed。
尚未进入任何 provider request 的 tail entity 可以 upsert。
每次新请求只允许在上一次 sealed bytes 后追加。

Seal 边界 = 最终进入 provider 的完整消息序列，
不是 OpenCode 原始 message（因为 part 会追加/更新）。
只有 terminal 后的 semantic turn 可被 seal。
```

## Provider-visible projection

缓存比较只使用真正进入模型的字段：

```fsharp
type ProviderVisibleMessage =
    { Role: string
      Text: string option
      Reasoning: string option
      ToolCallId: string option
      ToolName: string option
      ToolArgs: JsonValue option
      ToolResult: JsonValue option
      SyntheticProvenance: string option }
```

### Synthetic 稳定身份

[NORMATIVE]

```text
syntheticId = hash(sessionId + epochId + semanticKind)
同一 epoch 内 role/content/parts/IDs/order 全部逐字节固定
不得使用 GUID、Math.random、当前时间

Synthetic provenance 不进 canonical delta，不进 Provider-visible cache comparison。
```

排除字段：timestamp、cost、usage、runtimeId、directory、status、UI metadata、finish reason。

Companion delta、prefix equality 和缓存门禁测试必须使用同一份 Provider-visible projection。

---

## 六、B 的读取

`B(X)` 只读取 Y 当前有效投影中的 assistant 正文：

```fsharp
let currentB yProjection =
    yProjection.Messages
    |> List.choose AssistantFormalText.tryGet
    |> String.concat "\n\n"
```

不读取 Y 的 user 输入，因此旧 B 在自压缩时作为输入出现，不会混入新 B。

`LatestB` 是 Y 的完整累积输出。`ActivePrefixEpoch.FrozenB` 是冻结的快照版本。两者关系：

- 平常回合：`FrozenB` 不变，`LatestB` 增长。
- Epoch 切换：`FrozenB = LatestB`（冻结），之前 rawTail 中的旧内容不再向前传递。
- 自压缩：`LatestB` 替换为自压缩后的 B′，但 `FrozenB` 保持不变，直到下次 epoch 切换才搬过去。

创建有伴随子代理时：

```text
System role prompt
Parent B background
Current fork prompt
```

[NORMATIVE] ChildBackgroundB =
  fork 动词开始时，父 session durable LatestB 的不可变快照。

它只是背景，不声称父模型已经见过；
必须记录 ParentBDigest；
创建失败重试时复用同一快照，不重新读取最新值。

父 B 是背景，不要求 Manager 重复解释仓库历史。

---

## 七、投影主函数

```fsharp
let projectSession (s: CompanionScript) hostInput =
    companion {
        let canonical = s.Canonicalize hostInput
        do! s.OfferDelta canonical

        if s.ShouldEnablePrefixReplacement canonical then
            do! s.EnablePrefixReplacement()

        return s.ProjectForModel canonical
    }
```

主函数不出现：

- message event subscription；
- compaction stage；
- summary owner；
- pending delta queue；
- prefix generation；
- Blogger scheduler。

---

## 八、必须测试

1. 第一轮生成 B。
2. Y 忙时连续三次 X 投影，Y 只启动一次；空闲后下一 delta 包含三次变化。
3. Y 输出不含其输入。
4. X 开启替换前输出完全等于 canonical。
5. 开启后 Epoch 替换 BlogBase 前缀，raw suffix 保留。
6. LatestB 增长不改变 X 的 active prefix（epoch 未切换时 X 前缀逐字节不变）。
7. synthetic B 不进入下一次 delta。
8. Y 自压缩后 LatestB 只等于 B'，ActivePrefixEpoch 不变。
9. Epoch 切换后 X 请求产生一次可预期的冷边界，之后恢复 append-only。
10. Blogger 失败不阻塞 X 投影。
11. reasoning 缺失时机制仍正确。
12. JSON 属性顺序变化不产生 delta。
13. 工具大输出经过 canonical 后可正确 YAML literal 编码。
14. Provider-visible projection 变化而非模型字段变化，不产生 delta。
15. Transform Hook 幂等：重复调用不重复注入 caps-head/companion-b-head。
16. Same semantic content but different Host metadata → same Provider-visible digest.


---

# KISS-N03 — 异步 Fork / Join / List DSL

本卷定义 Manager 的全部控制面。Subagent 不再伪装成同步工具。

---

## 一、模型工具 Schema [NORMATIVE]

### 1.1 fork

```json
{
  "agent": "coder | inspector | browser | meditator | reviewer | executor | manager | <hex6>",
  "prompt": "string",
  "signal": "TERM | KILL | INT | HUP | ... (optional)"
}
```

约束：

- `signal` 仅当 `agent` 指向 PTY handle 时合法。
- 新建普通 agent 时 `prompt` 非空。
- 已有 agent ID + prompt = nudge/continue。
- PTY ID + prompt 非空 = write。
- PTY ID + prompt 空 = read 请求。
- PTY ID + signal = 发结构化 signal。

### 1.2 join

```json
{}
```

等待任意一个完成项。返回对象自带 handle ID、kind、role 和 A 版工作记录或 PTY 结果。

### 1.3 list

```json
{
  "kind": "all | agent | pty (optional)"
}
```

返回当前运行期资源的派生视图。

Orchestrator 只暴露 `fork / join`，不暴露 `list`。

---

## 二、运行时真实结构

```fsharp
type HandleId = private HandleId of string

[NORMATIVE]

```text
HandleId 至少包含 parent session identity；
同一 parent 生命周期内永不复用；
重启恢复原 handle，不能重新生成；
已退休 ID 永久返回 RetiredHandle，不回落为"把它当角色名"。
```

type AgentHandle =
    { Id: HandleId
      Role: AgentRole
      SessionId: string
      CompanionSessionId: string option
      Lifetime: CancellationTokenSource }

type PtyHandle =
    { Id: HandleId
      Pty: IPtyProcess }

type RuntimeHandle =
    | Agent of AgentHandle
    | Pty of PtyHandle
    | ManagerJob of ManagerJobHandle

type Completion =
    | AgentCompleted of HandleId * AgentRole * ARecord
    | AgentFailed of HandleId * AgentRole * string
    | PtyRead of HandleId * string
    | PtyExited of HandleId * int
    | ManagerPublished of HandleId * CommitHash
    | ManagerFailed of HandleId * string
```

基础设施：

```fsharp
type ForkRuntime =
    { Handles: ConcurrentDictionary<HandleId, RuntimeHandle>
      Completions: Channel<Completion> }
```

这不是 AgentStateMachine。状态直接来自：

- handle 是否存在；
- Host Session 是否 busy/idle；
- PTY Process 是否退出；
- Completion Channel 中是否有值。

---

## 三、新建 Agent

```fsharp
let forkNew role prompt =
    agent {
        let parentB = currentParentB ()
        let! child = host.CreateChildSession(role, parentB)
        let handle = register child role

        // 先监听，再发送；极快 terminal 也不会丢。
        attachTerminalProjection handle

        // Send 返回只表示请求已交给宿主；不等待 child 完成。
        do! host.SendPrompt(child.SessionId, prompt)
        return Forked handle.Id
    }
```

[NORMATIVE]

- fork 返回得快；child 在后台自然运行。
- 有伴随角色自动创建 Y。
- child 初始上下文自动含 parent B。
- terminal 提取 A 版正文，不含 reasoning。
- completion 写入 Channel；Manager 下一次 `join()` 获取。

---

## 四、已有 Agent ID = Nudge

```fsharp
let nudgeExisting handle prompt =
    agent {
        // 故意 fire-and-forget；不建插件队列，不等待当前 run。
        // Busy existing agent：不创建新 RunId、不安装新 listener、不创建新 completion。
        // Nudge 归属于当前 active Run。只有 idle 后再 fork 才创建新 completion。
        host.PostPromptFireAndForget(handle.SessionId, prompt)
        return Nudged handle.Id
    }
```

[NORMATIVE]

```text
Nudged = Host 已确认接受 prompt
NudgeSubmitted = 已尝试提交，不保证接受

若 Host 不支持 busy append，不能用 prompt queue 补救（SSOT 禁止插件队列）；
正确结果应是显式 BusyNudgeUnsupported，而不是返回成功。

Fire-and-forget Task 必须被最小观察：失败写诊断，不能触发第二调度器。
```

[NORMATIVE] Busy→idle 竞态归属：

```text
Host AcceptPrompt 返回的 run/message identity 是唯一归属依据。
不能仅依据运行时缓存的 Busy/Idle。
无法获得 identity 时，该 Host 不满足 busy-nudge contract。
```

---

## 五、Join

```fsharp
let joinAny () =
    agent {
        let! completion = runtime.Completions.Reader.ReadAsync(ct)
        return completion
    }
```

[NORMATIVE]

- 哪个先进入完成邮箱就返回哪个。
- 不允许传 child ID。
- 不做结果排序。
- 不建立 Waiter Registry；Channel 是邮箱本身。
- child 极快完成也不会丢，因为 terminal listener 在发送或创建时即安装。
- 父取消时 `ReadAsync` 被取消。

若当前无活跃 handle，`join()` 应返回明确的 `NothingToJoin` 工具结果，避免模型永等。这个判断直接由 Handles/PTY 派生，不保存 `JoinState`。

[NORMATIVE] Completion exactly-once：

```text
每个 RunIdentity 对应一个 single-assignment completion cell。
Terminal/SendFailure/Cancel 竞争 TrySetResult，首个成功者唯一生效。
Mailbox 只接收该 cell 的唯一结果。
join 消费后永久删除 completed handle。
```

---

## 六、List

```fsharp
let list filter =
    runtime.Handles.Values
    |> Seq.choose (HandleView.project filter)
    |> Seq.toList
```

示例：

```text
a1b2c3  coder      busy
91ff02  reviewer   idle
0304aa  pty        running
```

`busy/idle/running/exited` 是展示层瞬时派生值，不进入持久化领域。

---

## 七、父取消与关闭

父 Session abort：

```fsharp
agent {
    for handle in ownedHandles do
        do! cancelPhysicalResource handle
    return ()
}
```

- Agent：调用宿主 abort/close。
- PTY：SIGKILL 整个进程树并等待退出。
- ManagerJob：取消 manager，保留或清理 worktree 按 Orchestrator 策略。

不写 `CancellingStage`。`cancelPhysicalResource` 返回后，资源必须已经完成物理收敛或暴露真实 bug。

---

## 八、工具调用映射

```fsharp
let executeFork input =
    agent {
        match resolve input.Agent with
        | NewRole role ->
            return! forkNew role input.Prompt
        | Existing (AgentHandle h) ->
            return! nudgeExisting h input.Prompt
        | Existing (PtyHandle p) ->
            return! forkPtyOperation p input
        | Existing (ManagerJob _) ->
            return! agent.fail (InvalidFork "manager job cannot be nudged here")
    }
```

一个 `match` 就是完整 dispatcher；禁止另建 CommandBus/OperationRegistry。

---

## 九、测试

1. fork 返回前 terminal listener 已安装（或用 reconcile 代替后 equivalent）。
2. 极快 child 结果可被后续 join 取得。
3. 两 child 完成先后不同，join 按邮箱先后返回。
4. nudge running child 不打断、不建插件队列、不创建新 RunId。
5. nudge idle child 可继续同一 session。
6. list 同时显示 agent/pty。
7. join 无活跃资源返回 NothingToJoin。
8. 父取消关闭所有 owned handles。
9. A 版不含 reasoning/tool raw stream。
10. parent B 自动进入新 child 背景。
11. Busy agent → nudge → RunId 不变 → completion 恰好一次（不重复、不丢失）。
12. Executor summarizer 使用私有 mailbox，不从 Manager mailbox 偷 completion。


---

# KISS-N04 — 角色与能力矩阵

角色不是可动态组合的权限平台。第一版使用静态表和静态工具注册。

---

## 一、角色表 [NORMATIVE]

|角色|伴随 Blogger|可用工具|说明|
|---|---:|---|---|
|`orchestrator`|是|`fork`, `join`|只 fork ManagerJob|
|`manager`|是|`fork`, `join`, `list`|不直接读写仓库|
|`coder`|是|`read`, `write`, `edit`, `glob`, `grep`, `inspector`|真正修改代码|
|`inspector`|否|`executor`|命令调查；无直接 Python/JS execute|
|`browser`|否|`read`, web tools|仓库读取与上网|
|`meditator`|否|`read`, `glob`, `grep`, `inspector`|推理、方案、权衡|
|`reviewer`|否|`read`, `glob`, `grep`, `inspector`, `verdict`|只读审查；verdict 结构化|
|`executor` Agent|否|无|命令大输出 summarizer|
|`blogger`|否|无|增量工作记录|

[NORMATIVE] 任何未列出的工具都必须在 schema 层不可见，不是 execute 时拒绝。

规范工具集合：

```text
Coder      = read, write, edit, glob, grep, inspector
Inspector  = executor
Browser    = read, glob, grep, web tools
Meditator  = read, glob, grep, inspector
Reviewer   = read, glob, grep, inspector, verdict
Orchestrator = fork, join
Manager    = fork, join, list
Executor Agent = （无）
Blogger    = （无）
```

### PTY 与 Executor 权限规则

[NORMATIVE]

1. **Inspector 只能使用 Executor Tool。**
   * Inspector 不得拥有 `fork`、`join`、`list`。
   * Inspector 不得创建、读取、写入、发送信号或关闭 PTY。
   * Inspector 不得创建任何 subagent。
   * Inspector 的完整工具集合必须严格等于：

```text
executor
```

2. **只有 Manager 可以创建和操作 PTY。**
   * Manager 通过 `fork` 的结构化 PTY 变体创建 PTY。
   * Manager 通过 `fork(existingPtyId, operation)` 对已有 PTY 执行输入、读取、Signal 或 Close。
   * PTY handle 出现在 Manager 的 `list()` 中。
   * PTY completion 进入 Manager 的统一 `join()` 邮箱。
   * Orchestrator、Coder、Inspector、Browser、Meditator、Reviewer、Executor、Blogger 均不得直接操作 PTY。

3. **`fork` 的可见语义按角色静态收窄。**

```text
Manager 的 fork 支持：
  fork(role, prompt)
  fork(existingAgentId, prompt)
  fork(ptyRequest)
  fork(existingPtyId, ptyOperation)

Orchestrator 的 fork 只支持：
  fork(managerPrompt)

其他角色不得看到 fork 工具。
```

4. **权限必须在工具 Schema 层隐藏。**
   角色无权使用的工具或 union variant，必须根本不出现在其模型可见 Schema 中。

5. **Inspector 需要执行命令时只能调用 Executor Tool。**
   Executor Tool 负责：
```text
启动非交互命令
执行唯一的 3 × estimated_running_secs 时限
捕获 stdout/stderr
按输出预算进行 spool 和摘要
返回结构化命令结果
```

[FORBIDDEN]

- fuzzy_grep / fuzzy_glob / fuzzy_continue。
- Manager 继承普通工具。
- Reviewer 写文件。
- Blogger 调工具。
- 动态 Tool Capability Registry。

---

## 二、静态配置

```fsharp
type RoleDefinition =
    { Role: AgentRole
      Prompt: string
      Companion: bool
      Tools: ToolId list
      ModelA: ModelRef
      ModelB: ModelRef }

let roles : Map<AgentRole, RoleDefinition> =
    [ orchestratorDefinition
      managerDefinition
      coderDefinition
      inspectorDefinition
      browserDefinition
      meditatorDefinition
      reviewerDefinition
      executorAgentDefinition
      bloggerDefinition ]
    |> List.map (fun x -> x.Role, x)
    |> Map.ofList
```

这是静态数据，不支持插件运行期改写角色拓扑。新增角色必须代码审查。

---

## 三、Coder → Inspector 是局部同步子程序

Coder 工具：

```json
{
  "prompts": ["调查 A", "调查 B"]
}
```

每个 prompt 创建一次性 Inspector Session：

```fsharp
let inspectOnce prompt =
    coder {
        use! inspector = createOneShotInspector (currentCoderB())
        return! inspector.Run prompt
    }
```

并行：

```fsharp
let inspectMany prompts =
    prompts
    |> List.map inspectOnce
    |> parallel
```

[NORMATIVE]

- 一个 prompt 一个 Inspector Session。
- 不复用同一个 Inspector 并发。
- Inspector 完成后立即释放物理 session。
- 每个 Inspector 用 Coder 当前 B 作为背景。
- Coder 只接收 Inspector A 版结果。

这是同步局部调用，不与 Manager 异步 fork 语义混用。

[NORMATIVE] One-shot Inspector 契约：

```text
它是资源作用域，不是 ForkRuntime 的特殊模式。
  use! inspector = createInspector snapshotB
  let! result = inspector.Run request
  return result

约束：
- 阻塞当前 Coder tool call
- 使用独立 completion mailbox（不污染 Manager mailbox）
- 不创建 Blogger
- 继承 Coder fallback side
- Inspector provider failure 作为 Coder 工具错误返回
- Coder cancel 必须 await Inspector abort
- 多个 Inspector 并发时结果顺序与输入 prompts 顺序一致
- Inspector terminal 无正文 → 返回空结果（不是错误）
- 完成后删除 session，不只解除关联
- 不注册到 Manager 的 list/join
- 不可持久化为可 nudge agent
```

---

## 四、Inspector → Executor Tool

Inspector 调用：

```json
{
  "command": "...",
  "estimated_output_bytes": 12345,
  "estimated_running_secs": 30,
  "estimated_mem_usage": "medium | large"
}
```

Tool 执行真实命令。若输出触发摘要，Tool 内部创建一次性 Executor Agent：

```text
Executor Agent system prompt
Inspector A 版工作记录背景
Command + 200KB chunk(s)
```

Executor Agent 只返回摘要正文，不调用工具。

同名消歧：

- `AgentRole.Executor` = 模型 summarizer。
- `Tool.executor` = OS command tool。

用户语义清晰，实现类型安全即可，不改产品名。

---

## 五、角色 Prompt 只写职责，不写调度状态

正确：

```text
You are a reviewer. Inspect the current worktree.
Use verdict(REVISE) immediately when changes are required.
Use verdict(PERFECT) twice to confirm a flawless result.
```

错误：

```text
You are currently in ReviewStage=AwaitingConfirmation.
Owner=manager; Generation=4; Lease expires...
```

Prompt 不携带内部程序计数器。

---

## 六、背景注入

新角色 Session 的固定顺序：

```text
Role system prompt
Parent B work record (if any)
Fork prompt / local request
```

不自动注入父完整 transcript。需要精确代码事实时由角色读文件或 Inspector 执行命令。

---

## 七、测试

1. 每角色工具集合精确匹配表。
2. Manager 无 read/write/edit。
3. Reviewer 无写工具。
4. Blogger/Executor Agent 无工具。
5. Coder `inspectMany` 确实并发且每项独立 session。
6. Inspector only Executor Tool。
7. 新 child 背景来自父 B。
8. 未注册工具在 schema 层不可见。


---

# KISS-N05 — Executor / Process / Output Summary / PTY

进程是资源作用域。唯一执行时限来自模型给出的 estimate；系统不再叠加多层 timeout。

---

## 一、Executor Request [NORMATIVE]

```fsharp
type MemoryEstimate = Medium | Large

type ExecutorRequest =
    { Command: string
      WorkingDirectory: string
      EstimatedOutputBytes: int64
      EstimatedRunningSeconds: int64
      EstimatedMemoryUsage: MemoryEstimate }
```

派生值：

```fsharp
let processLimit request =
    TimeSpan.FromSeconds(float request.EstimatedRunningSeconds * 3.0)

let summaryTrigger request =
    request.EstimatedOutputBytes * 3L
```

[NORMATIVE]

- 模型允许填巨大 estimate；运行时不 clamp 到隐藏上限。
- `3 × estimated_running_secs` 是从进程启动到正常退出的唯一时间预算。
- Spawn、等待、SIGKILL、pump drain 不各自获得新 timeout。
- 超时后发 SIGKILL/平台等价的 kill entire process tree。
- Kill 后无第二兜底 timeout；若 SIGKILL 路径不能返回，这是实现 bug，测试应暴露。
- `Medium` 不做并发限制。
- `Large` 使用 OpenCode 进程级 `SemaphoreSlim(1)`。

[NORMATIVE]

```text
running_secs > 0
output_bytes >= 0
必须是有限数
乘法使用饱和/checked 语义
超过可表达范围 = effectively unbounded，由 CancellationToken 终止
```

---

## 二、Process DSL

```fsharp
let runExecutor request =
    process {
        use! largeLease =
            match request.EstimatedMemoryUsage with
            | Medium -> noLease
            | Large -> acquireGlobalLargeLease ()

        use! child = spawnWithPumps request
        let! result = waitOrKill child (processLimit request)
        return! maybeSummarize request result
    }
```

没有：

- ProcessStage；
- KillPhase；
- TimeoutCoordinator；
- MemoryLease 日志状态；
- ExecutorOwner。

`largeLease` 就是一个 semaphore releaser 资源。

---

## 三、泵序 [NORMATIVE]

```text
Create process object
→ connect stdout/stderr
→ start bounded pumps
→ start process
→ close/finish stdin protocol
→ await exit or unique timer
→ timer wins: kill entire process tree
→ await physical exit
→ await pumps EOF
→ build ProcessResult
→ async dispose
```

[FORBIDDEN]

```text
Start → WaitForExit → Read stdout
```

它会因管道填满死锁。

结果：

```fsharp
type ProcessResult =
    { ExitCode: int
      Stdout: OutputCapture
      Stderr: OutputCapture
      Duration: TimeSpan
      TimedOut: bool }
```

非零退出码是命令事实，返回 Inspector 判断。

---

## 四、大输出与 200KB 在线 Ripple-Carry 摘要

触发条件：

```text
stdout bytes + stderr bytes > 3 × estimated_output_bytes
```

触发后：

[NORMATIVE]

```text
chunk = 连续合并 stdout/stderr 后的 UTF-8 byte stream，每块 204800 bytes
fan-in = 8
map 并发，但结果按 chunk index 排序
每凑齐 8 个同 level summary，立即 reduce 为 level+1
最终从低 level 到高 level 按原始范围归并
所有 Executor IDs 由 processId + level + range hash 生成（确定性，不使用 Math.random）

最后一块不足 200KB：正常作为一块处理。
map/reduce 失败：返回原始尾部（最后 200KB 原始输出）+ 已完成的部分摘要。
```

## 五、Executor 私有 Runtime

Executor map/reduce 不应该与 Manager 的普通 Coder、Reviewer、PTY completion 竞争同一个 `join()` 邮箱。

```fsharp
// parent session 拥有一个私有 Executor runtime
let executorRuntimeFor (context: AgentContext) =
    let runtimes = context.ExecutorRuntimes
    match runtimes.TryGetValue(context.SessionId) with
    | true, rt -> rt
    | false, _ ->
        let rt = createPrivateRuntime context
        runtimes.[context.SessionId] <- rt
        rt

// Executor map/reduce 使用私有 mailbox
let summarizeChunkWithExecutorAgent chunk =
    process {
        let rt = executorRuntimeFor context
        let! child = rt.Fork(NewAgent Executor, chunkPrompt chunk)
        let! completion = rt.JoinAny()  // 不干扰 Manager mailbox
        return extractSummary completion
    }
```

生命周期：parent session `delete`/`abort`/`dispose` 时，清理 executor runtime 及其所有 child。

## 六、超大 Deadline 分段

`3 × estimated_running_secs` 是唯一 deadline。但 model 可能填巨大 estimate（几天/几周），超过 JavaScript timer 上限。

```fsharp
let waitWithDeadline (process: Process) (deadline: TimeSpan) (ct: CancellationToken) =
    task {
        let maxTimer = TimeSpan.FromMilliseconds(float Int32.MaxValue)  // ~24.8 天
        let mutable remaining = deadline
        while remaining > TimeSpan.Zero && not ct.IsCancellationRequested do
            let chunk = min remaining maxTimer
            let! exited = waitForExitOrTimeout process chunk ct
            if exited then return
            remaining <- remaining - chunk
        if remaining <= TimeSpan.Zero then
            killProcessTree process
    }
```

## 七、Large Gate

```fsharp
let globalLargeGate = SemaphoreSlim(1, 1)

let acquireGlobalLargeLease ct = task {
    do! globalLargeGate.WaitAsync(ct)
    return AsyncDisposable.create(fun () ->
        globalLargeGate.Release() |> ignore)
}
```

- 只限制 `Large`。
- Medium 不经过 semaphore。
- 成功、失败、取消、SIGKILL 都由 `use!` finally 释放。
- 不记录队列位置，不做公平性平台。

Executor Agent 输入包含：

- Inspector 当前 A 版工作记录；
- command；
- 当前块编号/总块数；
- 原始输出块。

提示词要求保留错误、路径、行号、统计、测试结论，删除重复日志和原始代码洪流。

---

## 八、PTY 复用 fork 表面

创建：

```json
{
  "agent": "pty",
  "prompt": "bash"
}
```

写：

```json
{
  "agent": "a1b2c3",
  "prompt": "npm test\n"
}
```

读：

```json
{
  "agent": "a1b2c3",
  "prompt": ""
}
```

信号：

```json
{
  "agent": "a1b2c3",
  "prompt": "",
  "signal": "TERM"
}
```

[NORMATIVE]

- 不使用 `[#SIGTERM]` 魔法字符串。
- 每次 read 结果进入 completion mailbox，供 `join()` 返回。
- PTY 自然退出写 `PtyExited` completion。
- `list()` 与 Agent 一起展示。
- 内部仍是独立 `IPtyProcess`，不强行伪装成 LLM Session。

[NORMATIVE] PTY read 契约：

```text
每次 read 返回自上次 read 后的 unread delta，不清空总 buffer。
UTF-8 半字符：保留在内部 buffer，下次 read 时拼接。
buffer 上限：实现定义，但必须 > 64KB。
背压：由 pump 和 buffer 上限自然形成。
process exit 后：可读剩余 buffer，然后返回 PtyExited。
Signal enum 精确集合：TERM, KILL, INT, HUP, QUIT, USR1, USR2。
TERM 后默认等待 5 秒再 KILL（可被 Manager 覆盖）。
Close = stdin EOF（不是 SIGKILL）。
```

---

## 九、测试

1. 子进程极速输出不死锁。
2. stdout/stderr 同时大输出不死锁。
3. 3×时间到达后 kill entire tree。
4. SIGKILL 路径物理返回；模拟不返回时测试明确挂出 bug。
5. Huge estimate 不被 clamp。
6. Medium 100 个并发不经 gate。
7. Large 严格同时一个。
8. 输出恰好阈值不摘要，超过一字节摘要。
9. 200KB UTF-8 边界不切坏字符。
10. 多级摘要保留 exit code、错误路径、测试结论。
11. PTY write/read/signal schema 正确。
12. PTY 与 Agent 同时出现在 list。
13. Executor 私有 runtime 不泄漏给其他 session。
14. parent abort 后 executor runtime 正确清理。


---

# KISS-N06 — A/B 角 Fallback

Fallback 是每个 Session 调用模型时的一段递归函数，不是独立状态机。

---

## 一、冻结语义 [NORMATIVE]

每个 Session 有两个模型角色：

```fsharp
type ModelSide = A | B
```

和一个单调失败计数：

```fsharp
type FallbackMemory =
    { mutable Side: ModelSide
      mutable Failures: int }
```

规则：

|失败序号|动作|
|---:|---|
|1|仍用 A，立即重试|
|2|永久切换 B，并立即尝试 B|
|3|仍用 B，立即重试|
|4|Session 真死|

成功不会把 Side 切回 A，也不会减少 Failures。`Failures` 是该 Session 生命周期累计失败数。

---

## 二、结构化实现

```fsharp
let rec invokeWithFallback (s: ModelSession) request =
    agent {
        if s.Fallback.Failures >= 4 then
            return! agent.fail (SessionDead s.Id)

        let model =
            match s.Fallback.Side with
            | A -> s.Role.ModelA
            | B -> s.Role.ModelB

        match! s.Host.TryInvoke(model, request) with
        | InvocationSucceeded output ->
            return output

        | InvocationFailed error ->
            s.Fallback.Failures <- s.Fallback.Failures + 1

            match s.Fallback.Failures with
            | 1 ->
                return! invokeWithFallback s request

            | 2 ->
                s.Fallback.Side <- B
                return! invokeWithFallback s request

            | 3 ->
                return! invokeWithFallback s request

            | _ ->
                return! agent.fail (SessionDead error)
    }
```

控制流完全可见：一个 side、一个计数、一个递归函数。

---

## 三、为什么这不是 Fallback State Machine

它没有：

- Phase enum；
- RemainingModels；
- Attempt record；
- Retry owner；
- fallback journal；
- recovery transition table；
- Governor；
- Coordinator；
- Lease。

`Side` 是永久模型选择事实，`Failures` 是预算计数。程序下一步由普通模式匹配决定：

```fsharp
match failures with
| 0 | 1 -> A
| 2 -> permanent_switch_to_B
| 3 -> B
| _ -> Dead
```

这不需要状态图，不需要持久化"执行到第几步"。只需要从一个简单的事实（累计失败数）推导出当前策略。

### 为什么不需要 RemainingModels 列表？

因为只有 A 和 B 两种模型。切到 B 后不切回 A。所以不需要列表、不需要索引、不需要"下一个候选"。

### 为什么不需要 FallbackPhase？

因为 Fallback 的"下一步"完全由 `failures` 和 `side` 决定。`failures` 是事实（写死的），`side` 是事实（写死的）。不需要中间阶段。

### 失败定义

沿用宿主现有"可触发 fallback 的模型调用失败"分类，不新增 Governor。

[NORMATIVE]

- provider/transport/model invocation failure：计一次。
- 用户取消/父取消：不计失败，直接取消。
- 正常 assistant 输出内容质量差：不计模型调用失败；由 Manager/Reviewer 处理。
- Reviewer 未调用 verdict：不是 provider failure；由 Reviewer Guard nudge。
- Executor command 非零退出：不是模型 failure。

### 适用范围

每个 LLM Session 独立拥有 FallbackMemory：

- Orchestrator；
- Manager；
- Coder；
- Inspector；
- Browser；
- Meditator；
- Reviewer；
- Blogger；
- Executor Agent。

---

## 四、持久化

`Side` 与 `Failures` 写入宿主 Session metadata。运行时可通过 Journal 持久化。

### Fallback identity

每个 failure 的稳定身份：

```text
sessionId + currentUserMessageId + providerAttempt

currentUserMessageId =
  当前 provider attempt 所属的 Host run root user message，
  不包括插件 synthetic continuation/background/reset frame。

Adapter 必须从 typed Host API 获得 currentUserMessageId，
禁止通过"最后一条 user message"猜测。
```

`session.status=retry` 事件到达后，提取上述 identity，检查是否已记录：

- 未记录 → append `FallbackFailureRecorded`，Fold 更新计数
- 已记录 → 跳过（去重）

### 唯一写入口

[NORMATIVE] 唯一允许写 `FallbackFailureRecorded` 的入口：`session.status=retry`。

以下情况**不写** durable fallback：

- `observeIdle` 发现空/XML-only assistant
- 重复 idle
- session.error (若后续没有 retry)
- user cancel / parent abort
- 零宽 continuation

空/XML-only terminal 最多触发一次 continuation，不进入 A/B 计数。

[NORMATIVE] 空/XML-only continuation identity：

```text
InteractionRepairIdentity =
  sessionId + rootUserMessageId + terminalAssistantMessageId + repairKind

修复合法的 classifier：
  - 只包含 XML tag 的 assistant（不含任何非 XML 正文）
  - 只包含 reasoning（不含 text）
  - 只包含 tool call（不含 text）
  - 空
  - 空白

同一 identity 最多触发一次 continuation。
任何新 root user message 自动产生新预算。

XML-only = 输出只包含 <tag>...</tag>，没有可见正文。
Tool call XML 计入 XML-only（因为它不是自然语言）。
```

[NORMATIVE] OpenCode 原生 retry 与 A/A/B/B 轨迹：

```text
git retry event #1 → Failures=1，下一 provider request 仍 A
git retry event #2 → Failures=2，下一 provider request B
git retry event #3 → Failures=3，下一 provider request B
git retry event #4 → SessionDead，禁止下一 request

同一 user turn 可能产生多次 retry，每次独立计数。
providerAttempt 是插件自增编号（不是宿主原始编号）。

宿主在发下一 request 前，插件必须有能力确定模型。
若 Hook 做不到，则这套语义在"不修改 OpenCode 本体"约束下不可实现，
必须先证明 Host contract。
```

---

## 五、测试

1. A 首次成功。
2. A 失败一次，A 重试成功。
3. A 两次失败，永久切 B，B 成功。
4. 后续新 turn 仍使用 B。
5. B 第三次累计失败后重试 B。
6. 第四次累计失败 SessionDead。
7. 成功不清零 Failures。
8. 用户取消不计数。
9. 每个 child 计数独立。
10. Blogger 死亡不杀 X；仅停止 B 更新。


---

# KISS-N07 — ReviewGuard 与双 PERFECT

Review 不再依赖 todo，也不再是独立 Coordinator。它是 Manager 的结束守卫和 Reviewer 的 verdict 守卫。

---

## 一、Verdict Tool [NORMATIVE]

Schema：

```json
{
  "verdict": "PERFECT | REVISE"
}
```

工具不接受描述字段。Reviewer 的 assistant A 版工作记录承担描述。

规则：

- `REVISE`：第一次调用立即生效。
- `PERFECT`：第一次仅返回"请再次调用 PERFECT 确认"；第二次连续 PERFECT 才生效。
- 第二次 PERFECT 必须来自不同 ProviderRunIdentity，
  且该 Run 的 user 输入必须包含第一次 PERFECT 后由 ReviewGuard 发出的确认请求。
  仅 ToolCallId 不同不足以证明独立确认。
  同一个 assistant message 内连续调用两次 PERFECT 无效。
  两次之间出现普通文本、read、grep 不打断确认序列。
  verdict tool 自身失败不改变当前确认状态。
  REVISE 后立即建立新 barrier。
- 任意 REVISE 清除未完成的 PERFECT 确认。
- Git tree 变化清除未完成或已确认的 PERFECT。
- Post-rebase 审查屏障必须是全新的双 PERFECT（两个新的不同 ToolCallId），不得复用 rebase 前或历史上对同一 tree hash 的确认。

---

## 二、Reviewer 局部程序

```fsharp
let rec awaitVerdict confirmations reviewedTree =
    review {
        match! nextReviewerEvent () with
        | VerdictCalled REVISE ->
            return RevisionRequired

        | VerdictCalled PERFECT when confirmations = 0 ->
            do! returnToolMessage
                    "PERFECT requires confirmation. Call verdict(PERFECT) again."
            return! awaitVerdict 1 reviewedTree

        | VerdictCalled PERFECT ->
            let! currentTree = git.currentTreeHash ()
            if currentTree = reviewedTree then
                return ConfirmedPerfect reviewedTree
            else
                do! nudgeReviewer
                    "The worktree changed. Re-read the current tree and review again."
                return! awaitVerdict 0 currentTree

        | AssistantTerminalWithoutVerdict output ->
            do! nudgeReviewer
                    "You returned without a valid verdict. Continue the review and call verdict."
            return! awaitVerdict confirmations reviewedTree

        | ProviderFailure error ->
            return! review.fail (ReviewerFailed error)
    }
```

这里的 `confirmations` 是递归参数，不是 `ReviewPhase`。

“Reviewer 不返回 Guard”定义为：Reviewer assistant 已 terminal，但没有产生可生效 verdict；Guard 立即向同一 reviewer fire-and-forget prompt，继续同一 Session。真正 provider 不返回由该 Session 的 A/B Fallback 处理，不另加 wall-clock timeout。

---

## 三、Review Witness

```fsharp
type ReviewWitness =
    | NoConfirmedReview
    | ConfirmedPerfect of
        {| ManagerJobId: string
           ReviewerSessionId: string
           ReviewBarrierId: string
           GitTreeHash: string
           FirstProviderRunId: string
           FirstToolCallId: string
           SecondProviderRunId: string
           SecondToolCallId: string |}
    | RevisionRequired of report: ARecord
```

这是当前代码树的审查事实，不是流程阶段。

- Reviewer REVISE → `RevisionRequired report`。
- 双 PERFECT → `ConfirmedPerfect reviewedTree`。
- 文件改动/tree hash 变化 → 投影为 `NoConfirmedReview`。

可以运行期保存；Orchestrator 发布时重新审查，所以无需复杂跨进程恢复。

[NORMATIVE] Manager Guard 只接受 durable projection 中完全匹配当前 Job 和 HEAD 的 witness。
Witness 必须满足：
- 来自该 Manager 创建的 Reviewer
- Reviewer 的 worktree 与 Manager worktree 相同
- barrier 是当前 ManagerJob
- tree 是当前 HEAD
- review 发生在最新修改之后
- reviewer role 未被伪造
- verdict tool call 已真正成功执行

---

## 四、Manager Guard

Manager 每次 assistant terminal 后：

```fsharp
let rec runGuardedManager manager =
    agent {
        let! output = manager.RunNextTurn()
        let! currentTree = git.currentTreeHash ()

        match manager.ReviewWitness with
        | ConfirmedPerfect tree when tree = currentTree ->
            return output

        | _ ->
            do! manager.PostPromptFireAndForget(
                "You may not finish yet. Fork or continue a reviewer, " +
                "resolve every REVISE, and obtain two confirmed PERFECT verdicts " +
                "for the current worktree."
            )
            return! runGuardedManager manager
    }
```

[NORMATIVE]

- Guard 不替 Manager 选择哪个 coder/reviewer。
- Guard 不读取 todo。
- Guard 不扫描 next action candidates。
- Guard 只判断“当前 tree 是否有确认 PERFECT”。
- 用户取消可终止循环。
- 不设置隐蔽最大轮数；审查应持续到满足或用户取消。

---

## 五、REVISE 路径

```text
Reviewer verdict(REVISE)
→ verdict tool 立即生效
→ join 返回 reviewer ID + A 版意见
→ Manager fork(existing coder, revision prompt) 或 fork(new coder)
→ coder 修改
→ tree hash 改变，旧 review witness 自动失效
→ Manager 再 fork/continue reviewer
```

工具 verdict 不带描述，避免 verdict 与报告双份字段不一致。

---

## 六、PERFECT 路径

```text
Reviewer verdict(PERFECT)
→ tool result: confirm again
→ Reviewer verdict(PERFECT)
→ bind current tree hash
→ reviewer A 版 terminal
→ join 返回 confirmed PERFECT
→ ManagerGuard 允许 finish
```

若第一次 PERFECT 后 reviewer 直接 terminal，Reviewer Guard nudge 同一 reviewer，请求第二次确认。

---

## 七、Orchestrator 复审

Manager worktree 在原基线确认 PERFECT 后，只得到"可进入发布"的资格。

Rebase 到最新目标分支后 tree/commit 变化：

```text
old witness invalid
→ same manager/reviewer context receives rebase result
→ reviewer 发出两个新的、不同 ToolCallId 的 PERFECT
→ ff
```

即使 rebase 后 tree hash 与 pre-rebase 相同（rebase 只改变 ancestry 不改变 tree），也必须发出两个新 ToolCallId 的 PERFECT，不得复用旧确认。

---

## 八、测试

1. REVISE 一次立即生效。
2. PERFECT 一次不生效。
3. 连续两次 PERFECT 同 tree 生效。
4. PERFECT、REVISE、PERFECT 不生效，需再次 PERFECT。
5. 第一次 PERFECT 后 tree 变化，计数归零。
6. terminal 无 verdict 被 nudge。
7. terminal 仅一次 PERFECT 被 nudge。
8. Manager 无 witness 不可 finish。
9. confirmed tree 后修改文件不可 finish。
10. rebase 后必须重新审查（两个新 ToolCallId）。
11. provider failure 走 Session Fallback，不走 reviewer wall-clock timer。
12. post-rebase tree hash 与 pre-rebase 相同仍视为未审查，需要新 PERFECT。


---

# KISS-N08 — Orchestrator / Worktree / Rebase / FF

Orchestrator 是更高一层的 Agent Program，不是万象阵 DAG Scheduler。

---

## 一、工具与职责

Orchestrator 只有：

```text
fork(manager, prompt)
join()
```

它不能：

- 读写仓库；
- 自己解决冲突；
- 操作 Git；
- 调普通子角色；
- 维护 DAG/wave/owner/lease。

每个 ManagerJob 的运行时动词内部负责 worktree 与发布。

---

## 二、用户输入 Clean Gate [NORMATIVE]

每次用户消息送入 Orchestrator 前：

```fsharp
let ensureClean target =
    orchestrator {
        let! dirty = git.statusPorcelain target
        match dirty with
        | [] -> return ()
        | paths -> return! orchestrator.fail (DirtyWorkspace paths)
    }
```

工作区 dirty 直接拒绝本次消息，不自动 stash、不自动 commit、不猜用户意图。

插件临时文件、worktree、spool 不得放进目标工作树制造 dirty；放在 Git common dir、仓库 sibling 或系统 cache。

[NORMATIVE] Clean Gate 竞态：

```text
接受用户消息时同时读取 target HEAD + porcelain。
创建 ManagerJob 前重新验证 HEAD 与 clean 状态。
不一致则拒绝本次 fork，不自动重试。
untracked、ignored 不算 dirty（仅 tracked file changes 和 staged changes）。
submodule dirty 算 dirty。
用户消息处理期间再次变 dirty：不影响已在执行的 ManagerJob，
但下一条用户消息必须重新验证。
```

---

## 三、fork ManagerJob

```fsharp
let forkManager prompt =
    orchestrator {
        // fork 只创建并转交资源所有权；不能在返回 handle 前 use! 释放 worktree。
        let! worktree = git.createIsolatedWorktree targetBranch
        let! manager = host.createManagerSession(worktree.Path, parentB())
        attachManagerGuard manager
        let handle = registerManagerJob worktree manager
        startOwnedManagerJob handle prompt
        return Forked handle.Id
    }
```

Manager 自动进入 ReviewGuard，不依赖 `/loop` 文本命令实现状态切换；若宿主必须通过 slash 激活，可由 Adapter 发送一次 `/loop`，领域层仍视为 `attachManagerGuard` 动词。`registerManagerJob` 把 worktree 所有权转交给后台 ManagerJob；只有该 Job 的 `use!` 作用域结束时才能清理。

Worktree 生命周期覆盖：

```text
Manager work
→ initial confirmed review
→ commit candidate
→ wait publish gate
→ rebase
→ conflict resolution if needed
→ post-rebase confirmed review
→ ff
→ cleanup
```

---

## 四、ManagerJob 结构化程序

```fsharp
let runManagerJob job =
    orchestrator {
        use! worktree = job.Worktree
        let! initial = runGuardedManager job.Manager
        let! candidate = git.commitCurrentTree worktree initial
        return! publishCandidate job candidate
    }
```

不写 `JobStage=Working/Reviewed/Rebasing/Merging`。资源和调用栈已经表达位置。

---

## 五、串行发布门与跨进程锁

工作并行，目标 Git ref 发布串行。

```fsharp
let publishCandidate job candidate =
    orchestrator {
        use! gate = acquireIntegrationGate job.TargetRef
        do! ensureClean job.TargetWorkspace
        return! rebaseReviewAndFastForward job candidate
    }
```

`IntegrationGate` 使用 proper-lockfile 锁定目标 ref，不限于对象内 Task chain：

```fsharp
let acquireIntegrationGate (targetRef: string) =
    orchestrator {
        let lockPath = gitCommonDir + "/publish." + targetRef + ".lock"
        let! lock = properLockfile.acquire lockPath
        return Resource.create lock (fun () -> lock.release())
    }
```

[NORMATIVE]

- lock path 位于 Git common directory，不在工作树。
- 同 repo + 同 branch 得到同一 lock。
- lock 释放发生在 ff-only 与 Published fact 写入之后。
- 崩溃后 stale lock 由 lockfile 自动回收。
- 两个 Runtime 竞争同 repo+同 branch 时，一个等待，一个执行。

## 六、阶段事实与崩溃恢复

不允许恢复"暂停的协程"。持久化业务屏障事实，重启后根据事实 + Git authority 决定下一步。

### 持久化事实

```fsharp
type OrchestratorFact =
    | ManagerJobLinked of {| ManagerSessionId: SessionId; WorktreePath: string; TargetRef: string |}
    | CandidateCreated of {| ManagerSessionId: SessionId; Commit: GitCommit |}
    | PreRebaseReviewConfirmed of {| ManagerSessionId: SessionId; Tree: GitTreeHash |}
    | Rebased of {| ManagerSessionId: SessionId; Candidate: GitCommit; TargetHead: GitCommit |}
    | ConflictDetected of {| ManagerSessionId: SessionId; Diagnostics: string |}
    | PostRebaseReviewConfirmed of {| ManagerSessionId: SessionId; Tree: GitTreeHash |}
    | PublishClaimed of {| ManagerSessionId: SessionId; TargetBranch: string; ExpectedHead: GitCommit |}
    | Published of {| ManagerSessionId: SessionId; Candidate: GitCommit; ResultingTargetHead: GitCommit |}
```

### 恢复逻辑

启动时 Fold NDJSON 获取每个活跃 ManagerJob 的最后事实，然后根据 Git authority 决定：

```text
最后事实
├── Published → 已发布；清理 worktree，从活跃 Map 删除
├── PublishClaimed → 查询 Git target 是否已包含 candidate
│   ├── 是 → 补写 Published（幂等）
│   └── 否 → 重试 publish
├── PostRebaseReviewConfirmed → 继续 publish
├── ConflictDetected → 恢复同一 Manager 解决
├── Rebased → 重新 review
├── PreRebaseReviewConfirmed → rebase
├── CandidateCreated → 等待 review 或走 publish
├── ManagerJobLinked → 刚从 worktree 启动 Manager
└── 无事实 → Job 失败
```

关键设计：

- 不同时恢复"调用栈"或"暂停的异步操作"。
- Journal 只记录已经完成的事实。
- Git 是权威来源：candidate 是否已进入 target 由 `git merge-base --is-ancestor candidate target` 判断。
- 发布后从活跃 Projection 删除，不会随历史积累。

[NORMATIVE] 崩溃恢复判定顺序（Git 优先于 Journal）：

```text
target contains candidate commit → 已发布
worktree HEAD rebased on target + 无有效 witness → 重新 review
存在 valid witness + target 未包含 candidate → 尝试 ff
branch/worktree 不存在 → 从 Git 事实判定 success 或 failure
Journal 只记录不可从 Git 推导的 reviewer witness 与 association
```

---

## 七、Rebase / 冲突 / 复审 / FF

```fsharp
let rec rebaseReviewAndFastForward job candidate =
    orchestrator {
        let! targetHead = git.readHead job.TargetRef

        match! git.rebase candidate targetHead with
        | Rebased rebasedCandidate ->
            do! job.Manager.InvalidateReviewWitness()
            do! job.Manager.PostPromptFireAndForget(
                "The candidate was rebased onto the latest target. " +
                "Re-run tests and obtain two PERFECT verdicts for the rebased tree."
            )
            let! _ = runGuardedManager job.Manager
            let! reviewed = git.commitCurrentTree job.Worktree "post-rebase fixes"
            do! git.fastForward job.TargetRef reviewed
            return Published reviewed

        | Conflict diagnostics ->
            do! job.Manager.PostPromptFireAndForget(
                "Rebase conflicts occurred. Resolve them in the current worktree, " +
                "run verification, then complete the full review loop.\n" + diagnostics
            )
            let! _ = runGuardedManager job.Manager
            let! resolved = git.commitCurrentTree job.Worktree "resolve rebase conflicts"
            return! rebaseReviewAndFastForward job resolved
    }
```

[NORMATIVE]

- 冲突返回同一个 Manager，不启动第二套 conflict resolver。
- 每次重试都读取最新 target head。
- Rebase 后旧 PERFECT 无效。
- FF 前再次 clean check。
- FF 失败作为真实 PublishFailed 返回，不伪装成功。

[NORMATIVE] 冲突返回同一 Manager 的 Run 归属：

```text
Manager 仍 busy 时：通过 nudge 发送冲突诊断（不打断当前 run）。
Manager 已 terminal 时：作为 continuation 发送（不是新 run）。
原 Manager completion 未消费：先消费 completion，再 nudge。
冲突解决后：创建新 candidate（新 commit）。
旧 pre-rebase witness：ManagerJob 级别失效。
Manager 四次死亡后 worktree 保留由 Orchestrator 决定：可放弃、可指派新 Manager。
```

---

## 八、target ref 权威与安全

- 目标 branch 由 `git symbolic-ref` 在 fork 时冻结，不以运行时状态猜测。
- `GetTargetHead` 读取失败 → fail closed，不得 fallback 到 `HEAD` 或 `"main"`。
- Reconcile 写入的 Published commit 是 candidate commit hash，不是 当前 target HEAD。
- `git merge-base --is-ancestor candidate target` 判断 candidate 是否已被包含。
- 不使用字符串前缀比较 commit hash。
- `git merge --ff-only` 检查当前 branch == frozen target branch、当前 target head == expected head。


---

# KISS-N09 — OpenCode Host Adapter、信号与投影管线

Host Adapter 只做协议翻译，不拥有业务流程。

## 零、核心原则

```text
碎片事件（message.updated / part.delta / session.updated）
    ↓ 最早边界丢弃
粗粒度信号（session.status idle/retry, session.deleted）
    ↓ single-flight
SDK API 读取完整消息
    ↓
ReconciledTurn（完整 typed parts / outcome / role / model）
    ↓
纯策略（completion / ReviewGuard / continuation / abort）
```

[NORMATIVE]

- 生产功能**不从碎片事件拼装**业务事实。
- `idle` 只触发一次 connected reconcile loop，不直接代表 terminal 完成。
- `retry` 是唯一写 durable fallback 的入口。
- Unknown（API 尚未显示当前 Run 的 assistant）不产生副作用，不判为 Empty/Failed。
- 同一事件重复到达不重复消费。
- 关联 child terminal 只由 idle 唤醒后 reconcile 确认，不监听 `current_parts`/`message_finalized`/part 增量。

[FORBIDDEN]

- 在 F# 业务层处理 `message.updated` / `message.part.updated` / `part.delta`。
- 在 F# 业务层处理 `session.updated` / `session.diff`。
- 根据 idle 事件内容直接写 `FallbackFailureRecorded`。
- 依赖事件先后顺序或 payload 结构推导业务因果。

---

## 一、关闭官方 Compaction [NORMATIVE]

配置层明确关闭 OpenCode 自动 compaction。若宿主仍可能在 overflow 时自动触发，Adapter 必须拦截或把阈值设置为不可达，保证唯一上下文替换来自本项目 Projection。

不调用官方 compaction agent，不监听 compaction event 驱动业务。

---

## 二、投影管线

```text
Host raw messages
→ Decode typed DTO
→ CanonicalProjection (pure JSON)
→ Companion OfferDelta (side effect, non-blocking)
→ PrefixReplacement (pure transform using current memory)
→ Role Tool Filter (pure)
→ Model messages
```

纯函数：

```fsharp
val decode: obj -> Result<HostMessage list, DecodeError>
val canonicalize: HostMessage list -> JsonValue
val replacePrefix: BlogBase -> BlogText -> JsonValue -> JsonValue
val filterTools: AgentRole -> ToolDefinition list -> ToolDefinition list
val encodeModelMessages: JsonValue -> ModelMessage list
```

副作用只有：

- 尝试启动 Blogger Task；
- 读取当前 B 缓存；
- 读取静态 role definition。

[FORBIDDEN]

- 投影 hook 内等待 Blogger。
- 投影 hook 内 fork 普通 agent。
- 投影 hook 写 Journal。
- 动态 Transform Registry。
- Transform 内 read/git/process。

---

## 三、Session 关联

```fsharp
type SessionAssociation =
    { MainSessionId: string
      BloggerSessionId: string option
      ParentSessionId: string option
      Role: AgentRole }
```

优先存宿主 metadata。只需要 association，不需要 ChildCreated/Owner/Lease 事件流。

启动时：

- 已有 BloggerSessionId：尝试恢复同一个 Blogger。Session 仍存在 → 继续使用，只发送 delta。Session 不存在/被删 → 新建 Y，发送 full reset frame。
- 无 BloggerSessionId 且角色需要伴随：创建 Y 并写 metadata。
- 不每次重启都重建 Blogger（避免无谓冷缓存）。

---

## 四、HostSignal Adapter

唯一允许进入业务层的事件类型：`session.status`（type=idle 或 type=retry）、`session.deleted`。

同一 Session 的 event 来源二选一（插件 `event` Hook 或 global SSE），不双投递。

```fsharp
type HostSignal =
    | SessionIdle of sessionId: SessionId
    | ProviderRetry of
        {| SessionId: SessionId
           Attempt: int
           UserMessageId: MessageId option |}
    | SessionDeleted of sessionId: SessionId
```

Adapter 规则：

- 不是 `session.status`/`session.deleted` → 立即 return（不 resolve properties，不复制 payload，不写日志）。
- `session.status=idle` → `MarkDirty(sessionId)`（触发 single-flight reconcile loop）。
- `session.status=retry` → 提取 sessionId/attempt/messageId（若有）→ `HandleRetrySignal`。
- `session.deleted` → `CleanupOwnedSession(sessionId)`。

```fsharp
let sessionReconcileLoop (sessionId: SessionId) =
    task {
        if isReconciling then return()
        isReconciling <- true
        try
            while dirty do
                dirty <- false
                let! messages = client.session.messages(sessionId)
                let! reconciled = reconcileTurn messages sessionId
                match reconciled with
                | Some turn -> do! applyTurnCompletion turn
                | None -> ()   // Unknown → 不产生副作用
        finally
            isReconciling <- false
            if dirty then
                startReconcileLoop sessionId  // 重跑
    }
```

- Single-flight：同一 session 同时最多一次 reconcile 请求。
- Dirty 标记：idle 信号到达时设 dirty=true，若已 running 则不会额外启动。

[NORMATIVE] Unknown 等待协议：

```text
一次 idle 建立 Dirty latch。
Reconciler 在同一 single-flight 中进行有限次 event-loop yield 后重读；
只要 API snapshot version 有因果进展即可继续；
达到 3 次仍 Unknown，则保持 Dirty，
下一个任何允许的粗粒度 signal 再重试。

不依赖 wall-clock timeout 解决 Unknown 问题。
若 Host API 不保证 idle 后立即可见，
则"等下一次信号"策略不满足 liveness，需要 Host 补 idle-visible 契约。
```

## 五、Terminal 与 A 版

ReconciledTurn 提供完整 assistant：

```fsharp
type ReconciledTurn =
    { SessionId: SessionId
      UserMessageId: MessageId
      AssistantMessageId: MessageId
      AgentRole: AgentRole option
      Directory: string
      Parts: ProviderVisiblePart array
      Outcome: TurnOutcome }  // Completed / Failed / Aborted
```

A 版只含正式 assistant text：

```fsharp
type ARecord =
    { Text: string
      Model: string option
      Error: string option }
```

---

## 六、Fire-and-forget Prompt Adapter

```fsharp
val postPromptFireAndForget:
    sessionId: string ->
    prompt: string ->
    unit
```

实现：

1. 发起宿主 prompt API Task。
2. 不 await assistant completion。
3. Task 异常写结构化诊断。
4. 不进入自建队列。
5. 不触发 fallback；真正模型调用由目标 Session 自己的 Fallback Wrapper 处理。

若宿主 API 在"请求未接受"阶段同步失败，可把工具 ack 标为 failed；不得为此发明 AcceptanceState 平台。

---

## 七、工具注册

静态函数：

```fsharp
let toolsFor role =
    RoleDefinitions.get role
    |> fun d -> d.Tools
    |> List.map ToolDefinitions.get
```

特殊 schema：

- `fork`：agent/prompt/signal。
- `join`：空对象。
- `list`：optional kind。
- `verdict`：enum PERFECT/REVISE。
- `executor`：command + 3 estimate 字段。
- `inspector`：prompts list。

禁止通过 before hook 给普通工具偷偷塞控制字段。

---

## 八、Host 生命周期

```text
plugin start
→ create runtime services
→ register static tools/transforms
→ lazily create association/companion on first projection
→ runtime dispose: cancel owned Tasks, kill PTYs/processes, dispose sessions as policy
```

Hook 回调保持短小：decode、调用纯 transform、post/return。长流程在 Flow 中运行。

---

## 九、日志

日志只记录诊断：

```text
session_id
role
handle_id
operation = fork/join/nudge/project/blog/process/review/publish
result
error
bytes/duration/tree hash
```

不写：

```text
stage
phase
owner
lease
generation
next_action
```

日志不是恢复协议。

---

## 十、测试

1. 官方 compaction 确实不会运行。
2. 投影顺序固定。
3. Blogger Task 不阻塞 projection。
4. role tool filter 静态正确。
5. session association 可恢复同 Blogger（不复用时不发 delta 给空白 Y）。
6. terminal 重复事件不会重复拼 A（Reconcile 是 idempotent）。
7. reasoning 不进 A，但可进 canonical delta。
8. fire-and-forget 不建插件队列。
9. hook 无长等待。
10. 日志不出现旧控制字段。
11. 断开 `message.updated`/`part.updated` 事件后，所有产品 E2E 正常工作。
12. Good message → idle → duplicate idle → no duplicate completion。
13. Unknown（API 尚未显示 assistant）→ 不产生任何副作用。
14. Abort terminal → fallback 不增加失败计数。
15. Single-flight reconcile：连发多次 idle 只触发一次 API 请求。


---

# KISS-N10 — 保姆式实施、测试、迁移与删除

## 零、Rescue Phase（当前残局）

在进入正式 Phase 前，必须先完成残局清理。

### 目标

修复两条根因错误：

1. `HostEventRouter` 没有"一条 assistant terminal 只能消费一次"的概念，把"没有观察到 parts"解释为"空输出"。
2. `runtime-aware child linkage` 把 Journal envelope 的 `RuntimeId` 错当成 child 生命周期所有权。

### 动作

```text
建立 AssistantTurnTracker：terminal 只取出一次，重复 idle 不重复消费
删除 LinkedRuntimeIds / childRuntimes / RuntimeId.create "" 哨兵
恢复重启后复用同一个 child linkage（SunAnalogy：runtime 重启 ≠ session 死亡）
Fallback 只从 session.status=retry 写入，不从 observeIdle 写入
零宽 continuation 不再使用同一套 A/B 失败计数
```

### 出口

```text
Dead_decision_survives_journal_rebuild    绿
Good message + idle + idle → 不产生第二次 completion
session.status=retry → 唯一 FallbackFailureRecorded 入口
```

---

本卷给出从当前 next/重构转向新 Agent DSL 的唯一实施顺序。

---

## 一、工程原则

1. OpenCode only。
2. `next/` 不 import 旧生产。
3. 不兼容旧 `.wanxiangshu.ndjson` 控制格式。
4. 先写能编译的 DSL 骨架，再接 Host。
5. 每个 Phase 只引入一个真实复杂点。
6. 新旧双轨期允许代码暂时增加；每个新行为验收后立即删对应旧路径。

建议目录：

```text
next/
  Kernel/
    Flow.fs
    AsyncResource.fs
    Parallel.fs
  Domain/
    Roles.fs
    ARecord.fs
    BRecord.fs
    Fallback.fs
    ReviewGuard.fs
  Companion/
    CanonicalProjection.fs
    JsonDelta.fs
    BlogDocument.fs
    CompanionProgram.fs
    PrefixProjection.fs
  Forking/
    Handle.fs
    CompletionMailbox.fs
    ForkProgram.fs
    JoinProgram.fs
    ListProjection.fs
  Process/
    ExecutorRequest.fs
    ProcessResource.fs
    OutputPump.fs
    OutputSummary.fs
    PtyResource.fs
  Orchestrator/
    WorktreeResource.fs
    ManagerJob.fs
    PublishProgram.fs
  OpenCode/
    Codec.fs
    ProjectionHook.fs
    SessionAssociation.fs
    PromptAdapter.fs
    ToolDefinitions.fs
    Plugin.fs

tests-next/
  GuideContract/
  Companion/
  ForkJoin/
  Process/
  Fallback/
  ReviewGuard/
  Orchestrator/
  HostContract/
```

---

## 二、Phase 0 — GuideContract 真编译

### 目标

把文档中的核心代码形状编译起来，不接 OpenCode。

### 必做

- `Flow`、Builder、`Using`、`parallel`。
- `agent/companion/process/review/orchestrator` 类型别名。
- fake Script 与 fake resource。
- 尾递归 Fallback。
- 双 PERFECT 递归。

### 出口

- 示例主程序能编译。
- 无 Flow AST。
- 无 Stage/Phase/Lease/Owner/Generation 类型。
- `use!` 在成功/失败/取消都 await dispose。

---

## 三、Phase 1 — Canonical Projection 与 Blogger

### 步骤

1. 从 Host fixture 解码 typed message。
2. 纯函数 canonical JSON。
3. 稳定 JSON delta。
4. fake Blogger Task。
5. busy skip；成功后推进 BlogBase。
6. B 只收 assistant output。
7. PrefixReplacementEnabled。
8. X prefix projection。
9. Y self-rebase。
10. 接真实廉价模型。

### 出口

- 三次 busy skip 后只补一次完整 delta。
- X 无等待投影。
- Y rebase 后旧 B 不再属于 B 输出。
- 官方 compaction 尚未接，但纯投影测试闭合。

---

## 四、Phase 2 — ForkRuntime

### 步骤

1. HandleId 生成与碰撞检查。
2. ConcurrentDictionary handles。
3. Channel completion mailbox。
4. fake Host child。
5. listener-before-send。
6. fork new role。
7. nudge existing ID fire-and-forget。
8. join any。
9. list projection。
10. parent cancel。

### 出口

- 极快 completion 不丢。
- 无自建 prompt queue。
- join 无 active 返回 NothingToJoin。
- 不出现 SubsessionActor。

---

## 五、Phase 3 — 静态角色与工具

### 步骤

1. RoleDefinitions 静态表。
2. Manager only fork/join/list。
3. Orchestrator only fork/join。
4. Coder 普通文件工具。
5. Coder one-shot Inspector。
6. Inspector Executor Tool。
7. Browser/Meditator/Reviewer 权限。
8. Blogger/Executor Agent no tools。

### 出口

- capability snapshot 测试逐角色比对。
- fuzzy 工具完全不注册。
- Manager 无普通工具。

---

## 六、Phase 4 — Process 与 Executor

### 步骤

1. Spawn 前安装 pumps。
2. 3× estimate 唯一 timer。
3. timeout kill entire tree。
4. kill 后无第二 timeout。
5. medium 无 gate。
6. large global semaphore 1。
7. byte counting。
8. spool 文件。
9. 200KB chunk。
10. Executor Agent map/reduce 摘要。

### 出口

- 所有死锁/大输出/kill 测试通过。
- huge estimate 未被 clamp。
- 无 Python/JS 特殊 execute 工具。

---

## 七、Phase 5 — PTY

### 步骤

1. `fork(agent=pty,prompt=...)` create。
2. ID + prompt write。
3. ID + empty prompt read。
4. ID + signal enum。
5. read/exit completion。
6. list 合并展示。
7. parent cancel kill。

### 出口

- 无魔法 signal 字符串。
- PTY 内部未伪装 Agent Session。

---

## 八、Phase 6 — Session Fallback

### 步骤

1. Side A/B。
2. Failures 计数。
3. A retry。
4. permanent B switch。
5. B retry。
6. fourth failure SessionDead。
7. cancellation excluded。
8. Role metadata/config 接线。

### 出口

- 表驱动的 0～4 failure 轨迹全部通过。
- 仓库无 FallbackPhase/RemainingModels/Governor。

---

## 九、Phase 7 — ReviewGuard

### 步骤

1. verdict schema。
2. REVISE immediate。
3. first PERFECT tool response。
4. second PERFECT bind tree hash。
5. terminal no verdict nudge。
6. manager terminal guard。
7. tree change invalidation。
8. coder revision loop E2E。

### 出口

- 一次 PERFECT 绝不能放行。
- Reviewer 忘 verdict 自动继续同一 session。
- Manager 无 review witness 绝不能结束。
- 不读取 todo。

---

## 十、Phase 8 — OpenCode Adapter（信号接入）

### 步骤

1. 关闭官方 compaction。
2. 建立 HostSignal adapter：只保留 `session.status`（idle/retry）和 `session.deleted` 三信号。
3. 建立 SessionReconciler：single-flight，从 SDK API 读取完整消息。
4. 删除所有 `message.updated`/`part.updated`/`part.delta` 业务依赖。
5. 接 projection hook（`experimental.chat.messages.transform`）。
6. association metadata：重启后复用同一 Blogger（不发 delta 给空白 Y）。
7. A extraction 来自 reconcile 后的 ReconciledTurn。
8. re-fire-and-forget prompt。
9. static tools。
10. plugin disposal。

### 出口

- 断开 `message.updated`/`part.updated` 事件后所有产品 E2E 正常工作
- child completion 通过 reconcile 确认，不通过 SSE listener
- 日志不出现旧控制字段
- Browser/Meditator/Reviewer 权限
- Blogger/Executor Agent no tools

---

## 十一、Phase 9 — Orchestrator

### 步骤

1. clean gate。
2. single Manager worktree。
3. auto ManagerGuard。
4. initial review。
5. candidate commit。
6. integration semaphore。
7. rebase。
8. post-rebase review。
9. ff。
10. two managers parallel work / serial publish。
11. conflict return same manager。

### 出口

- 只有 ff 成功才 join published。
- 第二任务基于最新 target rebase。
- rebase 后双 PERFECT。
- 无 DAG/Scheduler/HTTP 控制面。

---

## 十二、Phase 10 — 删除旧实现

按功能一刀切删除：

```text
todowrite SSOT / task 强制报告
select_methodology
通用 Nudge 与 idle proposals
SubsessionActor 与同步 subagent 工具
submit_review / return_reviewer 旧协议
Fallback 状态机/扫描链
ContextBudget/官方 compaction 协调
fuzzy_find/fuzzy_grep/fuzzy_continue
万象阵 DAG/Scheduler/Coordinator/HTTP 控制面
旧 PTY 多工具表面
旧 executor 多层 timeout
Stage/Phase/Lease/Owner/Generation 字段
```

README 改写为新产品语义；旧文档移入 `docs-legacy/` 或直接删除，不让两套规范同时有效。

---

## 十三、测试金字塔

### 13.1 纯函数

- Canonical JSON。
- JsonDelta。
- Prefix replacement。
- Role capability。
- Fallback transition function。
- Review witness/tree invalidation。

### 13.2 资源契约

- Flow Using。
- Completion Channel。
- Process pumps/kill。
- Large semaphore。
- Worktree cleanup。

### 13.3 Fake Host 轨迹

- Blogger busy skip。
- nudge running child。
- terminal without verdict。
- four failures。
- manager guard。

### 13.4 OpenCode E2E

- 长 session 真正不触发官方 compaction。
- B 替换前缀后继续正确工作。
- Manager 并行 fork/join。
- Coder parallel inspector。
- 大命令摘要。
- PTY。
- reviewer REVISE/双 PERFECT。
- orchestrator 两 worktree 串行发布。

### 13.5 测试执行器（tests-next runner）

[NORMATIVE] `tests-next/runner.js` 是纯 Fable 测试的执行器，设计原则：

```text
import + await 直接运行
`__resetAssertionTimeout()` 心跳重置本地定时器
心跳超时 → test 标记为失败，abandon 底层 async（不主动 kill）
串行执行
无 IPC / worker 协议
无 spawn ledger 参与
```

**旧模型 vs 新模型**：

| | 旧 | 新 |
|---|---|---|
| 执行 | `fork()` worker 进程 | 同进程 `import` + `await` |
| 心跳机制 | IPC heartbeat → 父进程 | `globalThis.__resetAssertionTimeout` → 在 `Promise.race` 中重置本地 timer |
| 超时处理 | `terminateTree()` → SIGTERM→SIGKILL | reject Promise → 忘记测试（不再等待或杀死） |
| 进程跟踪 | `spawn-ledger` + PID 重用保护 | 无 |
| 清理 | 确保子进程组全部终止 | 不需要——Node.js 进程退出自动清理一切 |

**为什么不再需要进程隔离**：

- 超时后不需要主动终止异步操作——Node.js 进程退出会清理一切。
- 进程隔离（`fork()` + `terminateTree`）引入心跳协议、进程跟踪、PID 重用防范等复杂度，但解决的是根本不需要解决的问题。
- 同进程串行执行 + Promise.race 已足够：测试点失败就是失败，不关心底层 async 是否还在跑。
- 心跳保留但语义简化：不是用来防止被杀，而是用来决定测试是否失败。

**设计**：

```text
discoverTestExports(file):
    import(file) → 枚举 export 中的 async function

runTest(file, name, timeoutMs=1000):
    import(file) → mod[name]()
    Heartbeat timer fires → reject failPromise
    Promise.race([testPromise, failPromise])
    heartbeat 胜出 → 抛 TIMEOUT 错误，test 失败
    test 胜出 → clear heartbeat timer
    不调 terminateTree，不 send SIGKILL，不清理

__resetAssertionTimeout():
    全局函数，测试调用以重置 heartbeat timer
```

[NORMATIVE]

- 每个 test 只 `import` 一次（ESM module cache 确保后续同文件 import 为同一引用）。
- Test function 必须返回 Promise（Fable 编译的 async test 天然满足）。
- 1s 是硬上限，不可配置；`TEST_SILENCE_MS`/`TEST_ABSOLUTE_MS` 环境变量不再存在。
- `__resetAssertionTimeout()` 不再通过 IPC 发送 heartbeat 到父进程，改为重置同进程的本地 timer。
- 不再 fork worker 进程，不再 import `process-lifecycle.js`、`spawn-ledger.js`。
- `worker.js` 和 `fixtures/hanging-test.js` 不再需要（保留简化的 fixture 供测试框架验证 timeout 行为）。

---

## 十四、代码门禁

CI 搜索并拒绝新代码中的：

```text
Stage
Phase
Lease
Owner
Generation
Coordinator
Governor
Registry  // 仅允许静态 ToolDefinitions/RoleDefinitions 与资源字典的具体命名
NextAction
NudgeState
FallbackState
ReviewStateMachine
SubsessionActor
```

例外必须在 allowlist 中解释为真实外部概念。

主程序审查门禁：

- 主要函数缩进不超过 3 层。
- 一次流程可在一个文件连续阅读。
- `match/while/rec/use!` 直接表现业务顺序。
- Host API、Process API、Git API 藏在动词内部。
- 没有“调用后 Refresh”语法；动词返回时值已可用。

---

## 十五、稳定性门禁

```text
P0 全套动态事件 stagger 启动：第一个 canary 立即启动，canary N 等待 N−1 输出精确的 `[setupScenario] ready` 随后立即启动（不等待前一个 canary 结束；ready 前退出或 ready 超时 = 该 canary 失败，可以释放启动门继续收集后续诊断，但整轮不能通过）
因果 Watchdog：2 秒 scenario-local，只认因果进展，不认 SSE 噪音
Release gate 恰好运行 3 轮，每轮所有场景并行
任一场景失败仍收割全部场景结果，再使本轮失败
泄漏检查：每个 scenario dispose 后检查 PID / port / session / worktree / 临时目录
```

Watchdog 有效心跳：
```text
  - blocking expectation 消费
  - API reconcile 发现新 terminal
  - Tool 完成（fork/join/list/verdict）
  - PTY/Process 退出
  - 显式因果 barrier
```

Watchdog 无效心跳：
```text
  - 任意 SSE 到达
  - session-created 事件（除非对应显式 barrier）
  - Blogger/Title/Compaction 等后台请求
```

[NORMATIVE] Watchdog 边界：

```text
Watchdog 只终止 test scenario，不终止生产资源。
被测动作拥有自己的 SSOT deadline 时，Watchdog deadline 必须大于该 deadline + cleanup allowance。
没有产品 deadline 的 LLM 请求，Watchdog 只能依据 mock expectation 因果进展，
不可用于真实 provider release gate。
```

## 十六、不修改 OpenCode 原则

除非修复阻止迁移测试运行的 host 兼容 bug，否则：

```text
禁止修改 opencode 本体
禁止增加新的插件 Hook
生产功能只在以下现有边界内工作：
  chat.message / experimental.chat.messages.transform
  tool.definition / tool.execute.before / tool.execute.after
  experimental.session.compacting / experimental.compaction.autocontinue
  event（仅用于 idle/retry/deleted 三信号）
  SDK client.session.* / prompt_async / session.messages
```

## 十七、完成定义

只有同时满足以下条件才可宣布新版完成：

1. 新架构所有行为有真实 OpenCode E2E。
2. 官方 compaction 关闭且 B 投影替换经长上下文验证。
3. fork/join/list 与 PTY E2E 稳定。
4. 四失败 Fallback 每角色生效。
5. Reviewer 双 PERFECT 与无 verdict guard 生效。
6. Orchestrator 可并行两个 Manager 并串行 rebase/review/ff。
7. 旧同步工具、todo、nudge、万象阵代码已删除，而非标 deprecated。
8. README 与唯一规范只描述新架构。
9. 搜索不到旧控制状态词，或每个命中均是历史文档/测试反例。
10. 生产主流程肉眼可证明，没有第二调度路径。


---

