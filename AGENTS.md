# 总体结论与 Flow-first 架构总纲

## 一、四个共同根因

这 7 个问题表面上分散在 fallback、tool hook、context budget、nudge、review、logging 六个区域，实际上共同根因只有四个：

1. **系统没有可靠区分"真人消息"和"系统合成消息"**，而是依赖零宽字符、文本、时间戳、最近一条消息等启发式判断。
2. **会话没有明确的"当前轮次、取消代次、自动续跑代次、事件所有者"**，所以 Esc、fallback、compaction、nudge 会抢着控制同一个 session。
3. **model、agent、original task 等上下文没有绑定到具体的人类轮次**，而是从"最近看到的某条消息"临时反推，必然读到旧值。
4. **warn、warn_tdd 等控制字段只有分散的 schema 注入和 hook 校验，没有唯一、强制、不可绕过的执行边界。**

当前代码已经有不错的纯状态机、事件日志、fold、nudge snapshot 和 review task 折叠结构，但这些结构还没有形成一个真正的单一事实源。尤其是 fallback 注入虽然已经进入事件日志，消费者仍然大量依赖消息和时间推断。

真正要删除的不是某个零宽字符，而是整个系统对"根据文本、时间和最近消息猜测状态"的依赖。只要 provenance、generation、owner、routing context 和 durable projection 建立起来，这 7 个问题会一起消失。

## 二、解决方案范式：Flow-based Event-Sourced Session Loop

不再围绕 7 个 bug 分别打补丁。采用 FLOW.md 提出的 Flow-first 架构作为统一实施范式：

> **单一反馈流 + 提交感知的 `scanCommit` 事务 + 声明式 effect 流**

核心思路：把当前散落在多个异步 handler、mutable flag、callback 中的运行时状态，收敛为一条唯一、可组合、可观测的时间管线。已有纯 fold、纯 decision 和事件状态结构天然兼容此范式——Flow 化最有价值的地方，正是把这些纯函数串成一条管线，而不是重新写一套新的大状态机。

Flow 是组合语言；State + Event 是语义；`scanCommit` 是带持久提交边界的状态积分；Channel 是反馈入口；Effect Flow 是并发边界；generation/lease 是正确性证明；Event Log 是持久事实。

## 三、核心管线

```text
Host events ──────┐
Tool results ─────┤
Timers ───────────┤
Effect results ───┘
        │
        ▼
    Channel<SessionInput>
        │
        ▼
 normalize / dedup / assign sequence
        │
        ▼
    scanCommit(SessionKernel.step, initialState, journal)
        │
        ├── candidate State / Events / Effects
        ├── append events to the workspace journal
        └── only after append: publish State / Events / Effects
                   │
                   ▼
           Effect Flow
           flatMapMerge(maxConcurrency)
           + lease validation at execution boundary
                   │
                   ▼
          EffectResult → Channel<SessionInput>
```

它本质上是单写者，但代码表达形式是 Flow，而不是 Actor handler。

## 四、Step 类型

核心不是 `State -> State`，而是 `Step`：

```fsharp
type StepCandidate =
    { State: SessionState
      Events: DomainEvent list
      Effects: Effect list }

val step:
    SessionState ->
    SessionInput ->
    StepCandidate
```

`step` 只在一个输入快照上生成候选值；它不得把候选值当作已提交状态。整个 Session Kernel 的提交入口是：

```fsharp
inputs
|> AsyncEnumerable.scanCommit
    (fun state input -> SessionKernel.step state input)
    initialState
    journal
```

`scan`/`runningFold` 可以用于只读重放或 UI 派生，但不得作为生产提交管线；普通
`scan` 先发布 state 再 append 的写法在本架构中是非法的。

## 五、串行提交区与并发 Effect 区

副作用是最容易写错的地方。

错误版本（禁止）：

“先做普通状态 fold、再异步 `Host.SendPrompt`”的组合（无论具体算子名称为何）均为
禁止的提交管线。

因为 `mapAsync` 如果顺序等待可能阻塞 Abort；如果并发执行可能乱序；如果取消枚举物理副作用状态不明；可能先执行副作用后写事件日志。

### 串行提交区

```text
Input
→ step(snapshot) → candidate State/Events/Effects
→ append all candidate Events in one journal transaction
→ publish accumulator/SessionState and committed Events
→ emit Effects
```

规范伪代码：

```fsharp
let committedSteps =
    inputs
    |> scanCommit (fun state input ->
        SessionKernel.step state input) initialState journal
```

`scanCommit` 对每个输入在 session mailbox 中串行执行：先计算 candidate，再以一次
事务追加 candidate.Events；append 成功后才原子发布 accumulator、`SessionState`、
committed Events 和 Effects。append 失败时不得发布任何 candidate，也不得发出
candidate Effects；session 进入 poisoned 状态，后续 synthetic prompt 一律阻断（仅允许
记录故障、关闭或显式人工恢复事件）。提交序号由 journal 分配，发布值必须携带该序号。

生产拓扑固定为：每个 workspace 只有一个 journal；每个 path 只有一个进程内
`JournalWriter`，跨进程通过排他锁串行化 append。不得按 session、房间或投影各自创建
独立事实日志。恢复按 journal 的提交序号逐行重放，投影仅从已提交事件重建。

### 并发 Effect 区

```text
Effect Flow
→ flatMapMerge(maxConcurrency)
→ Host I/O
→ EffectResult
→ 写回 Input Channel
```

```fsharp
let effects =
    committedSteps
    |> collect (fun step -> step.Effects |> AsyncEnumerable.ofSeq)

let effectResults =
    effects
    |> flatMapMerge maxConcurrency executeEffect

effectResults
|> iterAsync inbox.WriteAsync
```

最关键的不变量：

> **`EffectRunner` 不修改 SessionState，只把结果写回输入流。**

Effect 执行前必须在 session mailbox 中重新校验 lease（参见 PRD-01 ContinuationLease）。
只有 `scanCommit` 已发布的 Effects 才能进入 Effect Flow。

## 六、取消的双层防线

### 计算层取消

```kotlin
currentSessionEpoch
    .flatMapLatest { epoch ->
        runCurrentQueries(epoch)
    }
```

Abort 或新 turn 更新 epoch，自动取消旧计算。适用于 transcript 获取、model/context-limit 查询等可安全废弃的操作。

### 领域层取消

```fsharp
UserAbortObserved
→ reducer:
    CancelGeneration + 1
    Lifecycle = Cancelled
    ActiveLease = invalidated
```

所有 effect result：

```fsharp
effectResults
|> filter (fun result ->
    result.Context.Generation = current.Generation
    && result.Context.CancelGeneration = current.CancelGeneration)
```

最好仍在 `step` 内进行 filter，以便把迟到结果记录成 `StaleEffectIgnored` 而不是静默丢弃。

`SessionEpoch` 与 `CausalityContext` 是不同身份层：SessionEpoch 标识会话生命周期，
包括 `sessionGeneration`；CausalityContext 绑定一次输入/效果的因果链，包括
`humanTurnID`、`sessionGeneration`、`cancelGeneration`、`contextGeneration`、
`continuationID` 与 `requestID`。没有 owner 的 session-start observation 可以在
`CausalityContext` 中以 `owner = None`、`humanTurnID = None` 进入；必须通过显式
`ObservationOptions(allowUnowned = true, allowSessionStart = true)`，而非伪造 owner
或轮次。普通 observation 仍拒绝缺失身份。

compaction 只递增 `contextGeneration`；它不得递增 `sessionGeneration`。只有新会话或
真正重启才产生新的 SessionEpoch/sessionGeneration；新真人轮次只递增其 turn/cancel
代次。每个异步结果必须回带其
CausalityContext，过期结果只记录 `StaleEffectIgnored`。

双保险：

```text
flatMapLatest：减少无用工作
epoch 校验：保证领域正确性
```

## 七、策略模块的 Flow 化

fallback、budget、review、nudge 可以组合成一个高阶决策流：

```fsharp
let policies =
    [ FallbackPolicy.tryPlan
      CompactionPolicy.tryPlan
      ContextBudgetPolicy.tryPlan
      ReviewPolicy.tryPlan
      TodoPolicy.tryPlan ]

let plan snapshot =
    policies
    |> List.choose (fun policy -> policy snapshot)
    |> List.sortBy Candidate.priority
    |> List.tryHead
```

推荐在**一个 snapshot 上同步调用所有纯 policy**，而不是创建五条真正独立调度的异步流。因为这些计算都很快，不值得引入流之间的采样时刻问题。

```fsharp
stateFlow |> map plan // UI/diagnostic preview only
```

而不是：

```fsharp
combine fallbackFlow nudgeFlow reviewFlow
```

执行计划必须在 `step` 内基于同一个 `SessionState` snapshot 同步计算，并作为候选
Events/Effects 的一部分提交。`stateFlow |> map plan` 只产生 UI/诊断预览，绝不能发
送 prompt、取得 lease 或改变状态；因此预览与执行不存在第二事实源。

## 八、异步查询生命周期

收到新 human turn 后需要异步获取 transcript、当前 model、context limit、token usage。这种查询非常适合 `flatMapLatest`：

```kotlin
humanTurns
    .flatMapLatest { turn ->
        flow {
            val transcript = host.fetchTranscript(turn.sessionId)
            emit(TranscriptFetched(turn.epoch, transcript))
        }
    }
```

新人类轮次到达，旧 transcript 查询自动取消。但结果仍然携带 `humanTurnId`、`sessionGeneration`、`cancelGeneration`、`requestId`。即使底层请求不能真正取消，返回结果进入 reducer 时也会因为 epoch 过期而被忽略。

`flatMapLatest` **只用于可安全废弃的查询**。不适用于已经开始的、必须记录物理结果的 Host prompt dispatch。

## 九、算子适用范围

| 算子 | 适用场景 | 禁止场景 |
| :--- | :--- | :--- |
| `scanCommit` | 唯一生产状态积分与 journal 提交 Input Flow → SessionState | 普通 `scan`/`runningFold` 作为提交管线 |
| `flatMapLatest` | transcript 获取、model/context-limit 查询、新 generation 后旧查询无意义的操作、UI 派生信息 | 已开始且必须记录物理结果的 Host prompt dispatch |
| `flatMapMerge` | 相互独立的外部读取（fetch transcript / fetch provider info / fetch model info） | 领域输入（必须在 scanCommit 前 ordered） |
| `debounce` | 高频 token usage 更新、progress display、非关键 nudge 重新评估 | Abort、lease、dispatch、settlement 等领域事实 |
| `conflate` | telemetry、UI progress、"只关心最新值"的观察量 | human message、Abort、continuation dispatch、tool result、event log append |
| `distinctUntilChanged` | 候选动作去重（基于强类型 identity：humanTurnId+generation+purpose+anchor） | 以内容 hash 作为缓存正确性或 key |

## 十、Flow 与领域语义的结合原则

FLOW.md 的核心修正结论：

> 可以用 Flow/`IAsyncEnumerable` 高阶算子取代目前手写的控制流状态机、回调和 mutable flags；但生产提交必须保留一个强类型的纯 `step` 与 `scanCommit` 事务作为领域语义核心。

```text
Flow 是组合语言
State + Event 是语义
scanCommit 是提交感知的状态积分
Channel 是反馈入口
Effect Flow 是并发边界
generation/lease 是正确性证明
Event Log 是持久事实
```

**Flow 可以表达状态机，但不能替代 continuation ID、generation、provenance 等领域身份。** 这两者应该结合，而不是二选一。

当前仓库已经有大量纯 fold、纯 decision 和事件状态结构，但运行时仍散落着独立 mutable 状态；Flow 化最有价值的地方，正是把这些已有纯函数串成一条唯一、可组合、可观测的时间管线。

## 十一、最重要的取舍

这次修复中应坚持三个原则：

* **宁可少自动继续一次，也不能违反用户 Esc。**
* **宁可少发一次 nudge，也不能在 compaction/fallback 后重复抢控制权。**
* **宁可因状态缺失阻止 review continuation，也不能让 LLM 在没有原始任务的情况下猜。**

### 已确认的行为边界

* investigator agent 与其他 agent 一样必须收到 CAPS；不得因 investigator 身份跳过注入。
* 一回合只成功调用单个工具时，下一次可执行计划应包含“并行调用工具”的单一提示；不重复发多条提示。
* 对 `warn`、`warn_tdd`、`warn_reuse` 及 todo 长度，schema 负责强调要求；缺失或过短不作为硬拒绝，工具返回时通过一次结构化批评事件提醒模型。
* 对话开始时不得凭空发 emergency todo prompt；只有已观测到足够上下文且预算/owner 条件满足时才可计划。
* continuation lease 已 `Settled` 后不得再次生成 fallback nudge；settlement 是该 lease 的终态事实，而不是可重试的 idle 观察。

## 十二、文件索引

| 文件 | 内容 |
| :--- | :--- |
| PRD-00 | 总体结论 + Flow-first 架构总纲（本文） |
| PRD-01 | 系统约束：Flow 八条 + 领域十条不变量 + 跨问题统一对象 |
| PRD-02 | 问题 1：Esc、Abort 与 fallback auto-continue |
| PRD-03 | 问题 2：控制字段与执行网关 |
| PRD-04 | 问题 3：context budget 与 todowrite 触发 |
| PRD-05 | 问题 4：DEBUG 与结构化日志 |
| PRD-06 | 问题 5：compaction 与 nudge owner 仲裁 |
| PRD-07 | 问题 6：model routing 与人类轮次路由 |
| PRD-08 | 问题 7：review nudge 原始任务 |
| PRD-09 | 六个核心投影 |
| PRD-10 | 兼容旧事件日志 |
| PRD-11 | 实施顺序（Flow-first） |
| PRD-12 | 最终验收标准 |
| PRD-20 | Fable/JS 互操作与深度实施避坑 |
| PRD-21 | 六项行为修复方案 |
| PRD-22 | 子会话 Actor 架构 |
| PRD-23 | Message Transform 钩子重构：TransformState 与引用稳定 |
| PRD-24 | E2E 测试设计要求：Flow-first 验证基础设施 |
# 系统约束：Flow 约束 + 领域不变量 + 跨问题统一对象

## 一、Flow 管线不可破坏的约束

只要遵守这些约束，Flow 可以比 Actor 更"明显正确"。

### 约束 1：提交感知状态 fold 只能有一个 collector

不能对冷流 collect 两次，否则可能重复执行整个流程或重复副作用。生产 collector
必须是 `scanCommit`：candidate state/events/effects 先生成并 append，成功后才发布
accumulator、`SessionState` 与 effects。普通 `scan`/`runningFold` 仅可用于重放或
只读 UI 派生，不能作为提交管线。

### 约束 2：领域输入在 `scanCommit` 前不能 unordered merge

多 Host 来源必须先进入有序 per-session mailbox（参见 PRD-00 核心管线），分配 sequence 后再
进入 `scanCommit`。

### 约束 3：`step` 中不能执行慢 Host I/O

否则整个提交流被背压，Abort 也被堵住。慢 I/O 交给并发 Effect 区（`flatMapMerge`）。

### 约束 4：副作用前先持久化 intent

`ContinuationPlanned` append 成功后才允许 dispatch；更具体地，必须先以一次事件写入
原子地 claim lease（`DispatchClaimed`），随后才可进行物理 dispatch。append 失败时
session 进入 poisoned，禁止合成 prompt。

### 约束 5：取消必须既是控制信号，也是领域事件

`CancellationToken` 负责停止本地工作；`UserAbortObserved` 和 generation 负责业务正确性。

### 约束 6：不得对领域事实使用 `conflate`、`debounce` 或 `drop`

领域事实包括：human message、Abort、continuation dispatch、tool result、event log append、lease、settlement。

### 约束 7：`flatMapLatest` 只用于可安全废弃的查询

不能用它表示"Host 一定没收到 prompt"。已开始且必须记录物理结果的 dispatch 不适用。

### 约束 8：所有并发 effect 返回结果都必须回到唯一提交 fold

`EffectRunner` 不修改 SessionState，只把结果写回输入 Channel，由 `scanCommit` 统一积分。

### 约束 9：执行计划只能在 `step` 内产生

每个输入只读取一个 `SessionState` snapshot；所有 policy 在该 `step` 中同步仲裁并
生成 candidate Events/Effects。`stateFlow |> map plan` 只允许作为 UI/诊断预览，不得
取得 lease、发送 prompt 或成为第二事实源。

### 约束 10：workspace 只有一个事实 journal

一个 workspace 使用一个 journal；每个 path 由一个进程内 `JournalWriter` 实例负责，
并以 interprocess lock 保护跨进程 append。不得按 session 或 projection 分裂 journal。

## 二、领域不变量

以下规则必须写进架构设计和测试，否则修完一个还会从另一个入口复发。

### 不变量 1：Esc 是最高优先级、粘性的用户意图

用户按 Esc 后：
* 当前人类轮次立即进入 `Cancelled`。
* 已排队但尚未发送的 fallback、nudge、review continuation 全部失效。
* 已经发送但尚未返回的系统 continuation，其迟到事件不得重新激活会话。
* 只有 Esc 之后一条**明确确认是真人发出的新消息**，才能开启新轮次。

任何零宽消息、nudge、compaction、title、fallback continuation 都不能解除 Cancelled。

### 不变量 2：每条消息必须有来源

消息来源至少要分成：Human、FallbackContinuation、TodoNudge、ReviewNudge、ContextBudgetNudge、Compaction、TitleGeneration、Subagent、UnknownLegacy。

不能再只用 `role=user` 判断"这是用户消息"。

### 不变量 3：每个 session 同一时刻只能有一个 continuation owner

owner 必须使用以下唯一 canonical enum；文档和事件不得另造同义 owner：
`UserAbort | FallbackRecovery | Compaction | ContextBudget | ReviewLoop | TodoNudge |
RunnerReminder | None`。

优先级固定为：
1. UserAbort
2. FallbackRecovery
3. Compaction
4. ContextBudget
5. ReviewLoop
6. TodoNudge
7. RunnerReminder
8. None

| canonical owner | 可创建的 synthetic work |
| :--- | :--- |
| `UserAbort` | 无；只取消并使其他 lease 失效 |
| `FallbackRecovery` | fallback continuation |
| `Compaction` | compaction autocontinue |
| `ContextBudget` | context-budget nudge |
| `ReviewLoop` | review continuation |
| `TodoNudge` | todo 提示 |
| `RunnerReminder` | runner reminder |
| `None` | 无 synthetic work |

高优先级 owner 未释放前，低优先级 owner 不得发送 prompt。

### 不变量 4：所有异步行为必须绑定代次

至少需要：`humanTurnId`、`sessionGeneration`、`cancelGeneration`、`continuationId`、`contextGeneration`。

迟到回调只要 generation 不匹配，就只能记录，不得改变当前状态。

### 不变量 5：model/agent 属于轮次，不属于 session 全局

模型选择必须绑定到当前真人消息或当前 fallback attempt。不能用"session 最近注入过的模型"覆盖未来所有轮次。

### 不变量 6：控制字段在唯一入口软合规、硬门禁

`warn`、`warn_tdd`、`warn_reuse` 必须在 schema 中强调声明，并由唯一执行入口提取到
ControlEnvelope、从下游业务参数中删除、下游只拿净化后新对象。schema 缺失或长度
不足属于软合规：工具不得因此硬拒绝；工具返回时追加一次 stern compliance event
（违反时在参数规范化后恰好一次），批评模型。硬门禁只保留 malformed business args、security/permission denial、parse
failure，以及净化后仍泄漏控制字段。`amend` 字段已在代码中移除，不作为控制字段。

### 不变量 7：context budget 不允许静默失效

即使拿不到 provider limit 或精确 tokenizer，也必须进入明确的降级模式，而不是让 `MaxInputTokens=0` 后什么都不做。

### 不变量 8：review loop 活跃时，task 不能为空

只要系统判定 review loop 是 Active，生成任何 review continuation 时都必须携带完整原始任务。缺失任务属于状态损坏，应当阻止 nudge。

### 不变量 9：compaction/fallback 结束不等于人类轮次自然结束

只有真正的人类轮次完成事件，才有资格触发 todo/review nudge。

### 不变量 10：默认运行不得污染 stdout

正常 capability 缺失、降级、重试、缓存未命中都不应直接打印 `DEBUG:`。

### 不变量 11：BUGS 行为必须可观测且不改变硬门禁

investigator agent 必须接收 CAPS；单工具成功的回合只生成一个“并行调用工具”提示。
conversation start 在上下文尚未充分观测时不得生成 emergency todo prompt；已 Settled 的
fallback lease 不得再生成 fallback nudge。`warn`/`warn_tdd`/`warn_reuse` 的缺失和
todo 长度不足按不变量 6 的软合规规则处理，在工具返回时记录一次批评事件，不拒绝
本次合法工具调用。

## 三、跨问题统一对象

以下对象正式引入，作为 Flow 管线中传递的强类型值。

### 3.0 SessionEpoch 与 CausalityContext

`SessionEpoch` 是会话身份层，至少包含 `sessionID`、`sessionGeneration` 与
lifecycle。session 创建或真正开始新会话时递增 `sessionGeneration`；compaction 不得
改变它。

`CausalityContext` 是事件因果层，包含 `sessionEpoch`、可选 `humanTurnID`、
`cancelGeneration`、`contextGeneration`、可选 `continuationID`、`requestID` 及
canonical owner。session-start 或无 owner 的观察只有在
`ObservationOptions(allowUnowned = true, allowSessionStart = true)` 下才合法，且
必须显式记录 `owner = None` 与缺失的 turn；不得伪造身份。普通 observation 缺少所需
身份必须进入 parse/validation hard gate。

compaction 递增 `contextGeneration`，不递增 `sessionGeneration`；新真人轮次递增
相应 turn/cancel 代次。任何异步回调都必须携带 CausalityContext，过期结果只能写入
`StaleEffectIgnored`。

### 3.1 TurnIdentity

包含：session ID、human turn ID、session generation、cancel generation、user message ID。

用于阻止旧 fallback、旧 nudge 和旧 model observation 污染新轮次。所有进入
`scanCommit` 的 Input 和所有离开 `flatMapMerge` 的 EffectResult 都必须携带
TurnIdentity；仅 session-start/unowned observation 按 3.0 的显式 options 使用
`SessionEpoch + CausalityContext`，不伪造 TurnIdentity。

### 3.2 ContinuationLease

包含：continuation ID、canonical owner、turn identity、route、status、issuedAt、
invalidation reason、claim sequence。状态必须遵循：
`Pending → DispatchClaimed → Dispatching → Dispatched → Running → Settled`，并允许
`Invalidated` 作为不可执行的终态。
若在 claim 后无法判定 Host 是否收到请求，状态进入 `DispatchUnknown`，并只能通过
`Reconciling`（带同一 continuation ID 的查询/确认事件）收敛到 `Dispatched`、`Settled`
或 `Invalidated`。`DispatchUnknown` 不得直接重发。

任何 synthetic prompt 发送前都必须在 session mailbox 中原子 claim lease。只有
`DispatchClaimed` 已成功 append，才允许进入物理 dispatch；claim 与 owner/turn/
generation 验证不能拆成两次写。`flatMapMerge` 执行 effect 前仍须重新读取最新
`cancelGeneration`，与 lease 创建时的代次比较。只要发现当前代次已大于租约创建时的
代次，此租约必须被宣布为 `Invalidated`，直接拦截调用。

真正调用 `session.prompt` 前必须重新进入 session mailbox，验证：
* lifecycle 仍为 Active；
* cancelGeneration 未变化；
* humanTurnID 仍相同；
* continuation owner 仍为 `FallbackRecovery`；
* lease 已由同一事务从 `Pending` 原子变为 `DispatchClaimed`；
* 没有 compaction owner；
* 没有新真人消息；
* 没有 TaskComplete；
* 没有 force-stop。

claim 事务成功后，物理调用前必须再次确认 lease 仍为 `DispatchClaimed`，然后追加
`Dispatching`；Host 返回确认后追加 `Dispatched`/`Running`，最终追加 `Settled`。
验证失败则把 lease 标记为 `Invalidated`，绝不调用 Host。每个 dispatch/result 事件
都必须带 continuation ID、claim sequence 与 CausalityContext；result 只可作用于仍
匹配的 lease，迟到或重复 result 记录为 stale，绝不能复活旧 lease 或覆盖新轮次。

### 3.3 ControlEnvelope

包含：warn、warn_tdd、warn_reuse、已移除的 `amend`（仅作历史兼容说明）、tool capability、validation result、audit fields。

它与净化后的业务参数彻底分离。在执行网关（PRD-03）中由 before hook 提取，在自有 execute wrapper 中再次验证，在 after hook 中用于审计。

### 3.4 ContextBudgetObservation

包含：effective tokens、observation source、**count-based cache semantics**（revision/
counter、已应用事件数）、model limit、limit source、context generation、confidence、
degraded reason。缓存不得以内容 hash 或 fingerprint 作为 key/正确性依据；canonical
byte/prefix equality 只可作为可观察断言。

防止把一个裸整数误认为绝对真实值。

### 3.5 ReviewPromptContext

包含：original task、loop ID、round、feedback、todos、prompt origin。

所有 review prompt 统一由它生成。

## 四、SessionGateDemand 统一优先级

Compaction、fallback、review、todo、runner 之间不应分别在自己的 hook 中判断"现在能不能发"。

统一 gate 优先级：

1. `UserAbort`
2. TaskComplete gate (not an owner)
3. `FallbackRecovery`
4. `Compaction`
5. `ContextBudget`
6. `ReviewLoop`
7. `TodoNudge`
8. `RunnerReminder`

每个 session 同时只能有一个能发送 synthetic prompt 的 owner。owner 必须通过 settle 事件释放，不能只看 session 当前是否 idle。

