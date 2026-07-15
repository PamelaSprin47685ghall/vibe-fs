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
