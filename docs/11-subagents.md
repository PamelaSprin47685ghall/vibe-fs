# 11 — 子代理与子会话 Actor

## 模型

子代理 = 独立会话（OMP 可为子 workspace）。委派工具：`coder`、`inspector`、`browser`、`meditator` 等；参数 `intents[]` 可多项并发。

子代理由 `src/Runtime/Subsession/SubagentDispatcher.fs` 编排；若宿主适配器选择 Actor 路径，则进入 `SubsessionActor`。文档不把两者宣称为全局“旧/新生产路径”，实际入口以宿主装配和调用链为准。

## SubsessionActor 子系统

### 动机

子代理（Coder、Inspector、Browser、Meditator）的错误不应直接穿透到父 session。`session.prompt` 的网络错误、模型降级超时、非法回复等需要在一个受控的沙箱中处理，沙箱拥有自己的 Fallback 状态机、事件溯源和超时机制。

### 架构

```
父工具（coder/inspector 等）
  → SubsessionService.StartRun
    → SubsessionActorRegistry.GetOrCreate(childID, host, eventStore)
      → SubsessionActor (SerialQueue 串行消息泵)
        → Kernel/Subsession/Decision.fs 纯函数决策
          → ISubsessionHost (宿主适配)
            → host.Dispatch (session.prompt)
            → host.Abort
            → host.QueryDispatchStatus
          → ISubsessionEventStore (NDJSON 持久化)
```

### 核心模块

| 层 | 模块 | 职责 |
| :--- | :--- | :--- |
| Kernel | `Subsession/Types.fs` | `SubsessionState` DU（9 种状态）、`Command`、`Effect`、`Decision`、`RunResult`、`TurnObservation`、`CurrentTurnEvidence` |
| Kernel | `Subsession/Decision.fs` | `decide(state, cmd)` 纯函数（~873 行），处理所有状态转移 |
| Kernel | `Subsession/Policy.fs` | Fallback 策略决策（`afterError`、`afterTranscript`、`afterSuccessfulTurn`） |
| Kernel | `Subsession/TranscriptDecision.fs` | `classifyTurnEvidence`：从 `CurrentTurnEvidence` 分类为 `CompleteNaturally` / `RecoverWithPrompt` / `ContinueNormally` / `IncompleteWithoutRecovery` |
| Kernel | `Subsession/Fold.fs` | `SessionSafetyProjection`：从 `SubsessionEvent` 列表 fold 出活跃 run 和持久中毒状态 |
| Kernel | `Subsession/PartTypeClassify.fs` | 跨宿主的 tool-call / tool-result part type 标准化集合 |
| Runtime | `src/Runtime/Subsession/SubsessionActor.fs` | Actor 消息泵：`Post`、`BeginRun`、`GetState`、重启标记 |
| Runtime | `src/Runtime/Subsession/SubsessionActorRegistry.fs` | sessionID → SubsessionActor 注册表 |
| Runtime | `src/Runtime/Subsession/SubsessionEventRouter.fs` | `routeToChild`、`tryIdle`、`tryError`、`isChildSession` |
| Runtime | `src/Runtime/Subsession/SubsessionEventStore.fs` | NDJSON 与内存测试 event store |
| Runtime | `src/Runtime/Subsession/SubsessionEventWire.fs` | NDJSON 行/批次 → SubsessionEvent |
| Runtime | `src/Runtime/Subsession/SubsessionReconcile.fs` | 启动扫描未完成 run 并建立安全投影 |
| Runtime | `src/Runtime/Subsession/SubsessionService.fs` | `StartRun`、`TryPost`、`RemoveSession` |
| Runtime | `src/Runtime/Subsession/SubsessionTranscript.fs` | 从消息切片构建 `CurrentTurnEvidence` |
| Runtime | `src/Runtime/Subsession/SubsessionChildObserver.fs` | 观察 agent/model 元数据 |
| 宿主 | `src/Hosts/OpenCode/SubsessionHostAdapter.fs` | OpenCode 的 `ISubsessionHost` 实现 |
| 宿主 | `src/Hosts/Omp/SubsessionHostAdapter.fs` | OMP 的 `ISubsessionHost` 实现 |

### SubsessionState 状态机（9 种状态）