在 Flow 管线中，gate 判定作为 `step` 内的纯 policy 函数实现，输入是当前
`SessionState` 的原子 snapshot，输出是 `NudgeBlockReason` 枚举。`stateFlow |> map
plan` 只能呈现同一 policy 的 UI/诊断预览，不执行计划：

* Allowed
* UserCancelled
* FallbackActive
* CompactionActive
* SyntheticTurn
* PendingDelivery
* RunnerOwnsTurn
* DuplicateAnchor
* UnknownTerminalOrigin

纯 `deriveAction` 继续只根据 snapshot 判定，不反向依赖 Shell 可变 runtime。Shell/host adapter 读取 fallback observation 后转换为 `NudgeBlockReason`，构造 snapshot，传入纯函数。

## 五、不变量到 Flow 管线的映射

| 不变量 | Flow 管线位置 |
| :--- | :--- |
| 1. Esc 粘性取消 | `scanCommit` 第一分支 Cancelled/TaskComplete → 终止；lease 在 effect 边界校验 |
| 2. 消息来源 | Input 携带 provenance，在 normalize 阶段分配 |
| 3. 单一 owner | `step` 内以一个 snapshot 做纯 policy 仲裁 |
| 4. 代次绑定 | TurnIdentity 在 Input 和 EffectResult 中贯穿 |
| 5. model 属于轮次 | HumanTurnRoute 从 `chat.message` Input 派生，进入 projection |
| 6. 控制字段唯一入口 | effect 执行前净化 + clean assertion |
| 7. budget 不静默失效 | ContextBudgetObservation 在 `step` 内计算，degraded 不返回 None |
| 8. review task 非空 | ReviewProjection 从 Events fold，snapshot 派生 activeTask |
| 9. 终止来源分类 | terminalOrigin 在 Step 中，由 `scanCommit` 输出携带 |
| 10. stdout 纯净 | 所有日志作为 effect 在 `scanCommit` 外执行，不污染管线 |
# 问题 1：Esc、Abort 与 fallback auto-continue

## 一、Flow 管线位置

本问题映射到管线上的三个位置：

1. **`scan` 第一分支**：`Cancelled` 和 `TaskComplete` 作为最高优先级终止态，在任何 gate 计算之前拦截。
2. **`flatMapMerge` effect 执行边界**：lease 校验在 Host prompt 调用前的"最后一毫米"执行。
3. **Input normalize 阶段**：per-session serial mailbox 消除 OpenCode event hook 并发竞态。

## 二、当前根因

### 2.1 零宽字符身份判断不可靠

当前 Opencode fallback 使用零宽空格作为 `SendContinue` prompt。UI 看不见，但 host 登记为 `role=user` 消息。系统再通过注入时间判断是否为 fallback 发出。

问题在于：
* 零宽字符只是内容，不是身份。
* 时间戳可能缺失、为 0、单位不同、事件顺序倒置。
* fallback 注入和用户 Esc 是两个异步事件，可能交错。
* 状态机的 `NewUserMessage` 无条件把 lifecycle 恢复为 Active。
* 一条迟到的零宽 user message 可能被误判为真人消息，解除 Cancelled。
* 注入记录是"最近注入过什么"，没有精确绑定到 message ID、continuation ID 和 human turn。
* 请求、发送、观察、失败、取消被混成一个事实。

### 2.2 needFallbackContinue 漏掉 Cancelled（增补稿 I）

当前 `FallbackSubagentGate.needFallbackContinue` 只把 `TaskComplete` 视为终止状态：
* `TaskComplete` 立即返回 `false`；
* `Cancelled` 则继续检查 `EventHandlingActive`、`AwaitingBusy`、`SubsessionPending` 和 `BusyCount`；
* 因此只要任意临时门禁仍为活动状态，已取消的 session 仍被判定为需要 fallback continuation。

`terminalObservation` 和 `isSubagentSettledFromObservation` 也只特别处理了 `TaskComplete`，没有将 `Cancelled` 纳入完整终止语义。

### 2.3 Abort 空输出路径（增补稿 II）

`FallbackEventBridge.handleEvent` 在处理 `session.idle` 时：
1. 若 lifecycle 已为 `Cancelled`，只生成普通 `SessionIdle`；
2. 否则拉取消息历史，调用 `tryGetLastAssistantAbortInfo`；
3. 若识别到 Abort，生成带 abort 信息的 `SessionError`；
4. 只有未识别到 Abort 且最后输出无内容无工具时，才创建 `EmptyOutputError`。

因此"所有 Abort 空输出都直接被误判成 EmptyOutput"并非准确描述。但以下情况仍会丢失 Abort 语义：
* Host 只发送 `session.interrupted`，但不写 assistant metadata；
* stream 在生成 assistant message 之前被中止；
* Host 使用新的错误名称；
* Abort 信息位于 event status、cause 或嵌套 error 中；
* Abort event 先到，idle event 后到，后者无法再恢复原因。

### 2.4 状态机已返回 DoNothing 但旧 SendContinue 晚到（增补稿 II）

典型竞态：
1. 错误事件使状态机决定 `SendContinue`；
2. Action 已离开纯状态机，进入 Promise 或 Host 调用队列；
3. 用户此时按 Esc；
4. lifecycle 被改成 `Cancelled`；
5. 先前排队的 `SendContinue` 仍然调用 Host prompt。

单纯再次读取 `state.Lifecycle` 仍不够，因为：
* 读取后和真正发送之间仍有极短竞态；
* 读取的 state 可能是旧快照；
* 新人类轮次可能已经开始，lifecycle 又恢复为 Active；
* 旧 Action 可能因此误投到新轮次。

## 三、OpenCode v1.17.13 源码定案（增补稿 III）

### 定案一：TUI Esc 是双击取消

目标版本中：
* 第一次按 Esc 只增加 UI interrupt 计数，提示再次按键；
* 五秒内第二次 Esc 才调用 `sdk.client.session.abort`；
* CLI interrupt 则直接调用 abort，没有双击确认。

因此任何 Esc 问题必须先区分用户只按了一次还是完成了两次。如果只按了一次，OpenCode 根本没有发生硬取消，fallback 继续运行在 Host 语义上正常。但只要 Abort API 已被调用，fallback、nudge 和其他 synthetic continuation 就必须停止。

### 定案二：session.idle 绝不等于"自然完成"

官方 `session.idle` 只有 `sessionID`，没有 completion reason、abort reason、current run ID、origin、generation、是否来自 compaction、是否来自错误、是否即将 auto-continue。

它可能由正常回答结束、用户 Abort、provider error、retry 结束、compaction、不可恢复 overflow、session 没有 runner 时调用 abort 产生。

> 禁止仅凭 `session.idle` 触发 todo/review nudge。

### 定案三：Abort 事件不完整也不保证唯一

Abort 通常形成 `MessageAbortedError`，但并不保证一定有 `session.error`。有些取消路径只会更新 assistant message error、设置 completed time、发布 message.updated、发布一个或多个 idle。

* `processor.halt` 可能设置一次 idle；
* `Runner.cancel` 又可能设置一次 idle；
* cleanup 和 interrupted finalizer 可能多次更新同一 assistant message。

不得依赖"必须先收到 session.error，再收到一次 idle"。

### 定案四：plugin event hook 是并发 fire-and-forget

普通 trigger hook（`tool.execute.before`、`tool.execute.after`、`chat.message`、`chat.params`）由 OpenCode 按插件加载顺序串行等待。

但通用 `event` hook 是 fire-and-forget：不等待 Promise，同一插件的前后两个事件 handler 可以并发，同一 session 处理完成顺序不受保证，`message.updated` 本身可能重复发布。

当前依靠多个 bool 标志和异步 handler 自行读写状态的方式天然存在竞态。

### 定案五：OpenCode 没有 cancellation generation

官方 Runner 内部有 run handle，但没有通过插件协议暴露 run ID、cancel generation、continuation ID、当前 human turn generation。万象术不能仅靠 OpenCode session lifecycle 判断迟到事件属于旧轮次还是新轮次，必须自行建立 generation 和 lease。

### 定案六：before Hook 原地修改有效，替换无效

`tool.execute.before` 收到的 `output.args` 与随后传给真实工具的局部 `args` 通常是同一个对象引用。因此 `delete output.args.warn_tdd` 有效；`output.args = newObject` 无法改变真实 execute 使用的旧局部引用。

### 定案七：before Hook 不能覆盖全部内部执行路径

经过 before/after 的有：registry built-in 工具、custom plugin 工具、MCP server 工具、MCP resource 工具、Task/subagent 工具。

不经过或不完全经过的有：StructuredOutput、title agent、compaction agent、部分内部直接调用的 read。after hook 在工具失败或 Abort 时不会执行。

## 四、正确状态模型

### 4.1 fallback continuation 完整生命周期

* Requested
* DispatchClaimed
* Dispatching
* Dispatched
* Running
* ObservedByHost
* BusyObserved
* Settled
* Cancelled
* Failed

每个 continuation 带有：session ID、human turn ID、continuation ID、创建时的 cancel generation、选中的 model/agent、provenance = FallbackContinuation，以及唯一的 `ContinuationLease.owner`。`ContinuationLease` 是 owner 的唯一权威来源；不得另设一个可变的 current-owner 字段与 lease 竞争。

Dispatch 的阶段必须区分为：

* `Requested`：只有计划，没有取得发送资格；
* `DispatchClaimed`：已在 session mailbox 中原子取得 lease，但尚未开始 host 调用；
* `Dispatching`：host 调用已经开始；
* `Dispatched`/`Running`：host 已接受或已开始执行；
* `DispatchUnknown`：调用结果未知（超时、连接断开或进程中止），必须走 `Reconciling`，不能猜测为未发送或已 settle；
* `Settled`、`Cancelled`、`Failed`：终局状态。

`DispatchClaimed` 仍属于“尚未开始物理 dispatch”，可以被 Abort 原子撤销；进入 `Dispatching` 后则属于“已经开始 dispatch”，Abort 只能记录取消并等待/协调 host 结果，不能声称撤回调用。

### 4.2 CancelTombstone

AbortProjection 应综合以下证据：`session.error.name = MessageAbortedError`、assistant message `error.name = MessageAbortedError`、tool part `metadata.interrupted = true`、`session.interrupted`、`stream-abort`、本插件主动调用 abort 的记录、Abort 后出现的 idle、local force-stop 操作。

任何明确 Abort 证据出现，都建立取消墓碑：

```text
CancelTombstone
- sessionID
- humanTurnId
- cancelGeneration
- observedEventID
- observedAt
- reason
```

`session.idle` 本身不能创建取消墓碑，但在已有取消证据后可以帮助确认 Host 正在收尾。

所有 Host translator 应把以下语义归一化为同一种领域事实：用户取消、客户端取消、stream abort、session interrupted、AbortError、MessageAbortedError、SDK cancellation token、主机明确的 stop-by-user。

归一化结果必须包含：`DomainError = MessageAborted` 或明确的 ClientCancellation、`IsRetryable = false`、cancel generation、原始 Host reason code、当前 human turn ID。不能仅依赖错误名字字符串。

## 五、修复方案

### 5.1 P0 止血：统一 Cancelled gate

所有由 lifecycle 派生的 gate 判定，第一分支统一为：

```text
Cancelled    → 终止（false）
TaskComplete → 终止（false）
其他状态     → 继续判定
```

适用范围至少包括：
* `needFallbackContinue`
* `terminalObservation`
* `isSubagentSettledFromObservation`
* recovery wait
* nudge block
* pending prompt dispatch
* fallback action executor

任何 BusyCount、AwaitingBusy、EventHandlingActive、Consumed、Phase 都不得覆盖 Cancelled。

统一不变量：

> Lifecycle 只要是 Cancelled 或 TaskComplete，任何 BusyCount、Consumed、Phase 和 ActiveGates 都无权重新要求 continuation。

### 5.2 P0 止血：Abort 原子清理门禁（CancelEpisode）

Abort 被状态机正式识别为 `Cancelled` 后，在同一 session 串行操作中完成：

* `SetAwaitingBusy false`
* `ClearSubsessionPending`
* `SetEventHandlingActive false`，或确保当前 handler 的 finally 不会重新产生继续需求
* `SetNudgeActive false`
* `SetBusyCount 0`
* `ClearConsumed`
* 清除尚未发送的 continuation request
* 清除旧 injected model 的活跃作用域
* 唤醒等待 gate 状态变化的 listener，使其重新计算并结束等待

需要一个统一的 `CancelEpisode` 操作，不能让不同 host 分别手动清理若干字段。

### 5.3 引入 per-session 串行事件邮箱

由于 event hook 并发执行，所有下列操作必须进入同一个 per-session serial mailbox：event 解析、fallback state transition、lifecycle 更新、BusyCount 更新、injected continuation 记录、nudge snapshot 更新、compaction projection 更新、event log append、action 派生。

状态流：

```text
Host event
→ 快速解码 sessionID/eventID
→ 投递 per-session mailbox
→ 去重
→ 更新 projection
→ 派生 action
→ 记录 action intent
→ mailbox 外执行副作用
→ 副作用结果重新投递 mailbox
```

不得继续依赖多个异步 handler 直接修改同一 runtime map。

### 5.4 所有 SendContinue 改为带 Lease 的动作

状态机输出不应再只是 `SendContinue(model)`，应当包含 `ContinuationLease`（参见 PRD-01）。

真正调用 `session.prompt` 前必须重新进入 session mailbox，验证 lease 全部条件。验证失败则把 lease 标记为 Invalidated，绝不调用 Host。

被取消的 Action 不得产生 EmptyOutput、RetrySame 或下一次 fallback。记录 `continuation_invalidated` 而不是静默发送或重新分类为错误。

Abort 必须按 dispatch 阶段处理，不能把所有旧 Action 统称为“未发送”：

| Abort 时状态 | 必须动作 | 迟到事件 |
|---|---|---|
| `Requested` 或 `DispatchClaimed`（尚未开始物理调用） | 立即撤销 lease、写 `continuation_invalidated`，保证不调用 Host | 按旧 lease/generation 丢弃 |
| `Dispatching`、`Dispatched` 或 `Running`（已开始物理调用） | 标记 `CancelRequested`；若 Host 支持则调用 abort；不得伪造撤回或再次 dispatch | 进入 `DispatchUnknown`/`Reconciling`，只允许补齐该 lease 的事实 |
| `DispatchUnknown` | 保持取消屏障并启动 reconciliation；不得从不完整结果推导 settle | 仅接受匹配 lease、generation 的 reconciliation 事件 |

任何不匹配当前 lease、human turn 或 `cancelGeneration` 的 busy/idle/error/message 事件都是 stale event：必须幂等记录/丢弃，不能恢复 lifecycle、增加 BusyCount、释放新轮次的 owner，不能触发 fallback、nudge 或 synthetic prompt。已开始的 dispatch 即使最终报告成功，也只能结算其原 lease，不能为已取消的人类轮次产生后续 continuation。

### 5.5 注意 session.prompt 可能在 Busy 状态下排队

官方 prompt 路径可以在 Runner 尚未完全释放时等待或排队。user message 可能在真正开始下一次 run 之前已经创建。

应禁止在 event handler 原始调用栈中直接 prompt。先将 continuation 标记为待派发，等待本地 session mailbox 完成当前事件批次，重新查询 Host session status 和最后消息，再次校验 lease，然后调用 prompt。

固定 sleep 只能作为兼容性缓冲，不能承担正确性。

### 5.6 Esc 的处理顺序

Esc 到达时必须在同一 session 串行队列中原子完成：
1. 增加 `cancelGeneration`。
2. 把当前 human turn 标记为 Cancelled。
3. 标记所有旧 generation 的 continuation 为 invalidated。
4. 清除 AwaitingBusy、pending auto-continue、pending nudge（`CancelEpisode`）。
5. 能调用 host abort API 时，主动中止在途 prompt。
6. 记录持久事件 `user_abort_observed`。
7. 后续所有迟到事件先核对 generation，再决定是否处理。

不要仅仅把 fallback state 的 Lifecycle 设为 Cancelled。必须让所有异步回调都能看见"自己已经过期"。

Abort 前先在 mailbox 中读取 continuation lease 的 dispatch 阶段：尚未开始物理 dispatch 的请求必须取消且不得调用 Host；已经开始的 dispatch 必须进入 `CancelRequested`/`DispatchUnknown`（按结果可见性再 `Reconciling`），不得误报为“没有新 dispatch”。旧 lease 的所有迟到事件均按 stale event 处理，并且不得影响新 human turn。

### 5.7 真人新消息如何恢复

`NewUserMessage` 不应再是无参数事件，而应携带来源和消息身份。

只有同时满足以下条件才能开启新轮次：
* 来源为 Human；
* message ID 尚未处理；
* 消息不是 synthetic；
* 创建时间或 host 顺序位于最后一次 Esc 之后；
* 不是旧 continuation 的迟到映射；
* 不是 compaction/title/nudge。

然后创建新 human turn ID，重置 fallback attempt，清除旧 injected model，清除旧 cancellation barrier，保存真人消息携带的 model、agent、variant。

### 5.8 fallback prompt provenance

OpenCode PromptInput 不允许插件直接设置 `synthetic=true`，也没有正式 correlation ID。

短期双重识别：
1. 本地 pending lease；
2. prompt front matter 中的 opaque continuation ID；
3. `chat.message` Hook 捕获新 messageID；
4. 建立 `messageID → FallbackContinuation(continuationID)` 映射。

文本 front matter 只负责把 pending request 与官方 messageID 对上，真正 SSOT 仍是本地 projection。

### 5.9 零宽字符怎么处理

最佳方案是彻底停止用零宽字符承担身份识别职责。优先级：
1. host 支持 metadata：直接附加 provenance、continuation ID。
2. host 返回 message ID：保存请求 ID 和返回 message ID 的映射。
3. host 只能发送文本：使用内部可解析控制封套，在发送给 LLM 的消息变换阶段剥离。
4. 实在无法关联时，时间戳只能作为兼容旧版本的辅助信息，不能作为权威判断。

### 5.10 事件日志调整

把单一的 `fallback_continue_injected` 拆成更准确的事实：
* `fallback_continue_requested`
* `fallback_continue_dispatch_started`
* `fallback_continue_dispatched`
* `fallback_continue_observed`
* `fallback_continue_cancelled`
* `fallback_continue_failed`
* `fallback_episode_settled`

旧事件可以继续读取，但新逻辑不能把"写下 injected"直接等价为"host 已经接收并产生消息"。

### 5.11 事件幂等

使用 OpenCode event ID、messageID、partID、callID、local continuationID 建立去重键。`message.updated` 不能以"每出现一次就代表一次新完成"处理。

MessageID 可以辅助排序，但不能代替 generation。MessageID 是单调 ULID，但真人消息、compaction request、synthetic continuation、插件 nudge、fallback prompt 都会创建新 user ID。

## 六、验收标准

> Esc 之后，在真人再次输入前，不得再出现任何由旧 human turn 产生的 prompt。

补充验收：
* 单次 Esc 不错误标记为 Abort；
* 双次 Esc 后旧 continuation 调用数为零；
* `session.error` 缺失时仍能识别 Abort；
* 重复 idle 不产生重复状态转移；
* 旧 generation 的 message.updated 不恢复 session；
* 已派生但未发送的 action 被取消；
* 已经排队的 prompt 可被识别和失效；
* `Cancelled + EventHandlingActive=true` 必须返回不继续；
* `Cancelled + AwaitingBusy=true` 必须返回不继续；
* `Cancelled + BusyCount>0` 必须返回不继续；
* `Cancelled + Phase=Retrying` 必须返回不继续；
* Cancelled session 必须被视为 settled；
* `DispatchClaimed` 后但未开始 Host 调用时 Abort 不调用 Host；
* 已开始的 dispatch 在 Abort 后不被误报为未发送，且只可结算旧 lease；
* `DispatchUnknown` 必须 reconciliation，不能直接 settle 或重派；
* stale event 不恢复 lifecycle、不增加 BusyCount、不释放新 lease owner、不产生 nudge；
* 旧 continuation 的迟到 busy/idle 不能重新增加 BusyCount；
* 当前 event handler 的 finally 清理标志后不能触发新的 fallback send；
* 新真人消息必须创建新 turn/generation 后才允许恢复 Active；
* Abort 空输出不会产生 EmptyOutputError；
* 缺少 assistant abort metadata 时 translator 仍可识别取消；
* 旧 generation 的 busy/idle 不改变新轮次；
* 已生成但过期的 SendContinue 不会调用 Host；
* Cancelled 后所有旧 continuation lease 失效；
* 同一 Abort 被多个 Host event 重复报告时只取消一次。

## 七、必测竞态场景

* fallback 决策之前 Esc；
* Requested 之后、真正调用 prompt 之前 Esc；
* prompt 调用中 Esc；
* host 已接收但 message.updated 尚未到达；
* message.updated 已到达但 busy 尚未到达；
* busy 到达后、idle 之前；
* idle 到达时；
* session.error 与 Esc 同时到达；
* 进程重启、旧 injected 事件重放后；
* 消息时间戳缺失、为 0、时钟倒退；
* duplicate message.updated；
* fallback prompt 成功但回调晚于新真人消息；
* 状态机返回 `SendContinue` 后、Host prompt 调用前按 Esc；
* Host prompt Promise 已建立但未实际发送时按 Esc；
* Abort event 没有 assistant message；
* Abort metadata 晚于 idle event；
* idle 先被判定为空输出，随后收到 abort；
* 新真人消息已经开启新 turn，但旧 Action 仍在队列；
* lifecycle 恢复 Active，但 continuation generation 已过期；
* 单次 Esc（不调用 abort）后 fallback 继续运行（Host 语义正常）；
* 双次 Esc 后所有旧 continuation 调用数为零。
# 问题 2：控制字段与执行网关

## 一、Flow 管线位置

本问题映射到管线上的 **effect 执行边界**。在 `flatMapMerge(maxConcurrency)` 之前、真实 execute 调用之前，设置统一执行网关进行三层防线：schema 软提示 → before hook 原地提取+删除 → execute wrapper 终极净化+clean assertion。`warn`、`warn_tdd`、`warn_reuse` 以及 todo 报告长度是合规提示，不得成为 Host 的执行前门禁。

控制字段提取为 `ControlEnvelope`（参见 PRD-01），与净化后业务参数彻底分离。

## 二、当前根因

仓库已经有共享的解析和删除函数：`requireWarnTddOnArgs`、`requireWarnOnArgs`、`requireWarnReuseOnArgs`。`filterAmendFromArgs` 已随 `amend` 功能一起移除。这些函数应改为记录 present/missing/blank/non-canonical 合规状态并删除字段；合规缺失不阻止工具执行。

但整个系统仍存在结构性漏洞：

### 漏洞 A：schema 注入分散

Opencode、OMP、Mux 分别有自己的 schema 改写路径。有些 host 内置工具经过增强，有些自定义工具经过增强，有些 alias 没经过，动态注册工具可能晚于增强阶段，同类修改工具在不同 host 上要求不同。

### 漏洞 B：工具分类和 schema 分类没有完全共用

`WarnTdd` 中对 modification tool、subagent tool、warn-required tool 有一套集合，但部分 host 又自行硬编码 coder、executor、write 等名单。名单一旦漂移就会出现：执行阶段按能力识别字段但 schema 没给出软提示，或 schema 给出提示但执行阶段没记录合规状态，或某个 alias 可以绕过。

### 漏洞 C：before hook 不一定真正注册

从 Mux 的组装结构看，before 和 after 逻辑存在，但插件暴露路径可能只稳定注册了 after。这样"执行前剥离"就不是强保证。

### 漏洞 D：原参数对象可能继续流向下游

即使某个 hook 删除字段，如果 wrapper 保存了原引用、host 在 hook 之前复制了参数、after hook 又读取旧 input、执行路径不经过该 hook，控制字段仍可能泄漏到实际工具。

### 漏洞 E：把合规提示当作执行前拒绝

缺少 `warn_tdd`、`warn` 或 `warn_reuse` 不属于业务参数错误。工具应先执行，随后在结果规范化末尾追加一次严厉批评；只有净化后的业务参数仍泄漏控制字段时才 fail closed。

## 三、OpenCode v1.17.13 源码定案

### before Hook 原地修改有效，替换无效（定案六）

`tool.execute.before` 收到的 `output.args` 与随后传给真实工具的局部 `args` 通常是同一个对象引用。因此：
* `delete output.args.warn_tdd` 有效；
* `output.args = sanitizedCopy` 无法改变真实 execute 使用的旧局部引用。

### before Hook 不能覆盖全部内部执行路径（定案七）

经过 before/after 的有：registry built-in 工具、custom plugin 工具、MCP server 工具、MCP resource 工具、Task/subagent 工具。

不经过或不完全经过的有：StructuredOutput、title agent、compaction agent、部分内部直接调用的 read。after hook 在工具失败或 Abort 时不会执行。

所以 after hook 不能作为安全校验边界。

### MCP 工具不经过 tool.definition

MCP 工具虽然经过 `tool.execute.before`，但其 schema 不经过通用 `tool.definition`。

### 多插件顺序产生新隐患

trigger hook 按插件加载顺序串行执行，并共享同一个 output 对象。存在危险情况：
1. 其他插件先执行，把 `output.args` 替换成新对象；
2. 万象术随后看到的是新对象；
3. 万象术在新对象中删除 warn 字段；
4. OpenCode 最终仍把旧局部 args 传给 execute；
5. 旧对象中的 warn 字段未被删除。

万象术若依赖 before hook 原地净化，应满足至少一项：万象术排在会替换 args 的外部插件之前；启动时声明插件顺序要求；对自有工具包装真实 execute；推动上游将 `plugin.trigger` 返回的 `output.args` 传入 execute；对生产部署做多插件兼容测试。

