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