```
Available → Dispatching → Running → [Draining →] [IssuingAbort → AwaitingAbortSettle → ReconcilingAbortSettle]
         →                                                                               → Poisoned
         → CancellingDispatch → ReconcilingUnknownDispatch → ...
```

| 状态 | 含义 |
| :--- | :--- |
| `Available` | 空闲，可接受 StartRun |
| `Dispatching(ctx, plan, bufferedEvidence)` | 已向宿主发起 `session.prompt`，等待接受确认 |
| `CancellingDispatch` | 取消请求已发出，等待 dispatch 结果 |
| `ReconcilingUnknownDispatch` | 宿主接受状态未知，主动查询 |
| `Running(ctx, started, evidence)` | 正在运行，收集 `CurrentTurnEvidence` |
| `Draining(ctx, started, error)` | 宿主报告错误，等待 idle 确认停止 |
| `IssuingAbort(ctx, turn, abortCtx)` | 正在向宿主发出 abort 请求 |
| `AwaitingAbortSettle` | abort 已被宿主接受，等待 idle 确认停止 |
| `ReconcilingAbortSettle` | idle 后查询 dispatch 状态以确保停止 |
| `Poisoned(reason)` | 中毒，后续 StartRun 被拒绝直至 SessionClosed |

### 关键命令（Command）

| 命令 | 触发时机 |
| :--- | :--- |
| `StartRun` | `SubsessionService.StartRun` |
| `DispatchAccepted` | 宿主 `session.prompt` resolve |
| `DispatchRejected` | 宿主拒绝或返回未知错误 |
| `TurnErrorObserved` | 宿主 event 报告错误 |
| `SessionIdleObserved` | 宿主 event 报告 idle |
| `EvidenceUpdated` | 宿主 event 报告 assistant 消息或 tool result |
| `CancelRequested` | AbortSignal 触发 |
| `TurnDeadlineExpired` | 5 分钟超时 |
| `AbortDeadlineExpired` | 1 分钟 abort 超时 |
| `AbortConfirmed` | 宿主确认停止 |
| `AbortHostAccepted` | 宿主接受 abort 请求 |
| `AbortRequestFailed` | 宿主 abort API 不可用 |
| `SessionClosed` | 物理 session 关闭 |

### 原子 BeginRun

`SubsessionActor.BeginRun` 在 `SerialQueue` 内原子执行：

1. 注册 `Deferred<RunResult>`（reply 占位）
2. `decide(state, StartRun)` 纯函数决策
3. 若决策成功 → `eventStore.Append`（NDJSON 持久化）
4. 若 append 失败 → fail-safe abort（`IssuingAbort`）
5. commit 新 state
6. 启动宿主效果（DispatchPrompt）
7. 返回 `Deferred.Promise`（等待终局通知）

### 恢复协议

`SubsessionReconcile.reconcileUnfinishedRuns` 在插件启动时执行：

1. `store.ReadAllEvents()` → `projectFromWanEvents` 构建 `SessionSafetyProjection`
2. 对每个 `ActiveRun`（RunStarted 无 RunFinished）：
   a. 持久化 `SessionPoisoned` + `TurnFinished` + `RunFinished`（crash-atomic 信封）
   b. `actor.MarkUnknownAfterRestart()` → 内存中毒
3. 设置 `SubsessionActorRegistry.SetSafetyProjection(proj)`

`SubsessionActorRegistry.GetOrCreate` 检查 `SafetyProjection`：若 session 曾持久中毒 → 直接以 `Poisoned` 状态创建 actor。

### 事件信封（Crash-Atomic Envelope）

为减少 NDJSON 行数并保证原子性，`SubsessionEventStore` 将一次决策的多个事件打包为：

```
{ kind: "subsession_decision_committed", payload: { events: JSON } }
```

`tryDecodeWanEventBatch` 解码时拆分：

- `subsession_decision_committed` → 解码 `events` JSON 数组 → 逐个映射
- 其他 `subsession_*` kind → 单事件解码
- 任意解码失败 → `SessionPoisoned(EventStoreCorrupt)`

### ISubsessionHost 接口（宿主需实现）