### OpenCode 没有正式 block/deny 协议

目标版本中 before hook 抛错会：阻止真实工具执行、产生 AI SDK tool-error、通常不会直接形成 session-level error、不会自动进入万象术 fallback、但 LLM 可能重新尝试同一工具。

### Opencode JSON Schema 的硬 required 注入必须移除

旧实现为 `properties` 增加 `warn_tdd` 并追加到 `required`，对 `warn`/`warn_reuse` 也执行类似操作；该实现与“漏填也执行”冲突，必须移除。保留字段、description、examples 和 `x-wanxiangshu-soft-required` / `x-wanxiangshu-soft-min-length` 元数据即可。`enum` 只能放在不参与 Host 校验的说明性元数据中；不得放入会使 Host 拒绝调用的 schema。

需重点检查的是：Zod/Effect Schema 转换后的最终 JSON Schema 是否保留约束、Host 是否实际使用改写后的 schema、tool definition hook 是否覆盖所有工具、alias 和动态工具是否在 hook 之后才注册、某些 wrapper 是否替换了 parameters/jsonSchema、Mux/OMP 是否共享同一工具能力分类。

## 四、修复方案

### 4.1 建立统一 Control Field Policy

建立唯一策略表，以"工具能力"而不是工具名为主键。

每个字段定义：适用能力、软合规提示文本、推荐值、错误消息、是否向 LLM 可见、是否允许到达下游、是否进入审计事件、retry 时是否需要重新提供。缺失和短报告必须可执行，并由结果批评标记。

能力分类：
* FileMutation：coder、edit、write、apply_patch、patch
* ProcessExecution：executor、pty_*
* SubagentDelegation：investigator、meditator、browser、coder delegation
* SearchOnly
* ReadOnly
* ToolCorrection

新增 alias 时只需要注册能力，不需要复制四处名单。

### 4.2 Schema 软提示（第一层）

#### schema 必须在最终导出边界统一增强

正确顺序：
1. host 收集内置工具；
2. 插件加入自定义工具；
3. alias/包装器全部建立；
4. 最后统一执行 schema decorator；
5. 启动时检查所有适用工具均暴露软字段说明和元数据。

不能在各工具定义过程中零散增强。

#### 启动检查不得把软字段变成 fail closed

* FileMutation、ProcessExecution、SubagentDelegation 工具必须在 `properties` 中展示相应字段及软合规元数据；
* 不得把 `warn_tdd`、`warn`、`warn_reuse` 放入 Host 会强制执行的 `required`，不得设置会被 Host 执行的 `minLength`；
* 缺少软字段说明只产生诊断/警告，不得阻止插件启动或工具调用；
* 只有 malformed business args、permission/security denial、parse failure，或净化后控制字段泄漏，才允许启动/执行 fail closed。

#### MCP 工具

如果受保护能力全部来自 registry 或 Task，可以正常注入 schema。如果未来某个敏感 MCP 工具也需要这些字段，必须自己包装该 MCP tool、或修改 OpenCode adapter、或推动上游为 MCP 暴露 definition hook。不能假设现有 `tool.definition` 自动覆盖 MCP。

### 4.3 before Hook 原地验证+删除（第二层）

在 v1.17.13 中，正确做法是原地验证 → 原地提取 → 原地 delete。

禁止 `output.args = sanitizedCopy`，因为真实 execute 仍会使用旧局部引用。

before hook 提取控制字段，保存为 `ControlEnvelope` 到 transient `ToolComplianceStore`（键：session ID + tool call ID）。before hook 写入，after hook 读取并删除。

合规检查发现缺失、空白或非规范值时：before hook 必须保存 violation 并继续真实 execute；after/final result normalization 之后追加一次 `WANXIANGSHU_COMPLIANCE_REPRIMAND`，不改变 success、不抹掉原始输出、且明确不要重复已成功的调用。只有硬业务错误、权限/安全拒绝、解析失败或净化后仍泄漏控制字段时，才返回结构化 tool rejection。

### 4.4 execute wrapper 终极净化（第三层）

目标：抵御 Host 克隆和重建。

在最靠近真实副作用的统一 wrapper 中：
1. 从本次实际收到的 execute args 创建业务参数副本（浅拷贝或深拷贝，因 Host 可能有 Object.defineProperty 或 frozen object）；
2. 再次删除全部控制字段；
3. 检查删除后的对象；
4. 若净化后的业务参数仍发现保留字段，立即拒绝执行（fail closed）；这只适用于安全泄漏，不适用于控制字段缺失；
5. 把控制信息放入独立的 `ControlEnvelope`；
6. 真实工具只能看到净化后的业务参数。

"最靠近 execute"不等于指定某一个现有 wrapper。实施前必须绘制各 Host 的实际调用图：原生工具、插件工具、Mux 代理工具、Subagent 转发工具、PTY、文件修改、executor、MCP、动态注册工具。找到每条路径真正不可绕过的最后公共边界。如果不存在单一公共边界，就需要在少数几个 adapter 上放置同一个共享 sanitizer。

### 4.5 净化后断言

在开发和测试构建中，真实 execute 之前应断言业务参数中不存在 warn、warn_tdd、warn_reuse、已移除的 `amend`（如仍出现则视为残留）、`_ui`（若它只属于 UI）、未来新增的控制字段。

这条断言比"我们调用过 deleteKey"更有证明力。

```text
IF checkControlFieldsExist(args) THEN
    LogErrorAndFailClosed()
```

### 4.6 after hook 职责

after hook 只做：成功结果审计、使用统计、不变量告警。不能承担安全校验，因为失败和 Abort 时 after 根本不会执行。

### 4.7 三层防线正式定案

| 层 | 目标 | 机制 |
| :--- | :--- | :--- |
| 第一层 Schema | 让 LLM 正确生成 | properties + description + examples + `x-wanxiangshu-*` 软元数据；不得使用 Host 强制 required/minLength |
| 第二层 before Hook | 运行时记录 + 原地删除 | 原地 delete（不替换 output.args）+ 保存缺失/短报告 violation，不阻断 |
| 第三层 execute wrapper | 再次验证 + 再次净化 + clean assertion | ControlEnvelope 分离 + fail closed |

### 4.8 各 Host 具体改造

#### Opencode

* 保留 ToolDefinitionHooks 的 schema 扩展职责，但最终改为调用统一 decorator。
* HookExecute 不再自己定义工具名单。
* before 阶段调用统一执行网关。
* after 阶段不得再次要求已经被剥离的字段。
* 动态工具注册完成后重新执行完整性检查。

#### OMP

* 删除或废弃本地重复的 `requireWarn*Omp` 判断。
* coder、executor 之外的 host built-in edit/write/patch/pty 也要走统一能力分类。
* schema 和运行时都调用同一策略源。
* ToolResult hook 不再作为"二次安全兜底"；它只能检查执行网关留下的审计标记。

#### Mux

* 明确注册真正的 `tool.execute.before`。
* wrapper 和插件不能各自决定是否剥离。
* PluginCatalog 中只为少数工具注入字段的做法要改成全目录遍历。
* 对由 Mux 转发到其他 host 的工具，也要保证转发参数是净化后的参数。

### 4.9 仅硬错误作为 tool rejection

malformed business args、权限/安全拒绝、解析失败，或净化后仍存在控制字段泄漏时，返回强类型 `DomainError`，编码为"工具调用被拒绝"的正常 tool result：
* `executionStarted=false`；
* 不产生 session-level error；
* 不进入 fallback 错误分类器；
* 不调用真实工具。

`warn` 类缺失、空白、非规范值以及 todo 报告短不得走该路径；它们必须执行成功并在最终规范化结果中追加批评。不得抛出普通 JavaScript Error，因为可能被上层解释为 session.error、未知执行错误、retryable error、fallback 触发条件。

## 五、验收矩阵

对每个 host、每个 tool alias 都验证：
* schema 中字段存在；
* 软字段说明、examples 和 `x-wanxiangshu-*` 元数据存在，且没有 Host 强制 required/minLength；
* LLM 缺字段时工具仍执行，结果只有一次批评；
* warn 值缺失/空白/非规范时工具仍执行，结果保留原输出并追加批评；
* 合法字段可以执行且不产生批评；
* 实际下游序列化参数不含 warn/warn_tdd/warn_reuse；
* after hook 看不到普通参数中的控制字段；
* 重试不会因为字段已被删除而错误失败；
* null、空对象、额外字段行为一致；
* plugin 注册顺序改变后仍然有效；
* 动态加入的新工具要么被增强，要么产生软合规诊断但仍可执行；
* before hook 删除失效的模拟 Host 下，execute wrapper 仍能净化；
* 真实工具参数中控制字段数量为零；
* 软合规 violation 不产生 session.error 或 fallback；硬错误仍按安全策略拒绝；
* wrapper 直接调用 execute 仍无法绕过；
* after hook 检测到 before 被绕过时只告警，不假装已阻止；
* 替换 `output.args` 的回归测试明确失败并被检测；
* 其他插件替换 args 时产生兼容性警告；
| 硬错误或净化后泄漏导致 before/ wrapper 拒绝时真实 execute 次数为零；
| after 缺失不影响安全；
| 自有 wrapper 存在 clean assertion。

## 六、深拷贝问题专项测试

建立一个模拟 Host：
1. before hook 收到对象 A；
2. Host 在 hook 前保存 A 的深拷贝 B；
3. before hook 删除 A 中的控制字段；
4. Host 把 B 传给 execute；
5. 最终 wrapper 必须再次净化 B；
6. 真实工具收到的对象不得包含控制字段。

同时测试：浅拷贝、深拷贝、JSON round-trip、args wrapper 重建、alias 重新解码、nested args、frozen object、null prototype object。
# 问题 3：context budget 与 todowrite 触发

## 一、Flow 管线位置

本问题映射到管线上的 **`scan` 内部**。`ContextBudgetProjection` 作为 Step.State 的一部分，在每次 Input 进入 `scan` 时更新。预算判定作为 effect（注入 budget nudge synthetic message），在消息变换完成后、发送前计算。

## 二、当前实现存在的关键断点

### 断点 A：拿不到 token 就直接退出

当前 `resolveCurrentTokens` 的行为是：有实时 token count 则使用；否则有历史 `LastUsage` 按字节比例估算；两者都没有返回 `None`。随后 `applyContextBudget` 直接返回原消息，完全跳过预算保护。

> 最需要保护的未知状态，反而完全关闭保护。

### 断点 B：MaxInputTokens 可能变成 0

`resolveMaxInputTokens` 在同步和异步解析都失败后可能返回 0。`applyContextBudget` 又在 `MaxInputTokens <= 0` 时直接关闭机制。OpenCode 的 model limit 依赖 provider list 和最近 user model，provider API 不可用、model 解析失败、读取不到 limit 时可能把上限解析为 0。

### 断点 C：使用的是旧 assistant usage，不是即将发送的 prompt

最近一次 assistant 记录的 input/cache token，只表示上一轮 host 报告的使用量。它不一定包含当前 backlog projection、当前 caps、新加入的 synthetic message、message transform 后的最终消息、compaction 后的新上下文。

### 断点 D：R 的计算可能混入全部历史 todo

若完成 todo 的数量是从全部 flatten messages 统计，旧阶段做过的 todowrite 会持续影响当前公式。正确的 R 应是当前 budget episode 或当前 phase 开始后成功完成的 todo checkpoint 数量。

### 断点 E：NudgeTrack 被写入但没有真正参与防重和状态推进

系统可能记录 `EmergencySignaled`，但后续判定没有严格依据这个状态来决定是否已经发过、是否收到 todowrite、是否重置阶段、是否需要升级处理。

### 断点 F：message transform cache 掩盖了预算变化

如果缓存 key 只依赖原始输入消息，而模型 limit、usage observation、context generation 变化了，系统可能直接返回旧 transform 结果，不重新计算预算。

### 断点 G：开局误触发（增补稿 V）

当前 `rebuildPhaseState` 在第一次观测且 backlog 为空时将 `phaseBaseTokens = 0`。随后同一次 message transform 立即调用 `classifyPressure`。对于默认 `foldAfterFirst=false`，N=3，初始阈值约为有效窗口的 25%。CAPS、系统提示、用户首条消息本身可能已超过窗口 25%，于是系统把"开局就存在的固定上下文"错误认成"本阶段新增消耗"，立刻触发紧急 todowrite。

第一次没有真实 token usage 时当前兜底是 `totalBytes / 2`，属于未经校准的粗估，也可能严重高估首轮 token。

## 三、数学量语义

### 3.1 定义

* `L`：模型有效输入上限；
* `O`：为输出、工具、系统余量保留的 token；
* `B = L - O`：真正可用输入预算；
* `C`：当前最终 outbound prompt 的 token 数；
* `P`：一个 phase 预计新增的 token；
* `N`：允许继续的阶段数量；
* `R`：当前 budget episode 中已完成的 checkpoint 数；
* `A`：当前 phase 起点的 token 数；
* `G`：context generation。

### 3.2 关键约束

* 所有 host 对 L、O、C 使用同样定义；
* R 不从全历史计算；
* C 必须尽可能接近最终发送内容。

### 3.3 R 的正确计算

```text
phaseStartTodoOrdinal
currentTodoOrdinal
R = currentTodoOrdinal - phaseStartTodoOrdinal
```

Compaction、模型切换、context generation 变化时，都必须明确决定是否开启新 phase。

## 四、ContextBudget 状态机

* Healthy
* Approaching
* EmergencyRequired
* EmergencySignaled
* TodoObserved
* PhaseReset
* CompactionRequired
* MeasurementDegraded

### Healthy → EmergencyRequired

根据公式达到边界时进入。

### EmergencyRequired → EmergencySignaled

只注入一次 context budget nudge，并记录：episode ID、计算时的 C、B、阈值左右两侧、model、measurement source、context generation。

### EmergencySignaled → TodoObserved

必须观察到一次真正成功的 todowrite tool result，而不是仅仅看到 LLM 说"我会写 todo"。

### TodoObserved → PhaseReset

成功提交 backlog 后：更新 phase base、更新 todo ordinal、递增 phase generation、清除本 episode 的 signaled 状态。

### EmergencySignaled 长时间无进展

有限次数升级：
1. 第一次普通 nudge；
2. 第二次更强制的 nudge；
3. 再不执行则进入 CompactionRequired 或阻止继续扩展上下文。

不能每次 message transform 都无限重复插入。

## 五、token 测量分层

按可靠性排序：

1. host 的精确 tokenizer，对最终 encoded outbound messages 计数；
2. provider/model 官方 tokenizer；
3. 已校准的字节/token 上界估算；
4. 保守固定比例估算。

无法精确测量时进入 `MeasurementDegraded`，但仍执行保护。降级估算应该偏保守，宁可早一点 todowrite，也不能完全不触发。

### 5.1 区分 ObservedUsage 和 EstimatedOutbound

必须区分：

**ObservedUsage**：上一轮 Host/Provider 报告的实际 usage。

**EstimatedOutbound**：本轮经过所有 message transform 后，将要发送给模型的消息估算值。

Context Budget 不能只使用 ObservedUsage，因为新 tool result 尚未计入、backlog projection 尚未计入、caps/system prompt 变化尚未计入、review task 重注入尚未计入、budget nudge 自身尚未计入。

### 5.2 AI SDK v6 token 字段正确解释

```text
tokens.total
tokens.input
tokens.output
tokens.reasoning
tokens.cache.read
tokens.cache.write
```

* `tokens.total` 优先用于上一轮整体 usage；
* `tokens.input` 是经过 cache 调整后的值；
* 若重新构造原始 input，应把 cache read/write 加回。

推荐规则：

```text
若 total 有效：
  ObservedUsage = total
否则：
  ObservedUsage = input + cache.read + cache.write + output + reasoning
```

但每个 Host codec 仍须通过测试确认字段语义。

### 5.3 不再只读取最后一条 assistant

应从后向前寻找：非空、数值合法、context generation 匹配、非 title、可识别 usage 语义的最新 observation。跳过 synthetic assistant。只能选择最新有效快照，不能把多条 assistant usage 相加。

如果每条 assistant 的 `input` 已经代表该轮完整上下文，则遍历历史后只能选择最后一个有效值，否则会严重重复计算。

### 5.4 最终预算输入

```text
C = max(LatestObservedUsage, EstimatedFinalOutbound)
```

如果两者都不可用，进入 `MeasurementDegraded`，使用保守字符/字节估算，不能返回 None 后关闭机制。

### 5.5 Bootstrap estimator

当 Host 没有提供实时 token counts，且历史 `LastUsage` 也为空时，必须引入多语境保守估计器：

* 检测内容是否包含中文字符。若是，采用偏高估计（如每 2 个字符 1 个 token）；
* 若全为代码，按已知的高密度估计；
* 计算出的初始估算值必须乘以安全系数（如 1.25）；
* 显式标记此测量为 `MeasurementDegraded`。

不能使用单一的 `bytes / 4` 判定。中文字符在 UTF-8 中占用 3 个字节，但在很多 tokenizer 中一个汉字就是一个或半个 token，字节/token 比例接近 3:1。代码、长路径、YAML 标记的 token 密度可能会根据标点符号发生巨大抖动。

### 5.6 UsageConfidence

token 数值应携带可信度：

| 来源 | 可信度 | 可否触发 emergency |
| :--- | :--- | :--- |
| Host 返回的真实 usage | Observed | 是 |
| 基于上次真实 usage 的 bytes 比例估算 | CalibratedEstimate | 是 |
| 第一次 `bytes / 2` 粗估 | BootstrapEstimate | 否 |

第一次只有粗估时：仅记录 baseline，不触发 todowrite。至少取得一次真实 usage 或完成一次校准后，才允许进行压力判定。

## 六、model limit 解析

### 6.1 EffectiveContextLimit

集中建立 `EffectiveContextLimit` 解析器，返回的不只是数值，还包括：limit、source、model、是否缓存、缓存年龄、是否降级、reserve、failure reason。

### 6.2 优先级

OpenCode 模型 limit 为 `context`、`input?`、`output`。

推荐顺序：
1. `limit.input`，如果存在；
2. `limit.context - output reserve`；
3. 同模型最近成功缓存；
4. model family 保守值；
5. Host 配置默认；
6. 全局保守默认。

### 6.3 禁止行为

* 官方在 limit 缺失时把 context 设为 0，最终禁用 compaction。万象术不能照搬该行为。
* 不能使用固定的 100,000 或 120,000 token 作为所有未知模型的默认上限。未知模型可能只有 8K、16K 或 32K。错误使用 100K 会让 nudge 迟到数倍。
* 任何解析出的 limit 若低于 8192，一律回落到该 Host 的保守默认水位（如 16384 或 32768）。
* 全局默认必须偏小，并明确记录其 provenance，不能只返回一个无法审计的整数。

### 6.4 Reserve 与 OpenCode 语义协调

```text
EffectiveInputBudget = min(explicit input limit, context limit - output reserve)
```

reserve 应由 model output limit、tool call 余量、reasoning 模型额外余量、Host 配置共同决定。

### 6.5 provider list 临时不可用时

使用最近一次成功缓存；没有缓存时使用该 host 的安全保守默认；不得返回 0 并静默关闭。

Opencode、OMP、Mux 应共享这一逻辑，不要各自采用不同 reserve。

## 七、消息流水线中的正确位置

预算判定应放在：
1. 消息清洗；
2. backlog projection；
3. caps/system prompt 注入；
4. compaction 结果处理；
5. 其他稳定 synthetic 内容加入；

之后。然后：
6. 对将要发送的最终消息计算 token；
7. 决定是否加入 context-budget nudge；
8. 加入 nudge 后再次验证不会超出硬上限。

预算自身生成的 nudge 也占 token，不能假装它是免费的。

## 八、修复缓存

message transform 缓存 key 只能由结构化身份与单调计数构成：`modelRevision`、`modelLimitRevision`、`ContextUsageRevision`、`BacklogRevision`、`BudgetRevision`、`contextGeneration`、`CapsRevision` 及对应 scope/policy/phase 标识。不得计算或使用 raw message fingerprint、内容 hash 或其他内容摘要作为 key；最终 outbound 的规范化字节/稳定前缀相等只能作为可观察验证。

更干净的方案：稳定消息变换可以缓存；context budget 判定移到缓存之后，每次发送前重新计算。

## 九、todowrite 成功后的 phase 更新

收到本地 `work_backlog_committed` 后不应直接用旧 token 数值建立新 phase base。

正确流程：
1. 增加 todo checkpoint ordinal；
2. 标记当前 BudgetEpisode 完成；
3. 状态转为 `RebaseRequired`；
4. 清除本 episode 的重复 nudge；
5. 使 message transform cache 失效；
6. 下一轮对最终 outbound context 重新测量；
7. 建立新的 phase base。

当前实现已经在 backlog 与 `LastBacklog` 不同时重建 phase state 并更新 store。需要新增的重点是保证 event 到达后缓存必然失效、todo checkpoint ordinal 正确推进、下一次 transform 不返回旧缓存、phase 重建使用当前 context generation。

## 十、NudgeTrack 真正参与判定

至少区分：
* NotSignaled
* IncludedInOutboundPrompt
* AssistantProgressObserved
* TodoCommitted
* RetryAllowed
* Exhausted
* InvalidatedByCompaction
* InvalidatedByCancel

不能只写入 `EmergencySignaled`，却在下一轮继续无条件附加同一 prompt。

### 10.1 NudgeTrack 要表示 episode，而不是 transform 次数

当前 synthetic nudge 每次 transform 会被剥离，随后可能重新注入，并增加 `NudgeCount`。

更稳妥的状态应记录：
* context budget episode ID；
* signal 时的 todo ordinal；
* signal 时 tokens；
* stable synthetic nudge ID。

在压力仍成立且 todo ordinal 未推进时：输出中继续包含同一条提醒；不把每次 message transform 计作新提醒；不不断生成新 GUID；不快速耗尽"最多 2 次"限制。

只有以下情况才建立新提醒 episode：LLM 完成一次 todowrite 但压力仍再次跨阈值；phase baseline 重建；compaction 后重新进入压力区。

## 十一、开局防误触发

### 11.1 第一次观测只做校准，绝不触发提醒

`rebuildPhaseState` 应返回额外信息：是否为本次刚初始化的 baseline。若本次刚初始化：保存 state、保存 usage、不调用 emergency injection、直接返回原消息。

### 11.2 backlog 为空时也必须使用真实初始 baseline

删除特殊语义 `State=None && backlog.IsEmpty → phaseBaseTokens=0`。统一为 `phaseBaseTokens = stableTokens`。

即使 backlog 为空，CAPS、系统消息、首条用户消息也属于阶段基线，不属于阶段新增消耗。

初始化时若当前 tokens = 30,000，phase base = 30,000，则新增量为 0，不会立刻触发。

### 11.3 必须有正的阶段增长

在判定 emergency 前增加基本条件：`currentTokens > phaseBaseTokens` 或新增量超过最小噪声区间。避免 token 统计轻微抖动造成提醒。

### 11.4 phase base 已超过窗口 80% 时走 compaction，不再要求 todowrite。

## 十二、验收测试

### 开局测试

1. CAPS 很大，占窗口 30%：第一轮不提醒。
2. 没有真实 usage，只能 bytes/2：第一轮不提醒。
3. 首次 transform 连续调用两次：仍不提醒，baseline 不重复漂移。

### 正常触发测试

4. baseline 之后新增 token 未达阈值：不提醒。
5. 新增 token 跨过 F 阈值：注入一次紧急提醒。
6. 同一个消息集合重复 transform：提醒仍可见，不增加提醒 episode 数，synthetic ID 不变化。
7. 成功 todowrite：baseline 重建，提醒消失，不立即再次触发。
8. investigator 上下文超过阈值：不出现 todowrite 提醒。
9. phase base 已超过窗口 80%：走 compaction，不再要求 todowrite。

### 额外必测场景

* 公式边界前 1 token、正好边界、边界后 1 token；
* N、R、P 的性质测试；
* 历史存在 100 次 todowrite，但当前 phase 的 R 仍为 0；
* provider API 不可用；
* tokenizer 不可用；
* 第一次会话没有历史 usage；
* 中途从小模型切换到大模型；
* 中途从大模型切换到小模型；
* compaction 后 phase 正确重置；
* todowrite 失败时不能算 TodoObserved；
* 相同输入消息、usage 增长后必须重新判定；
* Opencode 和 OMP 对同一输入做出相同决策；
* 已经 Signaled 时不会连续插入重复 prompt；
* 自动 continue 期间预算 nudge 不与 fallback 抢 owner；
* 最后一条 assistant 无 usage 时能找到最新有效 observation；
* 不重复累加 cache.read；
* 大型 tool result 在本轮估算中被计入；
* transform 新增内容被计入；
* limit=0 时进入保守默认；
| work backlog commit 后下一轮必然重建 phase；
| 同一 budget episode 不产生无界重复 nudge；
| compaction 后 context generation 正确变化。
# 问题 4：DEBUG 与结构化日志

## 一、Flow 管线位置

本问题映射到管线上的 **effect 执行区之外**。日志不进入 `scan`（不改变状态），也不进入 effect flow（不是 Host I/O）。日志作为独立的诊断 sink，由 Step 中派生的 `LogEntry` 在 scan 外消费。

## 二、当前问题

至少已明确看到 context budget observation 直接向控制台输出：
* provider list 不可用；
* provider.list 调用失败。

这些属于可预期的能力探测和降级，不应默认污染用户控制台。`OpencodeContextBudgetObservation` 当前直接输出 provider list 不可用和 provider.list 失败信息。

## 三、OpenCode v1.17.13 日志契约

### stdout 禁止规则

OpenCode 的 Effect logger 默认走 stderr，但普通插件中的 `console.log` 会写 stdout。

在以下模式下 stdout 可能是协议通道：JSON output、MCP、ACP、subprocess、自动化脚本。

