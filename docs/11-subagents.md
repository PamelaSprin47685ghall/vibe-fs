# 11 — 子代理与子会话 Actor

## 模型

子代理 = 独立会话（OMP 可为子 workspace）。委派工具：`coder`、`inspector`、`browser`、`meditator` 等；参数 `intents[]` 可多项并发。

子代理由 `src/Runtime/Subsession/SubagentDispatcher.fs` 统一编排；若宿主适配器选择 Actor 路径，则进入 `SubsessionActor`。文档不把两者宣称为全局"旧/新生产路径"，实际入口以宿主装配和调用链为准。

## SubsessionActor 子系统

### 架构

```
父工具（coder/inspector 等）
  → SubagentDispatcher.dispatch
    → IHostAdapter.SpawnSubagent
      → SubsessionService.StartRun
        → SubsessionActorRegistry.GetOrCreate
          → SubsessionActor.BeginRun（原子：注册 reply → 决策 → 事件持久化）
            → Kernel/Subsession/Decision.decide（纯函数）
              → ISubsessionHost.Dispatch
                → session.prompt
                  → 宿主回调 → SubsessionActor.Post(Command) → 再次决策 → 循环直至终局
```

### 三部件复合

`SubsessionActor`（`SubsessionActor.fs`）为薄 wiring layer，复合：

| 部件 | 职责 |
| :--- | :--- |
| `CommandProcessor` | 10 步串行提交管线（Dequeue → Validate → Decide → Persist → Commit → Reconcile Resources → Committed Handlers → Publish → Wake Supervisors → Next） |
| `EffectSupervisor` | 宿主 effect 分发（fire-and-forget）；timer expiry 重入 |
| `ResourceScope` | RAII JS 定时器管理（`TurnDeadline`/`AbortDeadline`/`ReconciliationDeadline`） |

### SubsessionState 状态机（9 种状态）

```
Available → Dispatching → Running → Draining → IssuingAbort → AwaitingAbortSettle → ReconcilingAbortSettle → Poisoned
         → CancellingDispatch → ReconcilingUnknownDispatch
```

| 状态 | 含义 |
| :--- | :--- |
| `Available` | 空闲，可接受 StartRun |
| `Dispatching` | 已向宿主发起 `session.prompt` |
| `CancellingDispatch` | 取消请求已发出 |
| `ReconcilingUnknownDispatch` | 宿主接受状态未知，主动查询 |
| `Running` | 正在运行，收集 `CurrentTurnEvidence` |
| `Draining` | 宿主报告错误，等待 idle |
| `IssuingAbort` | 正在发出 abort 请求 |
| `AwaitingAbortSettle` / `ReconcilingAbortSettle` | abort 后等待确认 |
| `Poisoned` | 中毒，StartRun 被拒绝直至 SessionClosed |

### 关键命令（`Command.fs`，28 种）

`StartRun`、`DispatchAccepted`、`DispatchRejected`、`TurnErrorObserved`、`SessionIdleObserved`、`EvidenceUpdated`、`CancelRequested`、`TurnDeadlineExpired`、`AbortDeadlineExpired`、`AbortConfirmed`、`AbortHostAccepted`、`AbortRequestFailed`、`SessionClosed` 等。

### 原子 BeginRun

1. 注册 `Deferred<RunResult>`（reply 占位）
2. `decide(state, StartRun)` 纯函数决策
3. append 成功 → commit 新 state → 启动宿主效果
4. append 失败 → fail-safe abort（`IssuingAbort`）

### 恢复协议

`SubsessionReconcile.reconcileUnfinishedRuns`：
1. `ReadAllEvents` → `SessionSafetyProjection`
2. 每个 `ActiveRun` → 原子持久化 `SessionPoisoned + TurnFinished + RunFinished`
3. `SubsessionActorRegistry.SetSafetyProjection(proj)`
4. `GetOrCreate` 检查 SafetyProjection → 持久中毒 → 直接以 `Poisoned` 创建

### SubsessionActorRegistry

`src/Runtime/Subsession/SubsessionActorRegistry.fs`：`(workspaceRoot, sessionId)` 键映射 → `SubsessionActor`；`SetSafetyProjection` 按 workspace root 隔离安全投影；`ClearPoison`/`Remove` 清理；`RegisterGlobalCleanup` 全局清理回调。

### SubagentDispatcher 多意图编排

`src/Runtime/Subsession/SubagentDispatcher.fs`：
- `dispatch` → `decodeToolInvocation` → `promptsFromCoderIntents` → `adapter.SpawnSubagent`（`Promise.all` 并行）→ `formatBatchReports`
- `continue` → `consumeSubagentIterator` → `adapter.ContinueSubagent`

### Continue 多轮

Iterator id：`sci_s:<childID>:<agent>:<host>`，自包含不依赖内存存储。`consumeSubagentIterator` 解析后调用宿主 `ContinueSubagent`，成功则新 iterator 写回输出。

### 事件信封（Crash-Atomic）

`subsession_decision_committed` 含 `events` JSON 数组；`tryDecodeWanEventBatch` 拆分。任意解码失败 → `SessionPoisoned(EventStoreCorrupt)`。

### 子代理 durable 投影

`subagent_spawned` / `subagent_continued` 事件 → `foldSubagents` → `SessionState.Subagents`（`Map<string, SubagentState>`）；重启后可从 NDJSON 重建。

## Reviewer

`submit_review` 拉起 reviewer；`return_reviewer` + read 类工具。