| 方法 | 返回 | 说明 |
| :--- | :--- | :--- |
| `Dispatch(sessionId, turn)` | `Result<HostStartReceipt, DispatchFailure>` | 发送 `session.prompt`，含 model 和 nonce |
| `Abort(sessionId, turnId)` | `AbortResult`（ConfirmedStopped / RequestAcceptedAwaitIdle / AbortUnavailable） | 终止运行 |
| `CancelPendingDispatch(turnId)` | unit | 取消正在等待的 dispatch |
| `QueryDispatchStatus(sessionId, turnId)` | `DispatchStatus` | 查询 nonce 是否已被宿主接受 |

### ISubsessionEventStore 接口

| 方法 | 说明 |
| :--- | :--- |
| `Append(sessionId, events)` | 原子追加事件列表；失败 = 基础设施故障 |

### 错误恢复优先级

`SubsessionDecision.decide` 中的 `afterError` 和 `afterTranscript` 使用 `FallbackPolicyState` 决策：

1. 可重试错误 → `RetryAt(i, count+1)` 继续
2. 不可重试 / 重试耗尽 → `Scanning(start, i)` 扫描链中后续模型
3. 扫描也耗尽 → `StopWithFailure(FallbackExhausted)`
4. 转录不完整（无 assistant 消息、空回复）→ `RecoverWithPrompt` 或 `ContinueNormally`（零宽字符）
5. 恢复超限 → `StopWithFailure(RecoveryExhausted)`

## SubagentDispatcher（多意图编排）

`src/Runtime/Subsession/SubagentDispatcher.fs` 统一处理 `coder` 和 `inspector` 工具的多意图并发：

- `dispatch(host, adapter, toolName, args, scope, registry)`：
   1. `decodeToolInvocation` → `CoderBatch` / `InspectorBatch` / `Typed`
   2. 多意图 → `promptsFromCoderIntents` / `promptsFromInspectorIntents` 生成并行 prompt
  3. `adapter.SpawnSubagent` 并行执行（`Promise.all`）
  4. `formatBatchReports` 合并结果
  5. `storeSubagentIterator` 写入 iterator 供后续 `continue` 使用
- `continue` 工具 → `consumeSubagentIterator` → `adapter.ContinueSubagent`

`IHostAdapter` 接口：
- `SpawnSubagent(request)` → `SubagentResponse`
- `ContinueSubagent(childID, agent, prompt)` → `SubagentResponse`
- `RegisterTempFiles` / `TryGetTempFiles` — 跨 prompt 暂存文件路径

## Continue 多轮（`continue` 工具）

**问题**：spawn 类工具原先 fire-and-forget，子会话存活时外层无法追问。

**Iterator**：

- id 形如 `sci_s:<childID>:<agent>:<host>`（自包含迭代器，不依赖内存存储）
- 绑定 `{ childID; agent; host }`
- 存储：`src/Runtime/Subsession/SubagentIteratorStore.fs`（scope 内状态，容量策略以实现为准）
- scope 清理时 `clearTypedIteratorScope` 一并回收

**首次 spawn**：子代理返回后注册 iterator；工具输出 YAML front matter 含 `iterator`（`ToolOutputInfo.withIterator`）。

**continue 调用**：

| 参数 | 必填 |
| :--- | :--- |
| `iterator` | 上一步返回的 id |
| `prompt` | 追问内容 |

流程：`consumeSubagentIterator`（解析自包含 id）→ 宿主 `ContinueSubagent(childID, agent, prompt)` → 成功则 **新** iterator 写回输出。

## 各宿主 Spawn

| 宿主 | 路径 |
| :--- | :--- |
| OpenCode | `SubagentIo.continueSubagentCoreResult`、`SubagentTools.fs` |
| Mux | `src/Hosts/Mux/SubagentTools.fs`、`Delegate.fs` |
| OMP | `src/Hosts/Omp/SubagentTools.fs`、`ChildSession.fs` |

共用：`src/Runtime/Subsession/SubagentPromptBuild.fs`、`SubagentIntentsCodec.fs`。**OMP 禁止**引用 OpenCode/Mux 宿主实现。

## 事件溯源（子代理）

成功 spawn / continue 会 append（父 session）：

| kind | 时机 |
| :--- | :--- |
| `subagent_spawned` | 子代理首次委派成功 |
| `subagent_continued` | `continue` 续跑成功 |