因此万象术生产代码中必须禁止：
* `console.log`
* 裸 `printfn "DEBUG"`
* 非协议 stdout write

### Effect logger

普通 Hook 无法直接调用 OpenCode 内部 Effect logger，因此万象术应维护自己的 logger adapter。

### console.log 性能

"Node.js 中所有 console.log 都会同步阻塞事件循环"是过度概括。其具体行为取决于输出目标（终端/文件/pipe）、Node 版本、平台、stream 实现。日志清理的主要理由应是：污染 Host 协议、破坏机器解析、泄露内部状态或用户内容、造成高频 I/O、缺少 severity/结构/限频、不利于测试和生产观测。

## 四、不要简单全局删除所有 WARNING

应先分类：

### 必须删除或改为 trace

* capability 不存在；
* 缓存未命中；
* 正常 fallback 选择；
* 正常 nudge 跳过；
* provider list 不支持；
* 某 optional API 不可用。

### 应改为 warn

* 本应有 model limit，但解析失败；
* 事件日志写入失败；
* schema 完整性检查失败；
* 状态 invariant 被破坏；
* 使用降级 token 估算。

### 应保留为 error

* 数据可能丢失；
* 下游工具执行状态未知；
* event log 与内存状态不一致；
* 用户 Esc 后仍检测到旧 continuation 被发送。

## 五、统一结构化日志

### 5.1 推荐日志出口

* trace/debug：独立文件；
* warn/error：stderr；
* CLI 明确用户结果：stdout；
* event log：NDJSON 专用文件；
* 不把调试日志混入 event sourcing 日志。

### 5.2 日志结构

每条日志至少包含：subsystem、event name、severity、timestamp、sessionID hash、messageID、humanTurnID、continuationID、contextGeneration、hostVersion、degradedReason。

### 5.3 禁止记录

* 完整 prompt；
* 完整工具参数；
* 用户文件内容；
* API key；
* token；
* 私密路径；
* review task 全文；
* 模型隐藏推理；
* 完整 stack 中的敏感路径。

### 5.4 不建议删除所有 Semble trace 调用

若 trace 已经有环境变量门禁、写入独立 sink、默认关闭、不泄露敏感数据，则可以保留。应该删除的是"绕过统一 logger 的裸输出"，而不是删除所有诊断能力。

## 六、输出策略

* 默认：只显示 warn/error。
* debug：显式配置开启。
* trace：只写文件或诊断 sink。
* TUI/插件运行时不得直接 `printfn` 或 `console.log`。
* 相同错误需要去重或限频，避免 provider API 每轮失败都刷屏。
* session 结束时可以输出一条汇总，而不是几十条过程日志。
* OpenCode 本身没有日志 rate limit，万象术需自行做错误去重和限频。

## 七、CI 静态门禁

生产模块新增以下内容时构建失败：
* `console.log`
* `printfn "DEBUG`
* 无 logger 包装的 stderr write
* 直接输出完整 prompt/args

测试模块和明确的 CLI 用户输出需要列入白名单。

## 八、验收

自动化测试启动完整插件并执行普通会话：
* stdout 不包含 `DEBUG:`；
* stderr 不包含普通能力探测；
* debug=false 时没有 debug 级日志；
* debug=true 时结构化字段完整；
* 日志中不含 prompt 和控制字段原值；
* 默认运行 stdout 中 `DEBUG:` 数量为 0；
* 敏感 prompt、工具参数不进入日志。
# 问题 5：compaction 与 nudge owner 仲裁

## 一、Flow 管线位置

本问题映射到管线上的两处：
1. **`scan` 内 terminalOrigin 分类**：只有 `HumanTurnCompleted` 才允许普通 nudge，其他终止来源跳过。
2. **`stateFlow |> map plan` 中的 lease demand 预览**：CompactionEpisode 活跃期间 NudgeBlockReason = CompactionActive；可变 owner 只在 step 的事务中由 canonical `ContinuationLease` 仲裁。

## 二、当前根因

当前 nudge 主要根据 session idle/error/status idle 判断"自然停止"。但 session idle 可能来自：真人回答结束、compaction 结束、title 生成结束、fallback continuation 结束、recovery 尝试结束、nudge 自己结束、tool subturn 结束。

这些事件被压扁成了同一个 `isNaturalStop`。

虽然代码会跳过 synthetic assistant agent（如 compaction、title），但其做法往往是继续往前寻找上一条非 synthetic assistant。这样 compaction 刚完成时，系统可能拿到压缩前那条旧 assistant 消息，误以为它刚刚自然结束，然后发 nudge。

另外，fallback 的 `Consumed` 只反映某一瞬间状态机是否消费了事件。fallback transition 一旦把 phase 改成 Idle，后面的 nudge 观察者可能看到 `Consumed=false`，误以为事件没人处理。

当前 nudge snapshot 已经有 work state、block status、anchor 和 dedup，但没有"终止事件来源"和"当前 continuation owner"这两个关键维度。

## 三、OpenCode v1.17.13 compaction 协议

目标版本具有：
* `experimental.session.compacting`
* `experimental.compaction.autocontinue`
* `session.compacted`
* `assistant.summary = true`
* `part.synthetic = true`

OpenCode compaction 默认会执行 auto-continue。它会直接创建 synthetic user message，不经过 `chat.message` Hook，并最终产生普通 assistant 过程和 idle。这正是"压缩后错误触发 nudge"的官方协议根源。

## 四、引入 TerminalEventOrigin

Nudge 不再只接收 `naturalStop=true/false`。它应接收明确分类：

* HumanTurnCompleted
* HumanTurnAborted
* CompactionSummaryCompleted
* CompactionContinuationCompleted
* FallbackContinuationCompleted
* TitleCompleted
* NudgeCompleted
* ToolSubturnCompleted
* Unknown

默认只有 `HumanTurnCompleted` 可以进入普通 todo/review nudge 判定。`Unknown` 应保守跳过，而不是积极 nudge。

terminalOrigin 在 `scan` 的 Step 中携带，纯 `deriveAction` 继续只消费结构化 snapshot，不负责扫描 Host 原始消息。

## 五、continuation owner 仲裁

### 5.1 SessionGateDemand 统一优先级

参见 PRD-01。每个 session 同时只能有一个能发送 synthetic prompt 的 owner，且 owner 的唯一权威表示是 `ContinuationLease.owner` 及其 lease 状态。不得再维护独立的 fallback owner、nudge owner 或 current continuation owner 状态并让它们互相仲裁。owner 必须通过对应 lease 的 settle/cancel/fail 事件释放，不能只看 session 当前是否 idle；已开始 dispatch 的取消必须进入 `DispatchUnknown`/`Reconciling`，在事实收敛前不得释放给另一 owner。

### 5.2 NudgeBlockReason

纯 `deriveAction` 不反向依赖 Shell 可变 runtime。Shell/host adapter 读取 fallback observation 后转换为 `NudgeBlockReason`，构造 snapshot，传入纯函数。

建议从二值状态扩展为原因枚举：Allowed、UserCancelled、FallbackActive、CompactionActive、SyntheticTurn、PendingDelivery、RunnerOwnsTurn、DuplicateAnchor、UnknownTerminalOrigin。`NudgeBlockReason` 只表达 snapshot 中的可观察门禁，不拥有或改变 lease；owner 决定只能在 canonical lease 事务中发生。

## 六、CompactionEpisode 生命周期

### 6.1 建立状态

```text
CompactionEpisode
- episodeID
- sessionID
- contextGenerationBefore
- state
- autoContinueEnabled
- summaryMessageID
- continueMessageID
- startedAt
```

状态至少包括：Compacting、SummaryProduced、AutoContinuePlanned、Compacted、ContinuationObserved、ContinuationRunning、Settled、Cancelled、Failed。

### 6.2 开始信号

`experimental.session.compacting` 是最可靠的开始信号。在此 Hook 中：
* 创建 compaction episode；
* 将 NudgeBlockReason 设为 CompactionActive；
* 保存旧 context generation；
* 可向 `output.context` 注入 review task 等关键上下文。

### 6.3 auto-continue 信号

`experimental.compaction.autocontinue` 中记录 `AutoContinuePlanned`，读取默认 `enabled`，通常保持 enabled=true，不要再由 fallback 或 nudge 重复创建 continuation。

除非产品明确要求完全接管 compaction continuation，否则不应关闭官方 auto-continue。

### 6.4 识别 Compaction summary

稳定信号优先 `assistant.summary = true`。`agent="compaction"`、`mode="compaction"` 可以作为兼容辅助，但不是最终协议。

### 6.5 识别 synthetic continue

目标版本中 synthetic 标记位于 part，`part.synthetic=true` 是稳定字段，`part.metadata.compaction_continue=true` 是内部字段。该消息不经过 `chat.message`，会通过普通 message.updated 被观察到。

近期识别顺序：当前存在 CompactionEpisode → summary 已产生 → 随后出现 synthetic user part → metadata 可用于确认但不是唯一依据。

### 6.6 session.compacted

`session.compacted` 可以确认压缩处理已完成，但不能单独证明 auto-continue 已 settle、下一轮 assistant 已完成、现在可以发普通 nudge。因此从 Compacted 到 Settled 期间仍然阻止 todo/review nudge。

### 6.7 何时解除阻塞

若 auto-continue enabled：
* 观察 synthetic continuation；
* 观察其对应 assistant；
* 等该 assistant 成为 terminal；
* 确认没有 Abort；
* 才将 episode 设为 Settled。

若 auto-continue disabled：
* 在 `session.compacted` 后；
* 确认 Host idle；
* 确认没有 pending continuation；
* 再解除阻塞。

禁止使用固定 100ms、500ms 或"一秒窗口"作为正确性条件。

### 6.8 compaction 完成后

* 增加 context generation；
* 清除旧 assistant anchor；
* 清除基于压缩前消息生成的 pending nudge；
* 不触发普通 nudge；
* 等下一次真正人类轮次完成后再重新判断。

不能通过"忽略 compaction agent，然后使用前一条 assistant"来补偿。

## 七、fallback auto-continue 的处理

fallback continuation 应从开始到 settle 一直拥有该终止事件。流程：
1. fallback 请求 continuation；
2. 取得 owner；
3. continuation busy/idle/error 全部归 fallback episode；
4. fallback 判断是否继续下一个模型或结束；
5. 发出 `fallback_episode_settled`；
6. 释放 owner。

普通 nudge 不能在第 3 步看到某个 idle 就抢跑。

规范行为只有一种：`fallback_episode_settled` 写入后，该 fallback episode 不再发出任何 todo/review nudge，也不因 backlog 重新争抢 lease。backlog 只能在下一次真正的 `HumanTurnCompleted` 终止事件上重新进入 nudge 判定；settle 后的 idle、error 或 stale continuation 事件不得制造补偿 nudge。

## 八、Fallback/Compaction 状态不能侵入纯推导核心

不应让 `Kernel.NudgeDerivation.deriveAction` 直接依赖 FallbackRuntime。

`deriveAction` 目前是纯函数：输入 snapshot，输出 `NudgeAction`。直接把可变 runtime 注入内核会造成：Kernel 反向依赖 Shell、单元测试变复杂、host-specific 状态进入纯领域层、重放结果可能依赖当前内存状态。

更合理的方式：Shell/host adapter 读取 fallback observation，将其转换成 `NudgeBlockReason`，构造 snapshot，`deriveAction` 继续只根据 snapshot 判定。

## 九、Anchor 只能辅助迁移

仓库已定义 `compactionAnchorBody`、`hasCompactionAnchorPrompt`、compaction front-matter 提取和重建机制。

近期补丁中利用 anchor 辅助识别 compaction continuation 具有较低实施成本。但 anchor 有以下局限：文案未来可能改变、用户可能自己输入相同文本、compaction prompt 可能被截断、front matter 可能被重新编码、不同 Host 使用不同 wording。

最终系统不能根据一句英文文本决定 continuation 身份。Anchor 的正确地位是：旧历史兼容、调试、迁移期补充证据。最终身份仍来自 provenance 和 event projection。

近期补丁应在 Host adapter 收集 snapshot 时识别最后终止事件来源、最近 synthetic prompt origin、compaction anchor 是否存在、compaction 是否刚完成、是否正在 auto-continue，然后转换成 `NudgeBlockReason.CompactionActive` 或类似状态。纯 `deriveAction` 仍只消费结构化 snapshot。

## 十、Phase 条件修正

不能写成含糊的"Phase 不等于 Idle 或 Exhausted"。应明确：
* Phase 属于 Retrying/Scanning/ScanningToolCallText/RecoveringToolCallText：阻止；
* Phase 为 Idle：仍需检查 owner、gates 和 terminal origin；
* Phase 为 Exhausted：必须等 fallback episode 明确 settled 后再决定；
* Lifecycle 为 Cancelled/TaskComplete：直接阻止 nudge。

会话开始时（尚未观察到首个有效 `HumanTurnCompleted`）禁止发送 emergency todo prompt，也不得以 todo backlog、初始 idle 或恢复事件绕过该时间门槛。首个对话启动只建立 projection/lease demand；只有真正人类轮次完成且没有更高优先级 lease/block 时，才可进行一次普通 nudge 判定。

## 十一、必测事件序列

* human answer → normal idle：应允许一次 nudge。
* human answer → compaction started → compaction idle：零 nudge。
* title generation idle：零 nudge。
* fallback send → busy → idle → next fallback：零普通 nudge。
* fallback settle：本 episode 零重复 nudge。
* fallback settle 后 backlog 仍在：直到下一次 `HumanTurnCompleted` 前零 nudge。
* nudge 自己的 prompt 完成：不能递归 nudge。
* conversation start（没有 `HumanTurnCompleted`）：零 emergency todo prompt。
* Esc → idle：零 nudge。
* compaction 后旧 assistant 仍在历史：不能拿它作为新 anchor。
* session.error 被 fallback 消费后又出现 status idle：仍然只能有一个 owner。
* restart replay 时 owner 和 episode 恢复一致。
* `experimental.session.compacting` 打开 block；
* autocontinue planned 时普通 nudge 为零；
* summary assistant 不被视为真人完成；
* synthetic continue 不经过 chat.message 也能识别；
* `session.compacted` 不立即解除 block；
| auto-continue settle 后才解除；
| compaction Abort 时 cancellation 优先；
| 用户输入相同文案不被误判。
# 问题 6：model routing 与人类轮次路由

## 一、Flow 管线位置

本问题映射到管线上的 **HumanTurnRoute 从 `chat.message` Input 派生**，进入 `HumanTurnProjection`（PRD-09）。普通 todo/review nudge 只使用此 projection 中的 route，不读取 session 级 stale injected model。

## 二、当前根因

当前模型解析存在三种来源混用：
* 最近 assistant 的 model；
* 最近 user message 的 model；
* fallback runtime 中最近 injected model。

其中 injected model 是 session 级缓存，而且真人新消息时没有在所有路径稳定清除。

典型错误：
1. 用户用模型 A 发起任务；
2. fallback 切到模型 B；
3. 用户随后用模型 C 发新消息；
4. todo/review nudge 仍读取旧 injected model B。

OMP 某些路径直接从最后 assistant 选择模型。compaction/title 或旧 assistant 都可能改变结果。

`Opencode/NudgeEffect.collectSnapshot` 当前从 assistant info 中读取 model 时只接受 `providerID`/`modelID` 组成的对象。如果 model 是 `"openai/gpt-4o"` 字符串，局部解析会返回 `None`。仓库中已存在 `FallbackMessageCodec.decodeModelFromObj`，能够同时处理字符串和对象。

## 三、OpenCode v1.17.13 模型字段形态

官方字段形态不同：

```text
UserMessage:
  model.providerID
  model.modelID
  model.variant

AssistantMessage:
  providerID
  modelID
  variant

Session:
  model.id
  model.providerID
  model.variant
```

不能用一个只支持对象或只支持字符串的局部解析器处理所有来源。

## 四、模型事实拆分

### 4.1 HumanTurnRoute

来自真实 user message，在 `chat.message` Hook 中建立：providerID、modelID、variant、agent、messageID、humanTurnID。

普通 todo/review nudge 使用这个事实。它不属于 session 全局，只对当前 human turn 生效。

### 4.2 HostObservedRoute

来自 `chat.message`、`chat.params`、Session model 查询。其中 `chat.params` 最接近实际 LLM 请求边界，但也可能属于 compaction、title 或其他 agent，必须结合当前 owner 分类。

### 4.3 FallbackAttemptRoute

只属于某个 fallback continuation lease。只在 fallback 自己发送 continuation 时使用。

### 4.4 ObservedSessionRoute

表示 Host 当前报告的 session active model。有价值观测来源，但必须带 observation event ID、generation、observedAt、source、confidence。它可以帮助填补缺失信息，但不能无条件覆盖更明确的 HumanTurnRoute。

## 五、模型选择优先级

### 5.1 普通 todo/review nudge

1. 当前 HumanTurnRoute；
2. 与当前 humanTurnID 和 generation 匹配的 HostObservedRoute；
3. Session 当前模型；
4. Agent 默认模型；
5. 最后非 synthetic user model，兼容回退。

明确禁止：旧 injected fallback model、compaction assistant model、title model、无 generation 的 runtime cached model、最后 assistant model 无条件覆盖。

### 5.2 fallback continuation

1. 当前 FallbackAttemptRoute；
2. 当前 HumanTurnRoute；
3. fallback chain 默认。

Fallback 模型不能跨 human turn 生效。

### 5.3 不存在 current human turn 的恢复场景

从 EventLog 恢复 HumanTurnRoutingProjection；无法恢复时使用明确默认；不使用无作用域的 stale injected model。

## 六、当前 `resolveNudgeModel` 的问题

当前对普通 user message 的处理顺序大致是：
1. `fallbackRuntime.GetInjectedModel`
2. 当前 user message model
3. `fallbackRuntime.GetModel`
4. assistant/default model

这意味着 session 中只要残留 injected model，它就可能覆盖后一条真人消息显式选择的模型。短期应改为：
1. 最近一条确认是真人的、非 nudge、非 fallback synthetic message 的显式模型；
2. 当前 human turn 保存的 routing context；
3. session/agent 默认；
4. 最后非 synthetic assistant model，仅作为兼容回退。

## 七、`FallbackRuntimeState.GetModel` 不是 SSOT

当前 `FallbackEventBridge` 在识别 `NewUserMessage` 时会清空 fallback chain、调用 `runtime.ClearModel sessionID`、重置 fallback state。这说明 runtime model 至少在当前实现中是 fallback 运行期缓存，不是稳定的当前用户轮次路由事实。

它还可能存在：尚未捕获新模型、session.updated 事件缺失、旧事件晚到、fallback attempt model 覆盖用户选择、重启后内存丢失、多个 child session 混淆、model 存在但 variant/reasoning effort 等信息不完整。

Runtime map 可以加速访问，但不能成为跨重启、跨竞态的权威事实。

## 八、`session.updated` 和 `session.busy` 捕获模型

在 Host event 中及时调用 `SetModel` 有价值，但需升级为版本化 observation。不能只做 `sessionID -> model`，而应记录：session ID、human turn ID、event sequence、session generation、source event、route、observed timestamp。

旧 generation 的 session.updated 晚到时必须丢弃。

## 九、统一 model decoder

当前 `Opencode/NudgeEffect.collectSnapshot` 从 assistant info 中读取 model 时只接受 `providerID`/`modelID` 组成的对象。如果 model 是 `"openai/gpt-4o"` 字符串，局部解析会返回 `None`。

仓库中已存在 `FallbackMessageCodec.decodeModelFromObj`，能够同时处理字符串和对象，因此不应再编写第二套不完整解析器。

应当统一以下全部调用同一 decoder 或同一 model identity codec：
* `collectSnapshot`
* last user model 提取
* last assistant model 提取
* fallback route 恢复
* provider limit 模型提取

## 十、chat.message 是真人轮次的重要入口

普通用户提交 prompt 时会触发 `chat.message`，包含：sessionID、messageID、agent、model、variant、parts。

应在此建立 HumanTurnRoute。但 compaction synthetic continuation 不经过该 Hook，所以不能把"没有 chat.message"解释成事件缺失；它可能是官方 synthetic message。

## 十一、chat.params 用于校验实际模型

`chat.params` 可以观察到即将发送给 Provider 的真实模型。建议记录：

```text
RouteObservation
- owner
- sessionID
- humanTurnID
- model
- variant
- agent
- source = ChatParams
```

如果 owner 是 Human：更新当前 human route observation。如果是 Fallback：更新 fallback attempt route。如果是 Compaction：只更新 compaction observation。如果是 Title：不得污染 human route。

## 十二、Nudge 必须显式传递模型

调用 OpenCode `session.prompt` 时应显式设置 `model.providerID`、`model.modelID`、`model.variant`、`agent`。不能只发送 text 和 custom type，然后依赖 Session 默认。

OMP 当前 `sendMessage` 主要传递 customType、content、display、triggerTurn、deliverAs，模型并没有从 snapshot 显式传递给主机。应完成两件事：snapshot 模型来源改为当前 human turn；按 OMP 主机真实支持的 API 字段显式传递 route。

## 十三、reasoning effort 等无法完全恢复

官方消息和 Session 不持久保存 temperature、topP、reasoning effort、thinking、provider-specific options。这些由 agent、model 和当前配置在请求时重新计算。

HumanTurnRoute 至少应可靠保存 provider、model、variant、agent。其他参数若来自万象术自己的 fallback config，可在 FallbackAttemptRoute 中保存；若完全由 OpenCode 内部计算，普通 nudge 应服从当前 Host 配置，而不是伪造历史参数。

## 十四、Fallback model 的作用域

fallback 选中的 model 应保存为：humanTurnId、continuationId、attempt、selected route。它只对这次 fallback attempt 生效。

以下情况必须清除：真人新消息、Esc、fallback episode settle、task complete、session cleanup、session generation 改变。

## 十五、事件日志调整

折叠为三个独立部分：
* LastHumanTurnRouting
* CurrentFallbackAttemptRouting
* LastAssistantRouting

nudge 只使用 LastHumanTurnRouting。fallback 自己执行时使用 CurrentFallbackAttemptRouting。诊断和 UI 才使用 LastAssistantRouting。

## 十六、必测场景

* Human A → nudge：A。
* Human A → fallback B → fallback continuation：B。
* Human A → fallback B → Human C → nudge：C。
* Human C → compaction model D → nudge：仍为 C。
* Human C → title model E → nudge：仍为 C。
* Human C 无 model → 使用 agent/session default。
* 进程重启后从事件日志恢复：仍为 C。
* model 相同但 variant 不同：variant 必须保留。
* stale injected event 晚到：不能覆盖新 human turn。
* 真人 user model 成为 HumanTurnRoute；
* `chat.params` 按 owner 分类；
* compaction/title 不污染 human route；
* 旧 injected model 不影响新轮次；
* nudge 请求显式携带 model/variant/agent；
* fallback attempt route 不跨 turn；
| 字符串、User object、Assistant flat fields 均可解析。
# 问题 7：review nudge 原始任务

## 一、Flow 管线位置

本问题映射到管线上的 **`ReviewProjection` 从 Events fold 派生**。原始任务的 SSOT 是 `loop_activated` 事件 → `ReviewLoopFold.Active task` → `ReviewProjection`。构建 Nudge Snapshot 时调用 `activeTask`，派生 `reviewTask`，PromptContext 再输出。

## 二、当前数据已经存在但被丢弃

当前 review loop fold 会保存 Active task。`loop_activated` 事件包含 task，`ReviewLoopFold.Active task` 也能恢复它。review submission prompt 还已经使用 `original_task` front matter。

但数据在传到 nudge snapshot 时被丢掉：
* `NudgeSnapshotSource` 有 reviewLoop；
* `SessionSnapshot` 只有 todos、assistant text、work state、model 等；
* 没有 original task；
* `NudgeLoop` 最后只调用基于 todos 的 loop nudge prompt。

也就是说，这不是"任务没保存"，而是在 ReviewLoopFold → NudgeSnapshotSource → SessionSnapshot → loopNudgePromptFor 这条转换链中丢失。

当前 `loopNudgePromptFor` 只接收 todos，并不会携带原始任务。

## 三、修复方案

### 3.1 扩展 SessionSnapshot

当 review loop Active 时，snapshot 必须包含：
* originalTask
* reviewLoopId 或 reviewSessionId
* currentRound
* latestVerdict
* latestFeedback
* todos
* nudgeAnchor
* humanTurnId

### 3.2 不复制两个独立 task 状态

不建议同时维护 `reviewLoop = Active task` 和 `originalTask = Some task` 两个可变字段。

更安全的做法：task 继续只存于 `ReviewLoopFold.Active task`；`sessionSnapshotFromFold` 调用 `activeTask snap.reviewLoop`，将结果写入 `SessionSnapshot.reviewTask`；prompt 生成只读取这个值。

只有在确有性能或序列化需求时才增加派生字段，而且应在 fold 后计算，而不是作为独立可变状态维护。

### 3.3 建立统一 ReviewPromptContext

所有 review prompt 生成器只能从这个 context 构建。字段可以按场景选择是否显示，但以下不变量强制：
* review loop Active ⇒ originalTask 非空；
* review nudge ⇒ originalTask 必须输出；
* needs revision ⇒ originalTask 和 feedback 必须输出；
* double check ⇒ originalTask 必须输出；
* reviewer submission ⇒ originalTask 必须输出。

建议至少包含：original task、current round、latest feedback、affected files（若已提交过）、current todos、review loop ID、current human turn ID、prompt origin。

### 3.4 selectNudgePrompt

`selectNudgePrompt` 在处理 `NudgeLoop` 时调用新的 review 专用模板，输入完整的 review context，而不是只传 todos。

## 四、严禁使用 `task` 字段名

在 nudge prompt 渲染的 front-matter 中，键名决不能写成 `task`。

因为万象术在重启重放（Replay）时，其折叠逻辑（`inferReviewTaskFromTexts`）只要看到 `task` 字段，就会误判定为"用户重新发起了一次 With-Review 激活命令"。这会导致 review 状态和 version 发生严重错乱。

仓库已经明确区分：
* `task`：Worker With-Review 激活字段；
* `original_task`：Reviewer、double-check 和后续评审上下文使用。

Review nudge 应使用：

```yaml
original_task: <完整原始任务>
prompt_origin: review_nudge
review_loop_id: ...
review_round: ...
```

其中 `prompt_origin` 不应参与 loop 激活折叠。review nudge 不会被误识别为新的 loop activation。

## 五、跨 Compaction 的 Projection 权威路径

Compaction 后必须从持久 `ReviewProjection`（由 EventLog fold 重建）恢复 `original_task`。在构建 compaction continuation prompt 时，由该 Projection 重新注入 `original_task`、review loop identity、round 和必要 feedback；Projection 是跨 Compaction 的长期 SSOT，不能依赖压缩文本是否保留字段。

`experimental.session.compacting` 的 `output.context` 可以把 Projection 内容注入 summary 以提高可读性，但这不是身份恢复或正确性的替代路径。summary、anchor 和普通历史文本都不能取代 Projection reinjection。

在 `experimental.session.compacting` 阶段通过 `output.context` 注入当前 Projection 内容是可选的 summary 增强；真正用于 continuation 的物理重注入必须发生在 compaction 后的 continuation prompt 构建处。

## 六、Compaction whitelist

当前 compaction front-matter 白名单包括：task、verdict、double-check、squad_event。没有 `original_task`。

该白名单是旧版本兼容面；其没有 `original_task` 不得被当作 Projection 数据缺失，也不得驱动新逻辑复制 `task`。

白名单只允许作为迁移期兼容措施：旧版本若只能从 compaction front-matter 读取字段，可临时把 `original_task`、`prompt_origin` 和必要的 review loop identity 加入 whitelist，以完成历史数据迁移。白名单不是长期正确性路径，不能成为 Projection 的替代，也不得与 Projection 形成两个可变 SSOT。

迁移完成后，Projection reinjection 是唯一长期路径；新逻辑不得要求 whitelist 才能恢复 `original_task`，并应移除仅为迁移而保留的 whitelist 依赖。

## 七、Active loop 缺 task 时 fail closed

若 review loop 是 Active，但 originalTask 为空：
1. 不发送 review nudge；
2. 尝试从 event log 重新折叠；
3. 若仍无法恢复，记录 invariant violation；
4. 不允许退回无任务的普通 loop prose；
5. 不发送一个无任务 prompt 让 LLM 猜。

## 八、注意 task 与 original_task 的语义区分

当前系统里 `task` 往往用于激活 worker review mode；`original_task` 用于 reviewer 评审上下文。

不要为了补字段，简单把 `task` 到处复制，否则 review nudge 可能被误识别成一次新的 review 激活命令。

* 初次进入 With-Review Mode：使用 `task` 表示激活命令的任务。
* 后续 review nudge、double-check、reviewer continuation：统一使用 `original_task`。
* prompt origin 和 review loop ID 明确表明这是一条 continuation，而不是激活新 loop。

## 九、必测场景

* Active review，无 todos；
* Active review，有 todos；
* needs revision 后继续；
* WIP submit 后继续；
* compaction 发生在 loop activation 和 nudge 之间；
* 进程重启后恢复；
* original task 包含多行、冒号、引号、YAML 特殊字符；
* reviewer child session 收到 nudge，不应重新激活 worker loop；
* task 缺失时零 prompt；
* 多个 review round 始终保持同一 original task；
* review nudge 只包含 `original_task`，不重新生成激活字段 `task`；
* active loop 缺 task 时不发送；
| compaction 后仍从 EventLog 恢复。
# 六个核心投影

## 一、投影的 Flow 管线地位

在 Flow-first 架构中，投影不是独立的可变状态，而是从 `scanCommit` 已提交的
Events 派生的只读快照。`SessionState` 是这些投影的唯一产品（projection product）；
不得再维护 `currentProjections` 作为第二事实源。EventLog 是持久事实，投影是已提交
事件的折叠结果。

```text
step(SessionState snapshot, Input)
  → candidate State / Events / Effects
  → append to workspace journal
  → publish SessionState (projection product)
  → projection fold (pure function)
  → readonly snapshot
```

所有策略函数（`FallbackPolicy.tryPlan`、`CompactionPolicy.tryPlan`、
`ContextBudgetPolicy.tryPlan`、`ReviewPolicy.tryPlan`、`TodoPolicy.tryPlan`）只消费
同一个 `SessionState` snapshot，不读取 Shell 可变 runtime。它们在 `step` 内执行并
产出 candidate；`stateFlow |> map plan` 只能提供 UI/诊断预览，不能取得 lease、写事件
或 dispatch。

## 二、HumanTurnProjection

保存：
* currentHumanTurnID
* userMessageID
* route（HumanTurnRoute：providerID、modelID、variant、agent）
* lifecycle
* generation
* openedAt
* terminalOrigin

用于阻止旧 fallback、旧 nudge 和旧 model observation 污染新轮次。普通 todo/review nudge 的模型来源。

## 三、CancellationProjection

保存：
* cancelGeneration
* tombstone（CancelTombstone）
* source event
* affected turn
* invalidated continuation IDs

Abort 事件被确认后建立取消墓碑。`session.idle` 本身不能创建取消墓碑，但在已有取消证据后可以帮助确认 Host 正在收尾。

所有 Host translator 应把用户取消、客户端取消、stream abort、session interrupted、AbortError、MessageAbortedError、SDK cancellation token、主机明确的 stop-by-user 归一化为同一种领域事实。

## 四、ContinuationProjection

保存：
* continuation lease（ContinuationLease）
* owner
* route
* target turn
* dispatch state
* host messageID
* invalidation reason

dispatch state 必须显式区分：
`DispatchClaimed → Dispatching → Dispatched → Running → Settled`，以及无法确认 Host
是否收到请求时的 `DispatchUnknown → Reconciling`。`DispatchClaimed` 必须在物理 dispatch
前由单次 journal 事件写入原子取得；`DispatchUnknown` 只能通过 reconcile 收敛，不得
盲目重发。所有 result 绑定 continuation ID、claim sequence 与 CausalityContext，
迟到 result 只写 stale 事件。

所有 synthetic prompt 发送前必须验证 lease。`flatMapMerge` 执行 effect 前，在 session mailbox 中重新读取最新 `cancelGeneration`。

## 五、CompactionProjection

保存：
* episode（CompactionEpisode）
* summary
* auto-continue
* synthetic message
* context generation
* settle state

CompactionEpisode 活跃期间 NudgeBlockReason = CompactionActive。禁止使用物理时间窗口。

## 六、ContextBudgetProjection

作为第六个独立投影，维护 phase、ordinal 和 measurement。

保存：
* contextGeneration
* compaction state
* budget episode
* phase base
* phaseStartTodoOrdinal
* currentTodoOrdinal
* measurement provenance
* NudgeTrack episode
* UsageConfidence

`R = currentTodoOrdinal - phaseStartTodoOrdinal`，不从全历史计算。

## 七、ReviewProjection

保存：
* original task
* loop ID
* current round
* latest verdict
* latest feedback
* active/inactive

Nudge snapshot 不再从零散消息中自行推理，而是组合这四个投影（HumanTurn、Continuation、Compaction、Review）加上 ContextBudgetProjection。

## 八、投影的持久化

### 8.1 恢复

重启后从 workspace 的 NDJSON journal 逐行 fold 恢复所有投影。快照只是书签非真理，
只能记录已应用事件总数、最后已提交序号和 canonical byte/prefix equality 所需的
前缀范围。恢复时按计数与提交序号校验快照覆盖的前缀；不相等就从该前缀重新重放，
不得用内容 hash 作为缓存正确性、缓存 key 或事件身份。

### 8.2 事件 schema 演化

事件结构变更每条携版本号，旧版逐级升级转最新语义，升级函数纯且幂等，不读时钟不碰网不依赖环境。

### 8.3 物理载体

NDJSON 一行一个自包含事件，追加只碰末尾，恢复逐行读取折叠。恢复时首行损坏应在损坏处截断，不跳过后续行。

### 8.4 并发安全

一个 workspace 只有一个 journal path；启动或 append 由该 path 的单个进程内
`JournalWriter` 执行，并以 interprocess lock 防两个实例同时读写撕裂历史。按 session
过滤只是 projection 视图，不是独立日志。

## 九、投影与 Flow 的关系

投影在 `scanCommit` 成功发布后更新；投影本身作为只读快照对外暴露。外部消费者
（nudge、fallback、review 策略）通过已发布 `SessionState` 获取当前快照。计划执行仍
发生在 `step` 内；`stateFlow |> map plan` 仅为 UI/诊断预览。

```fsharp
// 规范边界：没有 currentProjections 第二 SSOT
let candidate =
    SessionKernel.step sessionState input
let committed =
    journal.Append candidate.Events       // success is required
    |> ignore
let sessionState' =
    SessionState.applyCommitted candidate // one projection product
```

策略函数：

```fsharp
let plan sessionState =
    policies
    |> List.choose (fun policy -> policy sessionState)
    |> List.sortBy Candidate.priority
    |> List.tryHead
```

上述代码只描述 `scanCommit` 的事务边界：真实实现必须在 append 成功后才执行
`SessionState.applyCommitted`，append failure 必须 poison session 并阻断 synthetic
prompt。任何 `currentProjections` map、先 fold 后 append 或 stateFlow 驱动执行均为
非规范实现。

## 十、Model SSOT 校正

不是 `FallbackRuntimeState.models Map`，也不是 `currentProjections`。应拆分为：
* 普通用户轮次模型：HumanTurnProjection.route
* fallback 模型：ContinuationProjection.lease.route
* Host 当前模型：ObservedSessionRoute（带 generation 的 observation）
* 内存 runtime map：上述投影的缓存或执行索引

Runtime map 可以加速访问，但不能成为跨重启、跨竞态的权威事实。

### 10.1 SessionEpoch 与 CausalityContext

`SessionEpoch(sessionID, sessionGeneration)` 标识 session 生命周期；新 session/真正
重启才递增 `sessionGeneration`。`CausalityContext` 绑定每个 input/effect 的
`humanTurnID`、`sessionGeneration`、`cancelGeneration`、`contextGeneration`、
`continuationID` 与 owner。session-start observation 可在显式
`ObservationOptions(allowUnowned = true, allowSessionStart = true)` 下使用
`owner = None`、`humanTurnID = None`；不得伪造身份。compaction 只递增
`contextGeneration`，不递增 `sessionGeneration`。

### 10.2 Count-based cache semantics

缓存正确性由 generation、revision/counter、已应用事件数和 canonical bytes/prefix
equality 的可观察断言决定。缓存 key 只能使用这些稳定 revision/counter 以及模型、
phase 等结构化身份；禁止使用内容 hash、fingerprint 或 hash 截断作为 key 或正确性
判定。cache miss、degraded observation 和 stale revision 都必须显式进入
`ContextBudgetObservation`，不得静默返回旧值。

## 十一、Task SSOT

原始 review task 应来自 `loop_activated` 持久事件，由 ReviewLoopFold/ReviewProjection 恢复。不从 LLM 历史自然语言中重新猜测。prompt 中使用 `original_task` 语义。

## 十二、Abort SSOT

Abort 是硬中断，但完整实现应覆盖三个层次：
1. Event translator：统一识别；
2. State machine：进入 Cancelled，不产生新 Action；
3. Effect executor：使旧 Action 失效。

只有前两层而没有第三层，仍无法阻止已经排队的 SendContinue。
# 兼容旧事件日志

## 一、原则

新增事件时不要破坏旧 `.wanxiangshu.ndjson`。建议提高事件 schema version，并提供纯迁移规则。

## 二、对旧 fallback 事件

旧 `fallback_continue_injected` 没有 continuation ID 时：
* 可以恢复"曾存在一次 fallback attempt"；
* 不能据此解除 cancellation；
* 不能据此覆盖当前真人模型；
* 重启后若状态不明确，保守结束旧 episode，不自动发送新 prompt。

## 三、对旧 assistant snapshot

缺少 humanTurnId 时：
* 可用于展示；
* 不可作为 nudge model 的高优先级来源。

## 四、对旧 review loop

如果 `loop_activated` 中有 task，恢复到 ReviewProjection。

如果 active 状态没有 task：
* 标记损坏；
* 不发 review nudge。

## 五、对旧 budget state

没有 phase ordinal/generation 时：
* 从当前上下文建立新 phase；
* 不沿用全历史 todo 数量；
* 首次恢复时执行一次保守测量。

## 六、迁移规则要求

事件结构变更每条携版本号，旧版逐级升级转最新语义。升级函数纯且幂等，不读时钟不碰网不依赖环境。否则同一历史不同时间重放出不同世界。

## 七、物理载体

NDJSON 一行一个自包含事件，追加只碰末尾，恢复逐行读取折叠。普通 JSON 数组追加要改已有结构，风险和语义都错。恢复时首行损坏应在损坏处截断，不跳过后续行。事件前后相扣，缺了中间后续事实就建在错基上，宁可少恢复一步，不恢复矛盾态。

历史变长格式演化机器故障需要少而硬的约束：快照只是书签非真理，要记录已提交事件计数、最后提交序号和完整状态前缀。恢复按计数与提交序号校验连续性，对不上就弃快照从头重放，不靠内容 hash、文件大小、字节数或修改时间猜测对齐。

生产环境每个 workspace 使用一个 NDJSON journal，事件携带 `sessionId`；进程内由该路径唯一的 `JournalWriter` 串行追加，跨进程再用排他锁防止并发写入撕裂历史。按 session 过滤只产生 projection 视图，不创建独立事实日志。测试用例使用独立 workspace，因此天然隔离。

## 八、审核验证

旧事件重放后：
* fallback 事件不能解除 cancellation；
* 旧 assistant model 不能覆盖 human turn route；
* 旧 budget phase 不沿用全历史 todo；
* 旧 review loop active 但无 task 标记为损坏；
| 迁移函数纯且幂等。
# 实施顺序（Flow-first）

## 一、原则

不要七个问题同时大改。每个 batch 内多文件可并行实施，batch 间有依赖。纯静态分析优先验证，编译测试需要 60s 尽量减少无谓测试。

三句取舍原则贯穿全程：
* 宁可少自动继续一次，也不能违反用户 Esc；
* 宁可少发一次 nudge，也不能在 compaction/fallback 后重复抢控制权；
* 宁可因状态缺失阻止 review continuation，也不能让 LLM 在没有原始任务的情况下猜。

## 二、P0：立即阻止错误行为

1. 将唯一生产输入管线改为 `scanCommit`：`step(snapshot)` 先生成 candidate
   State/Events/Effects，单次 append 成功后才发布 `SessionState`/accumulator/effects；
   journal failure 使 session poisoned，并阻断 synthetic prompt。
2. 固定 workspace journal 拓扑：一个 journal path、一个进程内 `JournalWriter`，并以
   interprocess lock 保护 append；投影不得各自写日志。
3. 先引入 `SessionEpoch` 与 `CausalityContext`（包括 session-start 无 owner observation
   options），再引入 `ContinuationLease` 原子 claim：在任何物理 dispatch 前写入
   `DispatchClaimed`，并实现 `Dispatching`、`Dispatched`、`Running`、`Settled` 与
   `DispatchUnknown`/`Reconciling`。迟到 result 必须 stale，不能复活旧 lease。
4. 修复所有 Cancelled gate（`needFallbackContinue`、`terminalObservation`、
   `isSubagentSettledFromObservation`）——第一分支统一 Cancelled/TaskComplete → 终止。
5. 区分一次 Esc 与实际 Abort（OpenCode TUI 双击 Esc 语义）。
6. 引入 per-session event mailbox（取代散落 async handler 直接读写 runtime map）。
7. `session.idle` 不再直接触发 nudge；已 Settled 的 fallback lease 不得再产生 fallback
   nudge。
8. fallback action 发送前增加 lease 校验（cancelGeneration + humanTurnID + lifecycle
   + canonical owner + compaction + TaskComplete）。
9. 接入 compaction Hooks（`experimental.session.compacting`、
   `experimental.compaction.autocontinue`、`session.compacted`），并规定 compaction
   仅递增 contextGeneration，不递增 sessionGeneration。
10. compaction episode 活跃时阻止 nudge；对话开始未取得足够上下文时不得生成 emergency
    todo prompt。
11. Review nudge 携带 `original_task`（不是 `task`），Nudge 显式传
    model/variant/agent。
12. investigator agent 必须注入 CAPS；单工具成功回合只发一个“并行调用工具”提示。
13. Hook 参数改为原地删除，不替换 `output.args`；after hook 移除安全校验职责。
14. `warn`、`warn_tdd`、`warn_reuse` 及 todo 长度遵循软合规：schema 强调但不硬拒绝，
    在工具返回时以一次 stern compliance event 批评模型；仅 malformed business args、
    security/permission denial、parse failure 和控制字段泄漏是 hard gate。
15. 删除 stdout DEBUG。

## 三、P1：消除主要竞态

1. HumanTurnProjection。
2. CancellationProjection。
4. CompactionProjection。
5. ContextBudgetProjection（RebaseRequired，revision/counter 语义）。
6. Host model observation generation。
7. Event ID 幂等与 stale-result immunity。
8. plugin load order 检查。
9. MCP schema 能力审计。
10. prompt provenance 与官方 messageID 绑定。

## 四、P2：消除兼容性启发式

1. 删除零宽字符身份判断。
2. 删除物理时间戳权威判断。
3. 删除 stale injected model 优先级。
4. 删除 compaction 文案判断。
5. 删除最后 assistant 推导 human model。
6. 删除全历史 todo 作为 R。
7. 删除固定大 context limit。
8. 删除 event handler 中直接 prompt。
9. 删除依赖 after hook 的安全策略。

## 五、阶段化实施建议

### 阶段 0：先固定复现和观测

建立端到端测试事件脚本，重放 Esc 与 fallback 竞态、compaction 后 idle、model A/B/C 切换、context threshold、review nudge、warn 字段执行。同时记录结构化事件，不再增加临时 DEBUG。

### 阶段 1：引入 provenance、turn ID、generation，但不引入第二事实源

新增 humanTurnId、continuationId、cancelGeneration、prompt origin、routing context。
所有变更通过一条 domain event 写入 journal，并由 shadow projections 在内存中比较
旧行为；禁止第二次事实写入、禁止 parallel state stores、禁止 `currentProjections`
成为 SSOT。

### 阶段 2：优先修复 Esc 和 nudge owner

最高风险问题。完成 sticky cancellation、invalidation、terminal origin、continuation owner、fallback settle、compaction gate。

此时应先保证"不会擅自继续"，哪怕偶尔少 nudge，也比 Esc 后继续安全。

执行计划必须留在 `step` 的单一 snapshot 事务内；`stateFlow |> map plan` 只用于
诊断/UI 预览。lease claim、dispatch、settle 均只能由已提交事件驱动。

### 阶段 3：修复模型和 review task

切换 nudge 消费者：model 从 HumanTurnProjection 取；task 从 ReviewProjection 取；禁止读取 stale injected model；禁止从旧 assistant 推导 review context。这部分变更相对独立，容易验证。

### 阶段 4：统一控制字段执行网关

先完成能力分类、最终 schema decorator 和启动完整性检查，再切换执行入口。迁移期间可以保留 host-specific hook 做审计，但不能继续作为权威判断。

### 阶段 5：重建 context budget

数学、测量、缓存、compaction 耦合最深，应在事件 owner 和 provenance 稳定后改。缓存
只按 generation、revision/counter、已应用事件数和 canonical byte/prefix equality
断言判断；不得以内容 hash 作为 key 或正确性。否则 context-budget nudge 也会成为
新的抢跑来源。

### 阶段 6：清理旧启发式和 DEBUG

删除或降级 zws 文本识别、timestamp 权威判断、`last non-synthetic assistant` 回退、session 级 stale injected model、各 host 重复 warn 判断、直接 `printfn DEBUG`、post-execute 安全校验。
# 最终验收标准

全部修复完成后，应满足以下可观察结果。

## 一、Esc

* Esc 后旧轮次发送 prompt 数量严格为 0。
* 迟到 zws/message.updated/busy/idle 不会解除取消。
* 单次 Esc 不错误标记为 Abort。
* 双次 Esc 后旧 continuation 调用数为零。
* `session.error` 缺失时仍能识别 Abort。
* 重复 idle 不产生重复状态转移。
* 旧 generation 的 message.updated 不恢复 session。
* 已派生但未发送的 action 被取消。
* 已经排队的 prompt 可被识别和失效。
* `Cancelled + EventHandlingActive=true` 必须返回不继续。
* `Cancelled + AwaitingBusy=true` 必须返回不继续。
* `Cancelled + BusyCount>0` 必须返回不继续。
* `Cancelled + Phase=Retrying` 必须返回不继续。
* Cancelled session 必须被视为 settled。
* 旧 continuation 的迟到 busy/idle 不能重新增加 BusyCount。
* Abort 空输出不会产生 EmptyOutputError。
* 缺少 assistant abort metadata 时 translator 仍可识别取消。
* 已生成但过期的 SendContinue 不会调用 Host。
* Cancelled 后所有旧 continuation lease 失效。
* 同一 Abort 被多个 Host event 重复报告时只取消一次。

## 二、warn 字段

* 每个适用工具 schema 都展示 `warn`、`warn_tdd` 或 `warn_reuse`，并以 description、examples 及 `x-wanxiangshu-soft-required` 元数据强调合规；不得使用 Host 强制执行的 `required`、`minLength` 或单值 `enum`。
* 缺失、空白或非规范控制字段时真实工具仍执行；最终结果保留原始输出并追加一次 `WANXIANGSHU_COMPLIANCE_REPRIMAND`，不改变 success、不触发 fallback，且告知 LLM 不要重复已成功调用。
* 下游参数中控制字段出现次数为 0。
* registry tool schema 中软字段 metadata 正确；没有把软字段放入 Host 强制 required/minLength。
* MCP 敏感能力被单独审计。
* 原地删除后真实 execute 收不到控制字段。
* 替换 `output.args` 的回归测试明确失败并被检测。
* 其他插件替换 args 时产生兼容性警告。
* malformed business args、权限/安全拒绝、解析失败或净化后控制字段泄漏时真实 execute 次数为零；软合规缺失不属于这些硬门禁。
* after 缺失不影响安全。
* 自有 wrapper 存在 clean assertion。
* 软合规缺失不产生 session.error 或 fallback；硬错误仍 fail closed。

## 三、context budget

* 精确阈值处稳定触发。
* token API 不可用时仍有保守触发。
* 一次 episode 只发送一次初始 budget nudge。
* 成功 todowrite 后 phase 正确重置。
* 最后一条 assistant 无 usage 时能找到最新有效 observation。
* 不重复累加 cache.read。
* 大型 tool result 在本轮估算中被计入。
* transform 新增内容被计入。
* limit=0 时进入保守默认。
* work backlog commit 后下一轮必然重建 phase。
* 同一 budget episode 不产生无界重复 nudge。
* compaction 后 context generation 正确变化。
* 第一轮无论 CAPS 多大都不触发 emergency todowrite。
* 无真实 token usage 的 bootstrap 阶段不触发。
* 达到真实增长阈值后能正确触发。
* investigator 永远不会收到 todowrite emergency。

## 四、日志

* 默认运行 stdout 中 `DEBUG:` 数量为 0。
* 敏感 prompt、工具参数不进入日志。
* stderr 不包含普通能力探测。
* debug=false 时没有 debug 级日志。
* debug=true 时结构化字段完整。

## 五、compaction/fallback

* compaction 完成后的普通 nudge 数量为 0。
* fallback episode 中普通 todo/review nudge 数量为 0。
* 每个终止事件只有一个 owner。
* `experimental.session.compacting` 打开 block。
* autocontinue planned 时普通 nudge 为零。
* summary assistant 不被视为真人完成。
* synthetic continue 不经过 chat.message 也能识别。
* `session.compacted` 不立即解除 block。
* auto-continue settle 后才解除。
* compaction Abort 时 cancellation 优先。
* 用户输入相同文案不被误判。

## 六、模型

* todo/review nudge 使用当前真人轮次模型。
* 旧 fallback injected model 不影响新真人轮次。
* variant 和 reasoning 设置不丢失。
* 真人 user model 成为 HumanTurnRoute。
* `chat.params` 按 owner 分类。
* compaction/title 不污染 human route。
* nudge 请求显式携带 model/variant/agent。
* fallback attempt route 不跨 turn。
* 字符串、User object、Assistant flat fields 均可解析。
* 旧 generation observation 被丢弃。
* Host 实时查询结果与当前 turn 不匹配时不得覆盖。

## 七、review task

* 每条 review nudge 都包含完整 `original_task`。
* active loop 缺 task 时不发送任何失忆 prompt。
* compaction、重启、needs revision 后原任务保持不变。
* review nudge 只包含 `original_task`，不重新生成激活字段 `task`。
* task 只存于权威 ReviewProjection。
* snapshot 正确派生 active task。
* prompt 使用 `original_task`。
* compaction 后任务仍可恢复。
* review nudge 不会被误识别为新的 loop activation。

## 八、跨 Host 一致性

OpenCode、Mimocode、Mux、OMP 对同一组输入断言：
* warn 缺失不拒绝；
* todo 报告短不拒绝；
* 工具执行后结果保留原文，并在所有结果规范化完成后追加一次一致的批评；
* 控制字段在历史中恢复；
* investigator 有 CAPS；
* investigator 有并行提示；
* 开局不出现 emergency todowrite。