Fold：`src/Kernel/EventSourcing/Fold.fs` → Subsession projection；与 iterator 内存态互补，重启后可从 NDJSON 重建子代理投影（见 [05-event-sourcing.md](./05-event-sourcing.md)）。

## Reviewer

`submit_review` 拉起 reviewer；仅 `return_reviewer` + read 类工具。

## 测试

`SubagentIteratorStoreTests`、`IntegrationSubagentSpecs`、宿主 E2E 测试、`SubsessionActorTests`。

## 恢复错误与子会话完成协议

`session.prompt` 网络错误（`ECONNREFUSED`、`network connection lost` 等）是**可恢复中间事实**，不等于子会话终态。Fallback 系统可能已启动重试（`FallbackPhase.Retrying`），但子会话本身可能已完成工作（`TaskComplete=true`）。

**完成协议**：父工具（`continue`、`coder`、`inspector` 等）返回必须发生在终态之后：
- `TaskComplete=true`（子会话明确完成）
- `FallbackPhase.Exhausted`（重试链耗尽）
- 明确不可恢复失败（如 `MessageAborted`、`ClientCancellation`）

**等待门**：`waitForSubagentSettle` 在 `FallbackPhase.Retrying` 时保持等待，**但当 `TaskComplete=true` 时必须释放**。终态事实覆盖残留相位——相位是过程，终态是结果。

绝对不能无限期超时挂起。

## REF 架构演进方向

### 三部件拆分（CommandProcessor + EffectSupervisor + ResourceScope）

REF 架构提出了当前 `SubsessionActor` 的外壳拆分方向，保留其事务内核：

1. **CommandProcessor（CommandProcessor）**：极薄的串行提交器，执行固定十步：
   Dequeue → Validate → Decide → Persist(Domain + Outbox) → Commit → Reconcile Resources → Committed Handlers → Publish → Wake Supervisors → Next Command
2. **EffectSupervisor（效应监督器）**：流式监督器，从持久化 Outbox 消费 Effect，支持 at-least-once 语义与幂等操作。结果 enqueue Command 回 Inbox，绝不同步或递归调用 Handle（**No Reentrancy Law**）。
3. **ResourceScope（资源作用域）**：RAII 管理器，`CommittedState → ResourceSpec` 投影驱动。

首要约束：继续使用同一个 `Kernel/Subsession/Decision.decide` 纯函数，不同时重写领域状态机和运行时。

### 资源分类应用于子会话

|资源|类别|稳定 Key|说明|
|:---|:---|:---|:---|
|Turn Deadline|Durable|`TurnDeadline(turnId)`|持久化绝对到期时间，重启后恢复剩余时间|
|Abort Deadline|Durable|`AbortDeadline(turnId, abortAttemptId)`|避免重启后重新获得完整超时|
|Reconciliation Deadline|Durable|`ReconciliationDeadline(turnId)`|查询 dispatch 状态的超时|
|CallerReplyLease|Invocation|—|属于 Run Scope，重启后不恢复|
|AbortSignal|Invocation|—|属于调用栈，不塞入领域状态|

### 应用九条语义法律

|法律|应用于 Subsession Actor|
|:---|:---|
|顺序法律|已在 SerialQueue 中实现|
|持久化法律|`subsession_decision_committed` 事件信封保证|
|取消法律|`CancelRequested` 仅停止本地等待，远端 Abort 由状态机协议驱动|
|背压法律|Command 不可丢弃；Evidence 可 Latest-wins|
|单次枚举|Effect Process 默认单次拥有、单次运行|
|无重入|Effect 完成后 enqueue Command，不直接调用 processor|
|重放确定性|状态完全来自持久事件，不依赖内存 callback|
|关闭法律|`Poisoned` → `SessionClosed` 区分 StopAccepting/Drain/Dispose|
|幂等|CommandId 去重、`subsession_decision_committed` 信封幂等|

## 相关

- [08-tools-and-permissions.md](./08-tools-and-permissions.md)
- [06-review-and-nudge.md](./06-review-and-nudge.md)
- [12-fallback.md](./12-fallback.md) § 子会话 Fallback 路由
- [05-event-sourcing.md](./05-event-sourcing.md) § 子会话事件