## 九、最重要的取舍

三个原则保留：
* 宁可少自动继续一次，也不能违反用户 Esc。
* 宁可少发一次 nudge，也不能在 compaction/fallback 后重复抢控制权。
* 宁可因状态缺失阻止 review continuation，也不能让 LLM 在没有原始任务的情况下猜。

真正要删除的不是某个零宽字符，而是整个系统对"根据文本、时间和最近消息猜测状态"的依赖。只要 provenance、generation、owner、routing context 和 durable projection 建立起来，这 7 个问题会一起消失。
# Fable/JS 互操作与深度实施避坑指南

本附录针对万象术在 Fable 编译 JS 环境、OpenCode v1.17.13 架构及 Mux 环境下，针对七项核心漏洞的落地实施提供深度的避坑、防御及细节校验指导。

## 一、Fable 与 JS 互操作中的隐性地雷

### 1.1 int64 比较与 JS Number 精度断层

在 F# 核心库中，`int64` 是通过自定义对象（包含高 32 位和低 32 位数值字段）在 JS 中模拟实现的。

**避坑警告**：当从 Host 获取时间戳（通常为 JS 的双精度浮点数 `number`，对应 F# 的 `float`）并与 F# 的 `int64` 进行直接比较（例如 `msgTimeMs >= injectedAt`）时，若其中一方被转换为 `obj` 传入，编译后的 JS 将使用 `===` 或原生的 `>` 运算符。这会导致 JS 运行时将 F# 的 `int64` 结构体与原生 JS 数值进行直接对比，进而产生恒为 `false` 的静默错误。

**定案方案**：在整个 Fallback 与 Nudge 系统中，时间戳统一使用 `float`（即 JS 原生的 `Number`，精度足以安全表示毫秒级时间戳，直至公元 285428 年）或者进行显式的双向类型转换，禁止混合使用 `int64` 和 `float` 进行逻辑比较。

### 1.2 Object.defineProperty 与只读 Host 对象的静默冲突

部分 Host（如 Mux/OpenCode）的 `args` 或 `config` 属性可能是通过 Object 冻结、getter 配置或代理拦截实现的只读对象。

**避坑警告**：直接对这些对象进行修改（如 `args?warn_tdd <- ...`）在 JS 严格模式下会抛出 `TypeError`，在非严格模式下则会静默失败（属性未成功写入，但程序继续运行）。

**定案方案**：在执行前终极净化阶段，不要假设可以直接对 Host 传入的参数对象进行写入。应当首先通过浅拷贝（`assignInto` 或 `Object.assign`）甚至深拷贝克隆一个自由的对象副本。对此副本完成净化、校验后，再行使后续的执行和调用，绝不在 Host 的原始参数对象上直接测试边界。

### 1.3 Object.ReferenceEquals 对 JS 临时重构对象的失效

在 F# 中常用 `System.Object.ReferenceEquals` 或其别名来判断消息的部分或整体在 transform 过程中是否发生了改变。

**避坑警告**：OpenCode 和 Mux 在传递消息列表或零件列表时，可能会在每次生命周期 Hook 触发前对其进行内部反序列化。这会导致即使消息内容完全没有改变，前后两次收到的 JS 对象引用地址也完全不同。此时，依赖 F# 引用一致性判断的缓存和防重复机制会彻底失效。

**定案方案**：缓存正确性和键只使用明确的 revision/generation/counter。引用变化时可用规范化 UTF-8 字节或前缀字节相等性作可观察断言（例如验证 transform 输出是否确实未改变），但不得计算、持久化或依赖内容 hash/fingerprint 作为缓存命中或防重复依据。决不能将引用等价性作为防重复和防刷新的核心依据。

## 二、单线程事件邮箱与异步租约（Lease）的防竞态设计

### 2.1 异步 continuation lease 的状态流转与自动失效

为了防止"状态机做出了 SendContinue 决策，但在调用 Host API 之前用户按了 Esc"这类物理竞态，所有的 Continuation 动作必须被约束在一个受代次控制的租约中。

**避坑警告**：若只在状态机中维护一个 `SessionFallbackState` 的 `Lifecycle` 字段，由于在途的 `session.prompt` 调用是异步 Promise，在 Promise 挂起期间，用户按了 Esc。此时，状态机的 `Lifecycle` 的确变为了 `Cancelled`，但已经进入微任务队列的 `prompt` 仍会强行将消息发送出去。

**定案方案**：每一个 `SendContinue` Action 必须关联一个唯一的 `ContinuationLease` 对象。
* 租约包含 `humanTurnID` 和 `cancelGeneration`；
* 在 Action 真正执行 Host 调用的"最后一毫米"，必须从 per-session mailbox 中读取该 session 当前最新的 `cancelGeneration`；
* 只要发现当前代次已大于租约创建时的代次，此租约必须被强制宣布为 `Invalidated`，直接拦截调用。

### 2.2 门禁信号的重置收敛

在 Abort 事件被确认为真且更新了 `Cancelled` 墓碑后，必须立即重置该 Session 的所有衍生标志。

**避坑警告**：如果仅修改了 `Lifecycle = Cancelled`，但没有清除 `AwaitingBusy` 或 `SubsessionPending` 等异步门禁，那么下游的 `needFallbackContinue` 计算由于看到这些门禁依旧为 `true`，仍然会继续产出"需要 fallback 续跑"的诊断结果，使得状态机制动失效。

**定案方案**：必须设计一个原子重置方法（`CancelEpisode`），当且仅当发生 Abort 时，该方法会强行将当前 Session 的 `AwaitingBusy`、`SubsessionPending`、`EventHandlingActive` 置为 `false`，并把 `BusyCount` 归零。这些门禁信号在生命周期终止时没有资格保留。

## 三、控制字段净化与 Schema 静态增强的协调防御

### 3.1 Zod/Effect 模式下软字段 metadata 的同步更新

在改写 OpenCode 的 tool parameters 时，必须在 `properties` 中展示软字段，并同步写入 description、examples 和 `x-wanxiangshu-soft-required` / `x-wanxiangshu-soft-min-length` 元数据；不得把它们加入 Host 会执行的 `required` 数组。

**避坑警告**：如果把 `warn_tdd`、`warn` 或 `warn_reuse` 写入 Host 强制执行的 `required`，或把报告质量写成 Host 强制执行的 `minLength`，漏填/短报告会在 hook 之前被拒绝，工具无法执行，违反软合规契约。

**定案方案**：在 schema 最终导出前，万象术必须调用统一 decorator 写入软字段的 description/examples 和 `x-wanxiangshu-*` metadata。若 host 支持不参与校验的说明性 enum，可在 metadata/examples 中记录唯一推荐值；不得在可执行 schema 中放置会触发硬拒绝的 `required`、`minLength` 或 enum。

### 3.2 应对 Host 参数重构的"净化后断言"

由于 OpenCode/Mux 在 Hook 执行前后可能进行深拷贝或重新序列化，导致 properties 中的多余字段再次出现在入参中。

**避坑警告**：在 `tool.execute.before` 中删除了 `output.args` 里的字段，但在真实执行 execute 之前，Host 又根据之前的 tool call 记录重新反序列化出了一份干净的 `args`，此时控制字段死而复生。

**定案方案**：引入执行前终极净化。在最贴近底层工具 execute 的 wrapper 中，实现如下逻辑：

```text
IF checkControlFieldsExist(args) THEN
    LogErrorAndFailClosed()
```

通过这一层净化后断言，即使 Host 机制发生漂移，敏感工具也会在控制字段泄漏时直接拒绝执行，从而保证安全；控制字段缺失或报告过短不触发该拒绝。

## 四、上下文预算公式失效与测量降级

### 4.1 UTF-8 字节数与 Token 密度的保守估算

当 Host 没有提供实时的 token counts，且历史 `LastUsage` 的测量数据也为空时，系统会退回 `None` 并关闭保护。

**避坑警告**：不能使用单一的 `bytes / 4` 判定。因为：
* 中文字符在 UTF-8 中占用 3 个字节，但在很多 tokenizer 中一个汉字就是一个或半个 token，字节/token 比例接近 3:1；
* 代码、长路径、YAML 标记的 token 密度可能会根据标点符号的不同发生巨大抖动；
* 使用过大的估算分母会导致严重低估上下文，进而延迟 todowrite 催促，发生物理溢出。

**定案方案**：引入多语境保守估计器：
* 检测内容是否包含中文字符。若是，采用偏高估计（如每 2 个字符 1 个 token）；
* 若全为代码，按已知的高密度估计；
* 计算出的初始估算值必须乘以安全系数（如 1.25）；
* 显式标记此测量为 `MeasurementDegraded`，允许提前触发 todowrite。

### 4.2 极限阈值 0 的拦截与安全默认值

当 `MaxInputTokens` 经由各类 client 反馈解析出 0、负数或小于 5000（保留空间）的值时，系统不得将其作为合法输入上限。

**避坑警告**：直接接受 0 限制会导致 `effectiveMaxInputTokens` 无法计算，或者公式 `F` 中分子分母出现除以零及矛盾边界。

**定案方案**：设定最低安全水位：任何解析出的 limit 若低于 8192，一律回落到该 Host 的保守默认水位（如 16384 或 32768）；确保即使完全无法从 Host 获取上限，系统也能以一个适中的安全视界运行，决不静默关闭预算。

## 五、压缩周期的多层状态阻断

### 5.1 废弃物理时间窗口，改用状态代次阻断

**避坑警告**：如果系统负载高、网络出现延迟，或者模型的 reasoning 过程超过了该时间窗，当 time window 过期而 compaction 产生的 auto-continue 仍然在途中时，nudge 就会乘虚而入。在测试环境和高并发环境下，这会造成极不稳定的偶发性失败。

**定案方案**：完全废弃任何物理时间窗口。改用基于 `CompactionProjection` 状态周期的强阻断：
* 当捕获到 `experimental.session.compacting` 时，Compaction 状态立即变更为 `Compacting`，并生成新的 `compactionEpisodeID`；
* Nudge 阻断状态（`NudgeBlockReason`）变更为 `CompactionActive`；
* 直至该 compaction summary 产生、auto-continue synthetic 消息发送、对应 assistant 消息自然结束（`terminal`），且 mailbox 确认该轮无 fallback 要求、彻底闲置后，方可将 compaction 状态标记为 `Settled`；
* 此时将 `NudgeBlockReason` 回归为 `Allowed`。状态未 Settled 前，阻断持续存在，无论经历了几万毫秒。

### 5.2 Compaction 过程中原始任务的重注入防护

**避坑警告**：压缩后如果直接使用 `ReviewLoopFold` 去找，由于旧的消息已经被截断，fold 可能会返回 `None`。此后触发的 nudge 就会丢失原任务，LLM 也会失去目标。

**定案方案**：万象术的 `ReviewProjection` 是一个跨 Compaction 持久生存的状态。在 `experimental.session.compacting` 阶段：通过 `output.context` 强制重新将 `ReviewProjection` 中存储的 `original_task` 拼装为新的 front-matter 写入即将生成的 summary 中，实现物理重注入；从根本上解决压缩过程中的原任务失忆问题。

## 六、模型路由与人类轮次单一事实源

### 6.1 NewUserMessage 对 model map 缓存的原子清洗

**避坑警告**：如果仅在 fallback 状态机内部清理 `state.CurrentIndex`，由于 `FallbackRuntimeState` 的 `models` Map（曾用作运行期缓存）中依然残留有上一次 fallback 时记录的 `"provider/model"` 值，随后的 Nudge 仍然可能会读取到这个残留值，进而指派错误的模型。

**定案方案**：
* 建立统一的 `NewUserMessage` 入口；
* 原子删除 `FallbackRuntimeState` 中该 session 对应的 `injectedModels`、`injectedAts` 以及 `models` 缓存；
* 清除 `ContinuationLease`；
* 将 `HumanTurnRoutingProjection` 更新为新 user message 实际携带的模型设置，以此作为后续所有 todo/review nudge 的唯一权威路由事实源。

## 七、评审提示词中 original_task 的严格隔离

**避坑警告**：在 nudge prompt 渲染的 front-matter 中，键名决不能写成 `task`。因为万象术在重启重放（Replay）时，其折叠逻辑（`inferReviewTaskFromTexts`）只要看到 `task` 字段，就会误判定为"用户重新发起了一次 With-Review 激活命令"。这会导致 review 状态和 version 发生严重错乱。

**定案方案**：
* 在 nudge prompt 模板中，原始任务一律统一编码为 `original_task`；
* 同时附加 `prompt_origin: review_nudge` 明确其非激活语义，与首发 With-Review 的激活命令在 schema 层面彻底实现物理隔离。
# 六项行为修复方案

本附录与七个核心问题正交：七个问题关注 hard gate（安全/正确性），六项行为修复关注 soft-required（用户体验/协议兼容）。

## 一、统一设计原则

### 规则 1：Projection、CAPS、Hint、Budget 必须拆成独立策略

当前 `ProjectionPolicy.ExcludeProjection` 会同时造成：不投影 backlog、不注入 CAPS、不注入并行工具 user hint、部分 Host 不加载 subagent 临时文件、Context Budget 行为也和投影流程耦合。这正是 investigator 丢 CAPS、单工具调用丢 user hint 的共同根因。

应将 `MessageTransformPlan` 中单一的 `ProjectionPolicy` 拆成至少四个独立决策：

| 策略 | 控制内容 |
| :--- | :--- |
| BacklogProjectionPolicy | 是否折叠、注入 todowrite backlog |
| CapsInjectionPolicy | 是否注入 CAPS 和 subagent 临时文件 |
| ParallelHintPolicy | 是否注入"并行调用工具"提示 |
| ContextBudgetPolicy | 是否计算预算、是否允许提醒 todowrite |

agent 策略矩阵：

| Agent | Backlog | CAPS | Parallel Hint | Todo Budget |
| :--- | ---: | ---: | ---: | ---: |
| 主工作/manager/build | 是 | 是 | 是 | 是 |
| investigator | 否 | 是 | 是 | 否 |
| reviewer | 视现有设计 | 是 | 视工具能力 | 否或专用策略 |
| browser | 否 | 否 | 否 | 否 |
| executor | 否 | 否 | 否 | 否 |
| title/compaction | 否 | 否 | 否 | 否 |

绝不能简单把 `"investigator"` 从 `defaultExcludedAgents` 删除。该函数还被工具权限判断复用，直接删除会扩大 investigator 的工具权限。

### 规则 2：强提示不等于硬拒绝

`warn_tdd`、`warn`、`warn_reuse`、todowrite 的五个报告字段及 1024 字要求都应采用"软要求"。schema MUST 通过 description、examples 与 `x-wanxiangshu-soft-required` / `x-wanxiangshu-soft-min-length` 元数据强调填写；不得使用 Host 强制执行的 `required`、`minLength` 或单值 `enum`。LLM 漏填/短填时工具仍执行；工具结果在最终规范化后追加一次严厉的协议违例批评；不把结果标记为工具失败；不抹掉工具原始输出；不要求 LLM 重复执行刚刚已经成功的工具。

### 规则 3：真正的 JSON Schema required、minLength 与"漏填也执行"冲突

如果 Host 严格执行 JSON Schema，required 缺失会在工具执行前被拒绝，minLength: 1024 不满足也会在工具执行前被拒绝，万象术的 tool-before/tool-after 根本收不到调用。

推荐 schema 表达方式：字段继续存在；description 明确写 MUST 和"至少 1024 字"；增加 Wanxiangshu 自有元数据 `x-wanxiangshu-soft-required`、`x-wanxiangshu-soft-min-length` 及 examples。唯一推荐值如需展示，只能放在不参与 Host 校验的 metadata/examples 中；不得放入真正的 `required`、可执行 `minLength` 或会硬拒绝的 `enum`。

真正的 fail-closed 只保留给 malformed business args、权限/安全拒绝、parse failure，以及终极 sanitizer 发现控制字段仍泄漏到业务参数的情形。软字段缺失、空白、非规范值和短报告绝不能进入这些硬门禁。

### 规则 4：批评必须发生在所有结果规范化之后

当前多个 Host 会在 tool-after 中重写工具结果（OpenCode `ProgressObserver` 替换 todowrite 输出；OMP `TodoHooks` 替换 todowrite 输出；Mux wrapper 重新构造 output）。如果先追加批评、后做标准输出替换，批评会再次丢失。

统一顺序必须是：
1. before hook 提取并删除控制字段，保存原始值及 missing/blank/non-canonical 状态；
2. 工具真实执行；
3. 网络错误、语法诊断、livelock 等现有处理；
4. todowrite 等工具进行标准输出规范化；
5. 在 finally 性质的路径将原始控制字段恢复到所有历史可见 args；
6. 最后追加协议违例批评；
7. 删除临时 compliance envelope。

## 二、investigator 也注入 CAPS

### 现有根因

`Kernel/MessageTransformPolicy.fs` 当前把 investigator 放在 `defaultExcludedAgents`。三个 Host 据此设置 `ProjectionPolicy.ExcludeProjection`。CAPS 被错误绑定到该 policy：`Shell/MessageTransformHostHooks.loadCapsForScope` 遇到 Exclude 直接返回空列表；OMP 的 `loadCaps` 也直接根据 ProjectionPolicy 返回空列表；`injectSubagentFilesIfAny` 同样受 ProjectionPolicy 限制。

### 正确修改

修改重点：
* `Kernel/MessageTransformPolicy.fs`：保留 projection 排除规则用于 backlog 和某些工具权限，新增 `CapsInjectionPolicy.Include`、`ParallelHintPolicy.Include`、`ContextBudgetPolicy.DisableTodoEmergency` for investigator。
* `Shell/MessageTransformCore.fs` + 三个 Host 的 `MessageTransform.fs`：计划中显式携带四个 policy。
* `Shell/MessageTransformHostHooks.fs`、`Omp/MessageTransform.fs`、`Opencode/MessageTransform.fs`、`Mux/MessageTransform.fs`：`loadCapsForScope` 只看 `CapsInjectionPolicy`。

不要顺手扩大 investigator 权限：investigator 仍不可调用 todowrite、写文件、调用 coder。ToolPermission 中依赖 projection exclusion 的逻辑不能被误改。

### 验收

* investigator 输入历史开头存在 CAPS synth 消息，内容与主 agent 看到的一致。
* investigator 是 child session 时使用 parent session 的 CAPS scope。
* 连续运行 message transform 三次 CAPS 不重复叠加。
* CAPS 内容改变时 transform cache 失效。
* investigator 工具列表没有新增写权限。

## 三、单工具调用后稳定注入并行工具 user hint

### 现有根因

`Shell/MessageTransformPipeline.tryInjectParallelToolPrompt` 三个脆弱点：
1. 整个功能被 ProjectionPolicy 关闭；
2. 统计所有 ToolPart 而非实际工具调用（synthetic internal part 导致 allToolParts.Length > 1）；
3. 结果匹配没有关联 call ID（旧结果可能被误认为当前工具结果）。

### 正确修改

1. 用独立 `ParallelHintPolicy`，investigator 启用，title/compaction/browser 不启用。
2. 定义"真实工具调用"：非 synthetic call ID、非 CAPS/prefetch/internal 伪造调用、属于当前 native assistant turn 的调用。触发条件只看 `triggerableToolCallCount = 1`，不再要求所有 ToolPart 数量等于 1。
3. 按 call ID 找到对应结果：最新 native assistant message → 提取真实 call ID → 真实调用数恰好为 1 → 后续消息找到该 call ID 对应 ToolResult → 该 ToolResult 后没有更新的 native assistant message → 当前输出还没有对应 hint → 追加 synthetic user hint。
4. hint ID 稳定：基于 assistant message ID 或唯一 tool call ID，如 `parallel-tool-hint:<callID>`。同一 transform 多次运行不会重复。
5. hint 内容保持"提醒"，不成为硬拒绝。

### 验收矩阵

| Assistant 工具组成 | 应否提示 |
| :--- | ---: |
| 1 个真实 read | 是 |
| 1 个真实 read + 1 个 synthetic internal part | 是 |
| 2 个真实 read | 否 |
| 1 个真实工具但尚无结果 | 否 |
| 1 个真实工具，结果已返回 | 是 |
| 结果后已经有新 assistant message | 否 |
| title/compaction agent | 否 |
| investigator 单独调用一次 read | 是 |
| 同一消息 transform 重复执行 | 只出现一次 |

## 四、warn、warn_tdd、warn_reuse 在 tool-after 恢复

### 现有根因

`Shell/ToolHookRuntime.executeBeforeGateway` 删除 warn 字段。before hook 通过 `saveCompliance` 将 envelope 持久化到 transient store。但 tool-after 不知道被删除的 warn 值。

### 正确修改

1. 引入统一的 Tool Compliance Envelope，保存 tool name、tool call ID、warn_tdd/warn/warn_reuse 原值及状态（present/missing/blank/non-canonical）、todo 报告质量问题、是否已输出批评。
2. 建立 transient `ToolComplianceStore`：键 session ID + tool call ID，值 compliance envelope。before hook 写入，after hook 读取并删除。
3. 存储必须有回收机制：session abort、turn end、session shutdown、tool call 被拒绝、child session 清理都要删除残留 envelope。
4. tool-after 恢复到所有历史可见 args：OpenCode（input args、output args、decoded args）；Mux（decoded args、input args、output args）；OMP（tool_result event input/args）。恢复的是 LLM 原始提供值，缺失字段不要伪造。
5. 恢复必须发生在 finally 性质的路径：无论结果是 success、error、network error、syntax diagnostic、livelock，都要恢复；若硬门禁在 before 阶段拒绝且没有可见结果，也必须清理 envelope，不能留下半截状态。

## 五、warn 字段缺失时不拒绝，只在结果中严厉批评

### 现有根因

`executeBeforeGateway` 当前对 FileMutation 缺 warn_tdd、ProcessExecution 缺 warn、SubagentDelegation 缺 warn_reuse 返回 `Result.Error`。OpenCode 设置 hook error + 抛 Tool validation exception；OMP 返回 block=true；Mux 设置 hook error。这与期望行为完全相反。

### 正确修改

1. before gateway 不再因 warn 缺失返回 Error。warn 三字段属于 compliance 问题，不属于执行合法性问题。
2. schema 改为 soft-required（保留字段、description 用 MUST 文案、使用 examples 与 `x-wanxiangshu-soft-required` 标记；不进入真正 required，不使用可执行 minLength；enum 仅可作非校验 metadata）。
3. tool result 末尾追加统一批评块：不替换原输出、不把 success 改成 false、不设置 hook error、明确指出缺少哪个字段和适用能力、明确告诉 LLM 工具已执行不要重复调用、要求下一次调用改正。
4. 只追加一次：使用固定 marker `WANXIANGSHU_COMPLIANCE_REPRIMAND`，已有 marker 不再追加。
5. 三个 Host 的追加位置：OpenCode 放在 `lifecycleObserver.handleToolExecuteAfter` 之后；OMP 放在 TodoHooks 标准输出重写之后；Mux wrapper 和 hook after 统一由最后的结果装饰器追加。

## 六、todowrite 报告不足 1024 字时不拒绝，只批评

### 现有根因

多层旧实现曾造成硬拒绝（这些门禁必须删除，不能作为目标行为）：
* 旧 Schema 将五个字段放入 `required` 并设置 `minLength=1024`；
* 旧 Runtime decoder 将少于 1024 视为 InvalidIntent；
* 旧 Host 直接执行层检查长度并返回 error，或将 decoder 失败映射为 success=false/failwith。

### 正确修改

1. 分离"结构合法性"和"报告质量"。硬错误保留（todos 不是数组、todo item 缺 content、status 无法解析、priority 非法、session ID 缺失、权限问题）。软错误为五个报告字段缺失/空/不足 1024。decoder 返回两部分：可执行的 `TodoWriteArgs` 和 `ReportComplianceViolation list`。
2. schema 改为软要求：五个字段继续展示，description 明确要求不少于 1024 字，不使用真正的 required/minLength/enum 门禁，增加 `x-wanxiangshu-soft-min-length` 元数据及示例。
3. 工具必须继续完成核心工作：todos 状态更新、native todo 工具调用、EventLog 记录 work backlog commit、已提供的报告内容原样保存、缺失内容保存为空。
4. 批评必须列出每个字段的实际长度。
5. 各 Host 修改：OpenCode（ProgressObserver 不再因短报告 failwith；先写 EventLog；标准化输出；最后追加批评）；Mimocode（去掉五个长度拒绝分支）；Mux（soft decoder 后调 native todos、捕获报告、append EventLog、返回 success、结果末尾追加批评）；OMP（去掉 execute 中五个长度错误；TodoHooks 覆盖后追加）。

### 验收

1. 五个字段均超过 1024：工具成功，无批评。
2. 一个字段 1023：工具成功，todo 状态更新，EventLog 有 commit，批评只列该字段。
3. 五个字段全部缺失：工具成功，todos 正常更新，批评列五项，不抛异常。
4. todo item status 非法：仍然硬拒绝。
5. after hook 执行两次：批评只出现一次。

## 七、修复 Context Budget 开局误触发 todowrite

### 现有根因

`rebuildPhaseState` 在第一次观测且 backlog 为空时将 `phaseBaseTokens = 0`。随后同一次 message transform 立即调用 `classifyPressure`。对于默认 `foldAfterFirst=false`，N=3，初始阈值约为有效窗口的 25%。CAPS、系统提示、用户首条消息本身可能已超过窗口 25%，立刻触发紧急 todowrite。

### 正确修改

1. 第一次观测只做校准，绝不触发提醒：`rebuildPhaseState` 返回额外信息（是否刚初始化 baseline），若刚初始化则保存 state/usage，不调用 emergency injection。
2. backlog 为空时也使用真实初始 baseline：删除 `State=None && backlog.IsEmpty → phaseBaseTokens=0`，统一为 `phaseBaseTokens = stableTokens`。
3. 引入 UsageConfidence（Observed/CalibratedEstimate/BootstrapEstimate），第一次只有粗估时不触发。
4. 必须有正的阶段增长：`currentTokens > phaseBaseTokens` 或新增量超过最小噪声区间。
5. investigator 不得进入 Todo Emergency。
6. NudgeTrack 表示 episode 而不是 transform 次数：记录 episode ID、signal 时 todo ordinal、signal 时 tokens、stable synthetic nudge ID。压力仍成立且 todo ordinal 未推进时输出中继续包含同一条提醒。
7. 成功 todowrite 后正确重置 baseline。

### 验收

1. CAPS 很大占窗口 30%：第一轮不提醒。
2. 没有真实 usage 只能 bytes/2：第一轮不提醒。
3. 首次 transform 连续调用两次：仍不提醒。
4. baseline 之后新增 token 未达阈值：不提醒。
5. 新增 token 跨过 F 阈值：注入一次紧急提醒。
6. 同一消息集合重复 transform：提醒仍可见，不增加 episode 数。
7. 成功 todowrite：baseline 重建，提醒消失。
8. investigator 上下文超过阈值：不出现提醒。
9. phase base 已超过窗口 80%：走 compaction。

## 八、建议新增的共享模块

### 1. AgentProjectionPolicy

职责：给定 host、agent、是否 child session，输出四个独立 policy，所有 Host 统一调用。禁止每个 Host 自己写一套 investigator 特判。

### 2. ToolCompliance

职责：根据 tool capability 判断哪些软字段适用；提取控制字段；产生 compliance envelope；检查 warn 字段；检查 todo 报告长度；生成统一批评文本；判断结果中是否已经有批评 marker。Kernel 保持纯函数：输入 tool/args，输出净化 args/envelope/violations。Shell/Host adapter 负责保存 envelope、执行工具、恢复字段、修改结果文本、清理 transient store。

## 九、推荐实施顺序

1. **第一阶段**：先解除错误拒绝（warn 软化 + todowrite 软化 + schema 修改 + 确认工具仍正常执行）。
2. **第二阶段**：建立 after compliance 闭环（envelope/store + before 保存 + after 恢复 + 批评 + 清理）。
3. **第三阶段**：拆分消息策略（ProjectionPolicy 拆四个 + investigator CAPS/Parallel Hint/Todo Emergency off + 保持工具权限）。
4. **第四阶段**：重做 Context Budget bootstrap（初次观测只初始化 + 初始 phase base 使用当前 tokens + usage confidence + 稳定 emergency episode + 成功 todo 后重置）。
5. **第五阶段**：跨 Host 契约测试（同一组输入依次运行 OpenCode/Mimocode/Mux/OMP，断言行为一致）。

## 十、最终验收标准

1. investigator 收到 CAPS，但没有新增写权限。
2. 一个真实工具调用完成后，下一轮稳定出现并行工具提示。
3. 一个真实调用加任意 synthetic ToolPart，仍视为单工具调用。
4. warn 三字段在 tool-before 对真实工具隐藏，在 tool-after 恢复到历史 args。
5. warn 缺失不阻止工具执行。
6. warn 缺失时结果保留原输出并追加一次严厉批评。
7. todowrite 五个报告字段不足 1024 时仍更新 todos。
8. 短报告仍写入 EventLog，并追加实际长度清单。
9. invalid todo status 等真正业务错误仍然拒绝。
10. 第一轮无论 CAPS 多大都不触发 emergency todowrite。
11. 无真实 token usage 的 bootstrap 阶段不触发。
12. 达到真实增长阈值后能正确触发。
13. investigator 永远不会收到 todowrite emergency。
14. 重复 transform 不重复 CAPS、hint 或批评。
15. OpenCode、Mimocode、Mux、OMP 行为完全一致。
# 子会话 Actor 架构（代码已实现）

## 一、设计目标

主会话（host session）里的 `coder`/`investigator`/`browser`/`meditator` 等子代理，需要一个与主会话生命周期隔离的子会话执行单元。它必须满足：

1. 一次只运行一个 turn，避免同一子会话被并发调用。
2. 模型选择可配置：既可以由万象术自己的 fallback chain 驱动，也可以完全交给宿主（DelegateToHost）。
3. 取消是硬边界：用户 Abort 或 turn 超时后，必须等待宿主确认停止，而不是立刻继续下一 turn。
4. 可恢复：重启后若子会话仍在运行，必须 poison 该 actor，避免状态未知。

## 二、核心类型

### 2.1 标识符

* `RunId`：一次子代理运行的唯一 ID。
* `TurnId`：一次运行内的 turn ID，形如 `run-xxxx-t0`。
* `TurnOrdinal`：turn 序号，从 0 开始。
* `SessionId`：子会话的物理会话 ID。

### 2.2 模型指令

```fsharp
type ModelDirective =
    | RetryChain of FallbackChain
    | DelegateToHost
```

* `RetryChain`：万象术按配置链选择模型，并在失败时切换下一个模型。
* `DelegateToHost`：不传递任何 `model` 字段，让宿主使用自己的 agent/model 配置。

模型指令选择优先级见 `FallbackConfigCodec.resolveModelDirective`：

1. 若宿主已显式配置该 agent 的模型 → `DelegateToHost`。
2. 否则按 `config → child runtime → parent runtime → parent live model` 解析 chain；非空则 `RetryChain`。
3. 完全无信息时 → `DelegateToHost`，而不是直接失败。

### 2.3 运行证据

`CurrentTurnEvidence` 取代旧式布尔快照，基于当前 turn 切片后的 transcript 评估：

* `AssistantEvidence`：无 assistant / 空 assistant / 有内容（并标记 `ToolFinish` 或 `NormalFinish`）。
* `TodoEvidence`：当前 turn 的 todo 是否完成。
* `ToolEvidence`：是否有 tool result。
* `RecoveryEvidence`：是否有 recovery prompt。

证据在 `Dispatching` 阶段即开始缓冲，因为宿主返回 assistant message 的速度可能快于 dispatch 确认。

## 三、状态机

`SubsessionState` 包含以下状态：

| 状态 | 含义 |
| :--- | :--- |
| `Available` | 空闲，可接受新运行 |
| `Dispatching` | 已请求 dispatch，等待宿主确认接受 |
| `CancellingDispatch` | 取消发生在 dispatch 确认前，需等待 dispatch 结果 |
| `ReconcilingUnknownDispatch` | dispatch 是否被宿主接受未知，需查询宿主 |
| `Running` | 宿主已确认运行，等待 idle 或 error |
| `Draining` | 宿主报告 error，但需等 idle 证明真正停止 |
| `IssuingAbort` | 正在请求宿主 abort，尚未确认 |
| `AwaitingAbortSettle` | 宿主已接受 abort，等 idle 证明停止 |
| `ReconcilingAbortSettle` | idle 后需再确认宿主是否真的已停止 |
| `Poisoned` | 状态机遇到非法转移或不可恢复错误，永久拒绝新运行 |

## 四、Actor 实现

`SubsessionActor` 是单线程的：

* 使用 `SerialQueue` 保证 `decide + append events + commit state` 原子执行。
* `DispatchPrompt`/`AbortHostSession`/`QueryDispatchStatus` 等宿主副作用在队列外 fire-and-forget，完成后通过 `Post` 重新进入队列。
* `BeginRun` 与 `StartRun` 为同一原子入口，在队列内注册 `Deferred` 并执行 `StartRun`。
* 事件追加失败会触发 fail-safe：尝试 abort 宿主；若运行已经结束，则 poison 并返回 `InfrastructureFailure`。

## 五、取消协议

取消不是简单设置 `Lifecycle = Cancelled`，而是完整协议：

1. 用户触发 `CancelRequested`。
2. 状态机进入 `IssuingAbort`，发出 `AbortHostSession` 效应。
3. 宿主可能返回：
   * `ConfirmedStopped` → 直接应用 `AfterAbort`。
   * `RequestAcceptedAwaitIdle` → 进入 `AwaitingAbortSettle`，等 idle。
   * `AbortUnavailable`/失败 → 保持 `IssuingAbort`，等待 abort deadline。
4. `SessionIdleObserved` 在 `IssuingAbort` 阶段不直接 settle，必须等宿主确认。
5. 若 abort deadline 到期仍未 settle → `Poisoned(AbortDidNotSettle)`。

## 六、与旧 `SubsessionPending` 的关系

旧代码把子会话等待简化为 `SubsessionPending` 布尔门禁。新架构下，子会话运行由 `SubsessionState` 显式表达，任何 `Running`/`Dispatching`/`IssuingAbort` 等活跃状态都等价于旧门禁的"正在子会话中"。因此：

* 主会话 nudge/fallback 不应在子会话活跃时抢跑。
* PRD 中提及的 `SubsessionPending` 应理解为"子会话 actor 处于非 `Available` 状态"。

## 七、事件与重放

`SubsessionEvent` 包含：

* `RunStarted`
* `TurnDispatchRequested`
* `TurnStarted`
* `TurnFinished`
* `AbortRequested`
* `RunFinished`
* `SessionPoisoned`
* `PhysicalSessionClosed`

重启后，若 actor 恢复为任意非 `Available`/`Poisoned` 状态，必须调用 `reconcile` 生成 poison 事件，使运行 durably 结束。

## 八、验收要点

* 同一子会话并发启动第二次运行 → `AlreadyRunning`。
* 取消后 dispatch 仍被确认 → 必须走 abort 协议，不能直接继续。
* 取消后 dispatch 明确被拒 → 安全结束，无需 abort。
* 宿主未报告 accept/reject 时取消 → 进入 `ReconcilingUnknownDispatch` 查询。
* 快速 provider 在 dispatch 确认前返回完整 assistant → 证据不丢失，最终进入 `Running` 并正确分类。
* 事件追加失败 → 运行被 poison 或进入 fail-safe abort，绝不返回 `Succeeded`。
* 重启后未完成运行 → `Poisoned SessionStateUnknownAfterRestart`。

## 九、与 Flow-first 管线的关系

子会话 Actor 是 Flow 管线之外的独立执行单元。主会话 Flow 的 `scan` 通过 `SubsessionState` 投影了解子会话状态，将"子会话活跃"映射为 `NudgeBlockReason.RunnerOwnsTurn` 或等价阻塞原因。

子会话内部的 dispatch/abort/settle 不直接参与主会话的 `scan`，但其结果（`RunFinished`/`TurnFinished`）作为 Input 回到主会话 Channel，由主会话 `scan` 消费。

这保持了 FLOW.md 中的约束：

> 所有并发 effect 返回结果都必须回到唯一 fold。

子会话 Actor 内部的 `SerialQueue` 等价于 per-session mailbox 的串行保证，只是作用域限于子会话。
# Message Transform 钩子重构：TransformState 与引用稳定

## 一、Flow 管线位置

本文件描述 transform 钩子的内部状态管理重构。在 Flow-first 架构中，transform 钩子位于 `scan` 输出（Step.State）与实际 provider 调用之间——它是 message pipeline 的**同步变换层**，不属于 `flatMapMerge` effect 区。

```text
committed kernel.step result
  → Step.State (含 projection 快照；这里只展示已提交 step 到 transform 的输入，不是提交算法)
  → transform hook (本文件)
      ├── CAPS 段（TransformState.Caps）
      ├── Backlog 段（TransformState.Backlog）
      └── Top slot 段（TransformState.Top）
  → replaceArrayInPlace → provider 调用
```

TransformState 不是 correctness cache——它就是 synthetic 注入段的进程状态；其中按 revision key 复用 CAPS/segment 只是性能优化。其内容 SSOT 关系遵循 PRD-09 投影纪律：NDJSON 是 durable SSOT，TransformState 从中派生。

## 二、当前 bug 面：整体重算而非增量维护

当前三类问题共享同一根因——transform 钩子每轮做"整体重算"而非"增量维护"：

### 问题 A：错误的身份/指纹式缓存判定

`MessageTransformHostEntry.fs` 里的 `SessionTransformCache` 不能以对象身份或一组不相干的元数据作为正确性判定。正确性必须比较最终 outbound message 的规范化内容及其稳定 prefix 的字节序列；`ReferenceEquals` 只能作为性能优化（命中时可复用对象），绝不能证明内容相同。缓存条目由 `scopeId × capsRevision × policyVersion` 等明确 revision 组成，不使用 hash。

### 问题 B：stripSyntheticBySource 无条件调用

`Messaging.stripSyntheticBySource` 在每次 transform 开头无条件扫描并剔除所有 `Synthetic` 消息（caps、backlog projection、context-budget-nudge、parallel-tool-synth），然后管线重新计算、重新插入。哪怕只有栈顶的 nudge 需要换一条，也要把栈底稳定了几十轮的 caps 前缀一起摘掉再贴回去。

### 问题 C：transform 钩子里正则推断 review 状态

`messagesTransform` 钩子里通过 `extractTextsFromEncodedMessages` + `inferReviewTaskFromTexts` 从消息文本里正则解析 `task:` frontmatter 来推断 review 是否激活。这不是 transform 钩子该做的事：消息切片在 compaction 后可能看不到激活锚点，真相源应只有一个 `.wanxiangshu.ndjson`（PRD-08 ReviewProjection）。

## 三、关键事实修正：transform 钩子修改不持久

### 原假设（已推翻）

"opencode 主机传给 transform 钩子的 `messages` 数组，其前缀本身就是 host 侧维护好的、跨轮次稳定的引用"，据此设计了"真栈"方案——记住上一轮推了几个 synthetic 元素，本轮弹出那么多个，再 push 新的。

### 实际行为（源码验证）

OpenCode `prompt.ts` 的 `runLoop` 每次循环迭代顶部：

```typescript
// prompt.ts:1092
let msgs = yield* MessageV2.filterCompactedEffect(sessionID)
```

`filterCompactedEffect` → `stream(sessionID)` → `page()` 从**数据库**分页读取消息，每次产生全新 JS 对象。然后才调用 transform 钩子：

```typescript
// prompt.ts:1254
yield* plugin.trigger("experimental.chat.messages.transform", {}, { messages: msgs })
```

插件的 `messagesTransform` 用 `replaceArrayInPlace` 原地修改 `msgs`。修改后的 `msgs` 直接传给 provider。**但修改从未写回数据库。** 下一轮迭代 `msgs` 重新从 DB 读取，上一轮注入的 synthetic 消息全部消失。

`ContextBudget.fs:91` 注释已承认此事实：`"Host strips synthetic nudge each transform round; reinject whenever pressure still holds."`

**结论**：transform 钩子的修改不持久。"真栈"的 pop 操作是空操作——上一轮推入的 synthetic 元素在本轮数组里根本不存在，无从弹出。

### 正确认识

* **无钩子时**，OpenCode 前缀天然稳定：同一 human turn 内 auto-continue 步骤间 DB 行不变（只有新 tool result 追加到末尾），前缀内容字节一致 → provider prompt-cache 命中。
* **有钩子时**，钩子每轮收到 fresh host 数组（DB 直读，无上一轮 synthetic），必须重新注入全部 synthetic 消息。
* **问题**：如果每轮重新创建 synthetic JS 对象（即使内容相同），对象引用不同 → 下游 `replaceArrayInPlace` 产出全新数组 → 无法保证 JSON 序列化字节一致。
* **解法**：在 RuntimeScope 中维护 synthetic 注入段的状态。状态不变时可复用同一份 JS 对象引用以减少分配；状态变化时重建。host 前缀天然稳定，发送前再以 canonical outbound bytes/prefix equality 判断是否可以命中 prompt-cache。

## 四、状态层次与 SSOT

| 层级 | 持久性 | 角色 |
| :--- | :--- | :--- |
| NDJSON 事件日志 | 跨重启 | durable SSOT：backlog 内容、review 状态、续命租约、nudge 去重（PRD-09） |
| `FallbackRuntimeState` / `ContextBudgetStore` / `TransformState` | 进程生命周期 | 进程状态：从 NDJSON fold 重建（重启时），运行时更新 |
| 每轮 host 消息数组 | 瞬时（不持久） | 非状态：每轮从 DB 重读，上一轮修改消失 |

| 段 | 内容 SSOT | TransformState 字段 | 何时更新 |
| :--- | :--- | :--- | :--- |
| CAPS | `CapsFormat` / `CapsPrelude` | `Caps: { ScopeId; CapsRevision; PolicyVersion; Segment } option` | scope、caps 内容变更计数或策略版本变化 → 重建对应段 |
| Backlog | NDJSON `work_backlog_committed`（PRD-07） | `Backlog: { BacklogRevision: int; Segment: obj array }` | `BacklogRevision` 变化 → 重建投影段 |
| Top slot | `PromptFragments`（docs/10） | `Top: { BudgetRevision; Key: TopSlotKey; Item: obj option }` | revision 或 identity-bearing key 变化 → 重建或清除 |

## 五、设计原则

1. **不弹不改 host 前缀**：每轮收到 fresh host 数组，原样保留。
2. **revision 先决定候选复用**：用 scope/revision/policy 计数和 identity-bearing ADT 判断哪些段可能复用；不使用内容 hash。
3. **规范化内容是正确性证明**：发送前比较 outbound message 的 canonical bytes（含稳定 prefix）；只有 canonical bytes 相等才可声称 prompt-cache 可复用。`ReferenceEquals` 仅用于减少分配，不能推出字节相等。
4. **字符串状态全部升级为 ADT**：F# union case 的相等比较编译为 tag 整数比较，零字符串比较。
5. **保留文档记载的行为**：管线行为（7 阶段、同 source 替换不重复追加等）正确；本计划只改实现方法，不改行为契约。

## 六、与文档管线（docs/10）的对应

| 阶段 | 文档描述 | 本计划调整 |
| :--- | :--- | :--- |
| 1. 剥离 synthetic | `stripSyntheticBySource` 无条件调用 | **删除**：修改不持久，host 数组中不存在上一轮 synthetic，strip 是空操作 |
| 2. Caps / 清理 | caps 前缀注入 + 消息净化 | `TransformState.Caps` 按 `scopeId × capsRevision × policyVersion` 复用 |
| 3. Backlog 投影 | 从事件 fold 投影，非历史 tool SSOT | `TransformState.Backlog`（`BacklogRevision` 驱动） |
| 4. Review replay | 从消息文本推断 review 状态 | **删除死代码**：`_replayTexts` 参数从未被使用，review 状态走 NDJSON 事件 fold（PRD-08） |
| 5. applyContextBudget | `classifyPressure` → 紧急 nudge 注入 | `TransformState.Top`，`BudgetRevision` + `TopSlotKey` 驱动。扩展现有 `ContextBudgetStore.StableSyntheticNudgeID` + `BudgetNudgeTrack`（ADT）到完整 JS 对象 |
| 6. tryInjectParallelToolPrompt | 单工具伪装并行提示 | `TransformState.Top`，与 budget nudge 互斥 |
| 7. Semble | investigator 断点注入 | investigator 始终收到 CAPS；Semble 操作 last assistant 的 parts，与三段状态的数组级操作正交 |

## 七、TransformState 类型

```fsharp
type TransformState = {
    Caps: { ScopeId: string; CapsRevision: int; PolicyVersion: int; Segment: obj array } option
    Backlog: { BacklogRevision: int; Segment: obj array }
    Top: { BudgetRevision: int; Key: TopSlotKey; Item: obj option }
}

// CapsRevision/BacklogRevision 等均为对应内容的单调 revision/counter，
// 不是全局事件数，也不是内容 hash。
type TopSlotKey =
    | NoTop
    | BudgetNudgeTop of episodeId: string * syntheticId: string * contentVersion: int
    | ParallelHintTop of callId: string * assistantMessageId: string * contentVersion: int
```

存在 `RuntimeScope` 里，key 用 `sessionID`，跟进程生命周期，不落盘。重启后自然为空 → 从 NDJSON 重建。

### 专用 revision 计数器

系统必须维护彼此独立的单调计数器：`BacklogRevision`、`CapsRevision`、`ReviewRevision`、`BudgetRevision`。每个计数器只在自己负责的规范化内容发生变化并完成相应持久化提交后递增：

* `BacklogRevision` 只表示 backlog projection 内容变化；
* `CapsRevision` 只表示 CAPS 内容变化；
* `ReviewRevision` 只表示 review projection 内容变化；
* `BudgetRevision` 只表示 budget pressure/nudge 决策内容变化。

禁止恢复或新增全局事件总数，也禁止用 event-log 总数、对象 identity 或 hash 充当这些 revision。compaction、host 数组重读、token 估算抖动在没有对应内容变化时不得递增任何 revision。`ReviewRevision` 虽不驱动 synthetic review 注入，却必须由 review projection 的消费者使用，以避免 transform 从消息文本推断 review。

### CAPS 状态

* CAPS cache 必须以 `scopeId × capsRevision × policyVersion` 为 key；三者任一变化，都必须按新的规范化 CAPS 内容构建对应段。cache key 不得包含 hash，也不得以对象 identity 代替任一字段。
* `scopeId` 隔离 RuntimeScope；`capsRevision` 仅在 CAPS 内容发生变化时递增，`policyVersion` 仅在 CAPS 策略发生变化时递增。内容未变时可复用已有段引用，但必须能以 canonical outbound bytes/prefix equality 证明正确性。
* 进程重启后 RuntimeScope 可为空，首次命中该 key 时重建；CAPS 段不得被视为永久构造物。
* compaction 不改变 CAPS revision；investigator 会话和 investigator 断点消息仍必须收到 CAPS。

### Backlog 状态

* 从对应 projection 读取 `BacklogRevision`；它只在 backlog 内容发生变化并提交 `work_backlog_committed` 时递增。不得用全局事件总数或任意 event-log 总数代替它。
* `BacklogRevision = state.Backlog.BacklogRevision` 时可以复用 `Segment` 引用；revision 变化时必须以新的 folded backlog 重建投影段。
* compaction 不改变 backlog 内容，也不递增 `BacklogRevision`，因此可复用段；若 compaction 明确改变 backlog 内容，则必须先产生对应 revision，再重建。
* auto-continue 中的 `todowrite` 只有在对应 backlog commit 成功后才递增 `BacklogRevision`，并只重建 backlog 段。

### Top slot 状态

关键区分（PRD-04 + PRD-06）：
* **context-budget-nudge**：transform 管线内的同步注入，由 `classifyPressure` 返回 `RequireTodoWriteEmergency` 驱动。消息内容固定（`PromptFragments` SSOT）。`ContextBudgetStore` 已有 `BudgetNudgeTrack` ADT（`Idle` / `EmergencySignaled`）。
* **async nudge**：`SessionIdle` 后的异步 `session.prompt`，由 `NudgeRuntime` 编排。不经 transform 钩子，不在 Top slot 范围内。
* **parallel-tool-synth**：`tryInjectParallelToolPrompt` 条件注入，消息内容固定。

`TopSlotKey` 必须携带 identity：budget nudge 绑定 episode/synthetic identity 和 content version；parallel hint 绑定 tool call、assistant message identity 和 content version。不得以无 payload 的 ADT tag 复用不同 top item。

`computeTopSlotKey` 从 `ContextBudgetStore` + 当前消息列表读取决策，返回完整 `TopSlotKey`。比较 key 时必须比较其 identity payload；key 相等时可复用 item 引用，key 不等时必须重建或清除。canonical 内容仍是正确性标准。

`BudgetRevision` 只在 budget pressure 或 nudge 内容发生变化后递增。conversation start 不得凭空产生 emergency todo prompt；settled 后不得通过 fallback nudge 补发。parallel hint 仍只在最后一条 assistant 恰有一个 tool call 时注入（single-tool hint），并以 call/message identity 区分实例。

Budget nudge 和 parallel-hint 互斥（pipeline 顺序已决定优先级，阶段 5 在阶段 6 之前）。

与现有代码的关系：`ContextBudgetStore.StableSyntheticNudgeID` 已存 nudge ID 用于 `isSameEpisode` 判断（`MessageTransformCore.fs:198-210`）。本计划将状态从 ID 扩展到完整 JS 对象引用，避免每轮 `buildContextBudgetNudgeMessage` 重构。

## 八、transform 函数

```text
transform(hostArray, state) → (newArray, newState)

1. CAPS（阶段 2）：
   let key = (scopeId, capsRevision, policyVersion)
   match state.Caps with
   | Some cached when cached key = key → reuse cached Segment as an optimization
   | _ → build canonical CAPS Segment for key, state.Caps ← Some {
             ScopeId = scopeId; CapsRevision = capsRevision
             PolicyVersion = policyVersion; Segment = newSegment }
   unshift the segment only after canonical outbound prefix equality is established

2. Backlog（阶段 3）：
   let revision = ProjectionStore.GetBacklogRevision(sid)
   if revision = state.Backlog.BacklogRevision → append state.Backlog.Segment
   else → rebuild, state.Backlog ← { revision, newSegment }, append

3. Top slot（阶段 5+6）：
   let budgetRevision = BudgetStore.GetBudgetRevision(sid)
   let key = computeTopSlotKey(contextBudgetStore, messages)
   if budgetRevision = state.Top.BudgetRevision && key = state.Top.Key →
       match state.Top.Item with
       | Some item → append item          // reference reuse is optimization only
       | None → ()                         // 不追加
   else →
       rebuild or clear, state.Top ← { budgetRevision, key, newItem }, append

4. replaceArrayInPlace(hostArray, finalArray)
5. assert canonical outbound content and stable prefix equality; return (finalArray, state)
```

核心不变式：正确性由每轮 outbound message 的 canonical content 和 prefix bytes equality 决定。revision/key 未变时可以通过 `ReferenceEquals` 复用 synthetic 段以减少分配；该引用相等只是性能优化，不能作为内容相等或 prompt-cache 命中的证明。

行为保持：docs/10 记载的"同 source 已存在则替换而非无限追加"行为不变——由于 host 数组不含上一轮 synthetic，"替换"自然退化为"追加"，效果等价。

## 九、字符串状态升级为 ADT

现有代码中大量使用字符串做同步判断（`sessionOwner = "Fallback"`、`lease.Status = "requested"` 等）。全部替换为 ADT，消除字符串比较。

### 新增 ADT 类型

```fsharp
// 替代 sessionOwner: string（"None"/"Human"/"Fallback"/"Nudge"/"Compaction"）
type SessionOwner =
    | NoOwner
    | Human
    | Fallback
    | Nudge
    | Compaction

// 替代 PendingLease.Status: string 和 NudgeLease.Status: string
type LeaseStatus =
    | Requested
    | DispatchStarted
    | Dispatched
    | Running
    | Cancelled

// 替代 finishContinuation 的 outcome: string（"failed"/"cancelled"/"settled"）
type ContinuationOutcome =
    | Failed
    | Cancelled
    | Settled
```

F# 对 union case 的相等比较必须包含 payload；不得依赖字符串化状态或 hash。内容正确性仍由 canonical outbound bytes 断言。

### ADT 相比 enum 的优势

| 方面 | enum | ADT |
| :--- | :--- | :--- |
| 比较成本 | 整数 | tag 整数（无数据 case）或 tag+payload |
| 携带数据 | 否 | 是（`BudgetNudgeTop` 未来可携带 ordinal） |
| 穷尽匹配 | 需手动 | 编译器强制 |
| 新增 case | 全部 `if/match` 静默漏过 | 编译器报错所有未处理位置 |

### 受影响位置

| 现有（string） | 替换为（ADT） | 影响文件 |
| :--- | :--- | :--- |
| `sessionOwner: string` | `SessionOwner` | `FallbackRuntimeState.fs` 全部 owner 读写；`FallbackEventBridge.fs` 全部 owner 比较 |
| `PendingLease.Status: string` | `LeaseStatus` | `verifyLeaseWithStatus`、`TryTransitionPendingLease`、`executeContinuationIntent` |
| `NudgeLease.Status: string` | `LeaseStatus`（复用） | `TryTransitionPendingNudgeLease` |
| `finishContinuation` 的 `outcome: string` | `ContinuationOutcome` | `finishContinuation`、所有调用点 |
| `PendingLease.Owner: string` | `SessionOwner`（复用） | lease 构造与验证 |
| `NudgeLease.Owner: string` | `SessionOwner`（复用） | nudge lease 构造与验证 |

## 十、Auto-continue 与复杂时序

### Auto-continue（OpenCode tool-call 循环）

OpenCode `runLoop` 每步：`status.busy` → `filterCompactedEffect`（DB 重读）→ transform 钩子 → provider → tool result 入 DB → 循环。只有循环退出才 `session.idle`（PRD-02）。

transform 钩子被调用多次（每步一次），每次收到 fresh host 数组。

| 计数器/状态 | auto-continue 步骤间 | TransformState 行为 |
| :--- | :--- | :--- |
| `sessionGeneration` | 不变（同一 human turn） | — |
| `BacklogRevision` | 可能变（tool 里有 todowrite 并完成 backlog commit） | 变 → backlog 重建；不变 → 可复用引用 |
| `BudgetNudgeTrack` | 可能变（token 增长触发 `EmergencySignaled`） | 变 → top slot 重建；不变 → 同一份引用 |
| `Caps` | key 通常不变 | key 未变时可复用引用；发送前验证 canonical bytes |

结果：auto-continue 步骤间若无 backlog content change 且未触发 budget nudge，所有 synthetic 段可用 `ReferenceEquals` 减少分配；发送前仍须验证 canonical JSON bytes/prefix equality，满足时 prompt-cache 才可跨步骤命中。有 todowrite 并完成 backlog commit → 只重建 backlog 段。触发 budget nudge → 只重建 top slot 段。CAPS 按 key 判断是否重建。

### Compaction

compaction 发生时 `compactionOrdinal` 递增，`SessionOwner` 短暂为 `Compaction`，完成后回到 `NoOwner`。仅 `contextGeneration` 递增；`sessionGeneration` 保持不变，因为 compaction 仍属于同一会话。host 消息被摘要替换。

compaction 对 host/数据库摘要的持久化不等于 projection event commit：它本身不得递增 `BacklogRevision`、`CapsRevision`、`ReviewRevision` 或 `BudgetRevision`。只有对应内容改变并完成 NDJSON/journal append 后，所属 revision 才递增。TransformState 仍是进程内派生状态；重启或重新加载时从 durable NDJSON 重建，而不是从 compaction 后的 host 数组猜测。

| 段 | compaction 后 | 原因 |
| :--- | :--- | :--- |
| CAPS | revision/key 通常不变 | CAPS 内容由 `CapsFormat` 规范化生成，与对话 compaction 无关；investigator 仍接收该段 |
| Backlog | `BacklogRevision` 不变（若 backlog 内容未变） | backlog 投影从 NDJSON 事件 fold 计算（PRD-07），不从 host 消息计算；compaction 不写 backlog event |
| Top slot | 可能重建 | `ContextBudgetStore` phase reset（PRD-04），`BudgetNudgeTrack` 回到 `Idle` → `TopSlotKey` 从 `BudgetNudgeTop(...)` 变为 `NoTop` → 重建 |

### Async nudge 派发（PRD-06）

async nudge 是 `SessionIdle` 后的异步 `session.prompt`，不经 transform 钩子。nudge 派发时 `nudgeOrdinal` 递增，`SessionOwner` 变为 `Nudge`。

* 对 TransformState 无直接影响：async nudge 创建的是真实 user 消息（DB 持久化），不是 synthetic 消息
* 下一轮 transform 收到的 host 数组包含这条 nudge 消息（作为 Native 消息），不影响 synthetic 段状态
* CAPS、Backlog、Top slot 均不受影响

### Fallback continuation 派发（PRD-02）

continuation 派发时 `continuationOrdinal` 递增，`SessionOwner` 变为 `Fallback`。ZWS prompt 作为真实 user 消息入 DB。

* 对 TransformState 无直接影响：与 async nudge 类似，continuation 创建真实 user 消息
* CAPS、Backlog 不受影响
* Top slot：`BudgetNudgeTrack` 可能因 token 增长而变化 → 独立判断

### 新 human turn

仅当前真人轮次的 `turnGeneration`/`cancelGeneration` 递增，`sessionGeneration` 保持不变；`SessionOwner` 变为 `Human`，pending lease/nudge lease 被取消。

* `ContextBudgetStore` phase reset：`BudgetNudgeTrack` 回到 `Idle` → `TopSlotKey` 变为 `NoTop` → 重建（清除）
* CAPS：不变
* Backlog：`BacklogRevision` 不变 → 可复用引用；有新 todowrite 且 commit 成功 → 重建

### 状态变化总览

```
                        auto-continue    compaction     async nudge   cont.派发     新human turn
──────────────────────────────────────────────────────────────────────────────────────────────────
sessionGeneration       不变             不变           不变          不变          递增
BacklogRevision         可能递增         不变           不变          不变          可能递增
CapsRevision            不变             不变           不变          不变          不变
ReviewRevision          可能变           不变           不变          不变          可能变
BudgetRevision          可能变           phase reset    不变          不变          phase reset
BudgetNudgeTrack        可能变           Idle(复位)     不变          不变          Idle(复位)
SessionOwner(ADT)       不变             Compaction    NoOwner→Nudge NoOwner→Fallback NoOwner→Human
                                        →NoOwner(完成后)

Caps                    不变             不变           不变          不变          不变
Backlog                 revision不变→复用 不变           不变          不变          revision变化→重建
Top                     key不变→复用      可能重建       不受影响      不受影响      key变→重建
```

关键区别：async nudge 和 fallback continuation 创建的是真实 user 消息（DB 持久化），不是 transform 钩子的 synthetic 注入。它们不影响 TransformState，只影响下一轮 host 数组的内容。

## 十一、新增/删除的模块

### 删除

* `Shell/MessageTransformHostEntry.fs` 里的 `TransformFingerprint`、`FingerprintMetadata`、`SessionTransformCache`、`computeFingerprint`、`fingerprintEqual`、`computeMetadata`、`getSessionCache`——整套深比对机制全部删除。
* 三个 Host 的 `MessageTransform.fs` 里对 `stripSyntheticBySource` 的无条件调用——transform 钩子修改不持久，strip 是空操作。
* `runHostMessagesTransform` 的 `_reviewReplayMode` 和 `_replayTexts` 两个死参数。
* `Kernel/ReviewReplayPolicy.fs` 的 `reviewTaskFromTexts`、`Shell/ReviewReplaySync.fs` 的 `syncReviewFromTexts`——不再在 messagesTransform 路径里调用。保留 `Kernel/LoopMessages.fs::inferReviewTaskFromTexts` 本体和独立单测。

### 新增

* `Shell/MessageTransformStack.fs`：TransformState 类型 + `TopSlotKey` ADT + `computeTopSlotKey` 函数 + RuntimeScope 存取函数。
* ADT 类型定义（放在 `Kernel/FallbackKernel/Types.fs`）：`SessionOwner`、`LeaseStatus`、`ContinuationOutcome`。

### 修改

* `Shell/FallbackRuntimeState.fs`：`sessionOwners: Map<string, string>` → `Map<string, SessionOwner>`；`PendingLease.Status`/`NudgeLease.Status` → `LeaseStatus`；`PendingLease.Owner`/`NudgeLease.Owner` → `SessionOwner`。
* `Shell/FallbackEventBridge.fs`：`verifyLeaseWithStatus` 参数 `string` → `LeaseStatus`；`finishContinuation` 参数 `string` → `ContinuationOutcome`；所有字符串字面量 → ADT case。
* `Shell/MessageTransformPipeline.fs`：按三段独立判断重构。
* `Shell/MessageTransformCore.fs::checkAndInjectNudge`：改造为读写带 identity payload 的 `TransformState.Top`。
* `Shell/MessageTransformHostEntry.fs`：删除整套 Fingerprint 机制。
* 三个 Host `MessageTransform.fs`：删除 `stripSyntheticBySource` 无条件调用；删除 `replayTexts` 相关死代码。

## 十二、Review 状态：messagesTransform 钩子里做什么

结论不变：messagesTransform 钩子对 review 不需要做任何事——不渲染、不推断、不同步。删除 `replayTexts`/`_replayTexts` 死参数就是全部动作。真正的 review 状态同步路径（`syncReviewFromEventLogDedicated`，在 `/loop` 命令执行、reviewer 会话结束时调用）保持不变，它已经是从 `.wanxiangshu.ndjson` 单向恢复到 `ReviewStore` 内存投影，并在 review 内容提交变化时递增 `ReviewRevision`，符合“真相源唯一”的原则。investigator 消息变换仍必须注入 CAPS，不得因 review replay 被删除而省略 CAPS。

## 十三、分步执行顺序

1. **Kernel 层**：新增 `SessionOwner`、`LeaseStatus`、`ContinuationOutcome` ADT 类型定义。
2. **改 `Shell/FallbackRuntimeState.fs`**：全部 `string` → ADT。
3. **改 `Shell/FallbackEventBridge.fs`**：所有字符串字面量替换为 ADT case。
4. **新增 `Shell/MessageTransformStack.fs`**：TransformState + TopSlotKey + computeTopSlotKey + RuntimeScope 存取。
5. **改 `Shell/MessageTransformHostEntry.fs`**：删除 Fingerprint 机制，签名去掉死参数。
6. **改 `Shell/MessageTransformPipeline.fs`**：按三段独立判断重构。
7. **改 `Shell/MessageTransformCore.fs::checkAndInjectNudge`**：改造为读写 TransformState.Top。
8. **改三个 Host `MessageTransform.fs`**：删除 `stripSyntheticBySource`；删除 `replayTexts` 死代码。
9. **删除死代码**：`reviewTaskFromTexts`、`syncReviewFromTexts`。
10. **调整测试**：去掉死参数；字符串断言改为 ADT 断言；新增 `MessageTransformStackTests.fs`。
11. **验证**：重点检查 canonical outbound bytes/prefix equality、四个专用 revision 的递增边界、CAPS cache key 隔离和 investigator CAPS 注入；不得以 `ReferenceEquals` 作为正确性断言。

## 十四、不变式（验收标准）

1. 同一 session 连续两轮 transform 的 correctness 由 outbound canonical content 与稳定 prefix bytes equality 证明。revision/key 未变时 synthetic 段可逐一 `ReferenceEquals` 复用，但该引用相等仅是性能优化。
2. CAPS 段按 `scopeId × CapsRevision × policyVersion` 缓存；任一 key 字段变化必须重建对应段，key 不变时才可复用。CAPS 不得被当作永久不变的单次构造；compaction 不因自身动作改变 CAPS revision。investigator 始终收到 CAPS。
3. Backlog 折叠区间只有在对应 `BacklogRevision` 递增时才会整体替换；不递增则可复用。compaction 不改变 backlog 内容时不产生 backlog commit，因此 `BacklogRevision` 不变。
4. Top slot 由带 identity payload 的 `TopSlotKey` 驱动：`NoTop`、`BudgetNudgeTop(episodeId, syntheticId, contentVersion)`、`ParallelHintTop(callId, assistantMessageId, contentVersion)`。key 不变可复用 item；key 变必须重建或清除。
5. Auto-continue 步骤间：若 backlog/caps/review/budget 内容均未变化，且 top key 未变化，发送前 canonical bytes/prefix equality 成立时 prompt-cache 才可跨步骤命中。
6. messagesTransform 钩子对 review 激活/关闭状态零读取、零写入。
7. `FallbackRuntimeState` 和 `FallbackEventBridge` 中不存在任何字符串字面量做状态比较。全部使用 ADT case。
8. docs/10 记载的管线行为保持不变：caps 注入、backlog 投影、context budget nudge、parallel hint 的触发条件和注入内容不变，只改状态管理机制。
9. 架构测试不回归：`UsesProjectionPolicy`、`noQuadraticListAppend`、`parallelToolPromptSSOTGuard` 等。
10. async nudge（PRD-06）和 fallback continuation（PRD-02）不受本计划影响：它们创建真实 user 消息（DB 持久化），不经 transform 钩子 synthetic 注入。
11. `TransformState` 是 synthetic 注入段的唯一进程状态——不存在第二份"缓存"或"投影"与之竞争。NDJSON 是 durable SSOT，`TransformState` 从中派生。
# E2E 测试设计要求：Flow-first 验证基础设施

## 一、Flow 管线位置

E2E 测试是 Flow-first 架构（PRD-00）的端到端验证层。它验证完整管线 `Host events → Channel → scanCommit → append → publish → provider → tool result → feedback` 在真实宿主中的行为正确性。

E2E 测试不验证纯函数内核（那些用单元测试覆盖），而是验证 Host adapter 归一化、per-session mailbox 串行化、lease 校验、projection 恢复等 Shell 层行为。

## 二、时间预算、网络与套件边界

E2E 必须使用按等待对象分类的超时，而不是一个适用于所有操作的全局值。超时是测试失败的安全网，失败信息必须包含操作、宿主、session 和调用栈：

* **本地事件观察**（`fireEvent`、`waitForCalls`、`waitForNdjson` 等）：1s；
* **本地 Host API**（`sendPrompt`、`runCommand` 等）：3–5s，按接口契约选择；
* **宿主 bootstrap**（冷启动、动态端口、插件就绪和 warmup）：10–20s，按宿主的已知启动预算选择；
* **外部网络**：正确性套件禁止访问外网，必须使用 fake provider。确需网络的实验不属于这些 E2E 门禁。

四个宿主在 30s 内完成一轮是**性能目标**，不是 correctness gate；机器负载或冷启动差异不得使正确性测试失效。CI 分为 `quick`（本地事件和 Host API 的核心路径）、`full`（完整 Flow-first 矩阵）和显式的 `restart`（恢复/崩溃路径）套件，各自使用上面的分类超时。

正常的 `quick`/`full` 套件对每个 Host 只启动一个受管实例，在用例间复用它但不重启 Host。任何用于测试行为的 kill/restart 仅允许出现在显式的 `restart` 套件；不得把重启隐藏在普通 suite 的 setup、teardown 或测试用例中。普通 suite 的 teardown 仅执行资源清理，不构成重启测试。

## 三、宿主常驻单例设计（Host Singleton Lifecycle）

### 3.1 架构拓扑

```text
E2E 启动 (tests/e2e.js)
  │
  ├──► 全局 Setup: HostSingletonManager 启动
  │       ├──► OpenCode 宿主 (spawn once, listen on Port A)
  │       ├──► Mimocode 宿主 (spawn once, listen on Port B)
  │       ├──► Mux 宿主      (spawn once, listen on Port C)
  │       └──► OMP 宿主      (spawn once, listen on Port D)
  │
  ├──► 顺序/并发跑完所有正常测试用例 (Tests.fs, MuxTests.fs, etc.)
  │       ├──► 用例 1: 复用 Port A 实例 (创建新 sessionID)
  │       ├──► 用例 2: 复用 Port A 实例 (创建新 sessionID)
  │       └──► 用例 3: 复用 Port C 实例
  │
  └──► 全局 Teardown: HostSingletonManager 统一销毁
          └──► 对受管 PID/进程组先 SIGTERM，等待有界时间后才 SIGKILL & 清理临时环境
```

`restart` suite 使用同一套 Host 管理器但拥有显式的 stop/start 阶段；只有该 suite 可以验证重启后的恢复。崩溃 suite 必须对已记录的 PID/进程组执行 direct kill，以模拟崩溃，不得用模式匹配杀进程。

### 3.2 隔离环境与缓存

每次 suite 使用随机临时目录作为 `HOME`，禁止继承开发者的 `HOME`、用户配置或用户缓存。缓存加速只能通过显式挂载专用的、只读的 NPM/Bun 缓存目录完成；不得挂载整个 HOME，也不得让宿主写入这些缓存：

```javascript
HOME: path.join(tempRoot, "home"),
NPM_CONFIG_CACHE: path.join(tempRoot, "cache", "npm-ro"),
BUN_INSTALL_CACHE_DIR: path.join(tempRoot, "cache", "bun-ro"),
```

`cache/npm-ro` 和 `cache/bun-ro` 必须来自测试专用 fixture，并以只读方式挂载；下载或生成临时内容时使用 `tempRoot/cache/rw`，而不是开发者目录。`XDG_CONFIG_HOME`、`XDG_DATA_HOME`、`XDG_STATE_HOME`、`XDG_CACHE_HOME` 以及 `OPENCODE_TEST_HOME` 等写路径也必须位于本次 suite 的临时目录中。

## 四、多用例逻辑沙盒隔离（Logical Sandbox Isolation）

由于所有测试用例共享同一个常驻的宿主进程及其实体工作目录，若多个用例并发对工作区进行写操作，或者共用同一个数据库记录，必定会导致状态撕裂。必须在共享宿主下实现高度的逻辑沙盒隔离。

### 4.1 会话隔离（Session ID Isolation）

* 每一个测试用例在开始时，必须向常驻宿主发起 `POST /api/session` 请求以获取一个全新的、随机生成的 `sessionID`。
* 严禁直接使用或复用共享的默认 session。所有的提示词请求和状态变更事件，必须通过该特定的 `sessionID` 进行精准的逻辑路由。

这与 Flow-first 架构的 per-session mailbox（PRD-02）天然对应：每个 sessionID 拥有独立的串行邮箱和独立的 projection（PRD-09），测试用例之间的状态不会互相干扰。

### 4.2 工作区子目录逻辑分区（Logical Workspace Partitioning）

* 为了防止测试文件在同一个 `{workDir}` 根目录下发生脏写，每一个测试用例在调用文件读写工具时，其操作路径必须被逻辑封装在以该用例 `sessionID` 命名的独立子工作区内：

```text
[共享工作区] /tmp/wanxiang-e2e-XXXX/workspace/
  ├── [用例 A 子目录] sess_0ae74ba9.../
  │     ├── test.txt
  │     └── outputs/
  └── [用例 B 子目录] sess_0bf85cb1.../
        ├── src/
        └── outputs/
  └── .wanxiangshu.ndjson  # workspace 的唯一 production journal
```

* 插件边界与 Host Codec 在拦截或路由文件系统 IO 时，应自动完成此子工作区路径的前缀补全，从而消除用例之间的物理竞态。

生产拓扑是**每个 workspace 一个 journal**，并由该路径唯一的进程内 `JournalWriter` 串行写入；跨进程访问再由 interprocess lock 保护。session 子目录不得被解释为生产 journal 拓扑。测试可以为 fixture workspace 建立一个 `.wanxiangshu.ndjson`，各 session 通过 workspace/path 路由到同一 writer。

## 五、受管进程生命周期与孤儿进程防范策略

长寿命常驻宿主一旦由于异常流产或强制中止未被清理，会导致本地端口持续占用和 lock 文件残留。必须实施全封闭的清理防线。

### 5.1 全局同步析构（teardownSuite）

在所有正常用例跑完后，测试入口必须显式执行全局析构，释放排他锁 `E2E_LOCK`，并按记录的 PID/进程组逐个停止受管 Host：发送 `SIGTERM`，等待一个有界的 graceful-shutdown 窗口；仍未退出时只对该 PID/进程组发送 `SIGKILL`。正常 teardown 不执行 restart。

### 5.2 进程出口兜底捕获（process.once('exit')）

`HostSingletonManager` 必须在加载的第一时间向当前 Node 进程挂载事件监听器。当主测试进程由于任何预期或非预期的原因（如 `RUNALL_FAILED`、未捕获的 rejection、用户手动中断）即将退出时，必须对已记录的宿主 PID/进程组执行同样的 SIGTERM→有界等待→SIGKILL 兜底。此出口处理只负责回收受管进程，不得启动或重启 Host。

不得使用按名称或命令行模式匹配的批量 kill 命令。

### 5.3 物理文件锁与 PID 记录

E2E 的排他锁文件（`E2E_LOCK = '/tmp/wanxiang-e2e.lock'`）的获取必须处于 `HostSingletonManager` 的顶层 Setup 中。成功获取 lock 后，管理器必须记录每个 spawn 的 PID、进程组 ID、端口和退出状态；发现旧的 lock 元数据时只能校验其 PID 是否仍为受管进程，并按 PID/进程组执行有界的 SIGTERM→SIGKILL 清理。绝不能按名称或命令行模式清理其他进程。

## 六、E2E 测试与 Flow 管线的映射

E2E 测试用例应覆盖 PRD-02 到 PRD-08 中定义的必测场景，以真实宿主行为验证 Flow 管线各环节。

| PRD 问题 | E2E 验证目标 | 关键断言 |
| :--- | :--- | :--- |
| PRD-02 Esc | Abort committed 后旧 lease 不再产生新 Host dispatch；已开始 dispatch 收到 abort 请求且迟到结果不改状态 | dispatch claim/调用计数、Host abort 记录、`StaleEffectIgnored` 事件 |
| PRD-03 控制字段 | 下游参数中控制字段出现次数为 0 | execute args 中无 warn 字段 |
| PRD-04 budget | 达到真实增长阈值后触发 | synthetic nudge 出现在 outbound |
| PRD-05 日志 | stdout 中 `DEBUG:` 数量为 0 | stdout/stderr 捕获断言 |
| PRD-06 compaction | compaction 完成后普通 nudge 数量为 0 | nudge 调用计数 |
| PRD-07 model | nudge 使用当前真人轮次模型 | outbound request model 字段 |
| PRD-08 review | review nudge 包含 `original_task` | outbound front-matter 解析 |
| PRD-21 行为修复 | warn 缺失不拒绝、todowrite 短报告不拒绝 | tool success + 批评 marker |

### 6.1 测试用例间的 NDJSON 隔离

正常套件只在受管 Host 上创建隔离 `sessionID`，不执行 kill 或 restart。重启恢复测试（PRD-10）必须放在 `restart` suite：显式停止 Host、重新启动并从 workspace journal fold，随后断言 projection 恢复一致。

### 6.2 验证 projection 恢复

E2E 测试应覆盖 PRD-09 六个投影的恢复路径：
* 写入若干事件到 workspace journal；
* 在 `restart` suite 显式停止并重启宿主进程（复用 HostSingletonManager 的受管 spawn 通道）；
* 从 NDJSON fold 恢复所有投影；
* 断言 HumanTurnProjection、CancellationProjection、ContinuationProjection、CompactionProjection、ContextBudgetProjection、ReviewProjection 状态正确。

### 6.3 验证跨 Host 一致性

PRD-12 要求 OpenCode、Mimocode、Mux、OMP 行为一致。E2E 测试框架的 HostSingletonManager 天然支持跨宿主验证：同一组输入依次发送到不同宿主实例，断言结果一致。

## 七、禁止行为

* 禁止在用例内部 `spawn` 新宿主进程（违反 Single-Start Singleton）。
* 禁止复用默认 session（违反 Session ID Isolation）。
* 禁止用例之间共享工作目录（违反 Logical Workspace Partitioning）。
* 禁止不带分类超时的异步等待：本地事件观察使用 1s，本地 Host API 使用 3–5s，bootstrap 使用 10–20s。
* 禁止在 E2E 中依赖 `sleep` / `setTimeout` 做正确性保证（PRD-02 lease 校验同理）。
* 禁止在正常 suite 中 kill/restart Host；仅 `restart` suite 可执行显式停止、启动或崩溃模拟。
* 禁止使用名称匹配或命令行模式匹配的批量 kill 命令清理进程；必须使用已记录的 PID/进程组，并按 SIGTERM→有界等待→SIGKILL 处理（crash suite 使用 direct kill）。
