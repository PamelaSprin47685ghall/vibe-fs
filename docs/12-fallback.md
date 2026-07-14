# 12 — 模型降级 (Fallback)

## 动机

上游 401/402/403、429、5xx 或断连时，**内环** Fallback 拦截错误、按链切换模型并 `continue` 探测；链耗尽才向父 agent 传播（`Consumed = false`）。与 **外环** nudge/review 正交；Fallback 优先消费错误。子代理（SubsessionActor）的错误路由到子会话的 Fallback 桥，不污染主 session 状态。

## 两层架构

### Kernel 纯规则（`Kernel/FallbackKernel/`）

| 模块 | 职责 |
| :--- | :--- |
| `Types.fs` | `FallbackModel`、`FallbackChain`、`FallbackConfig`、`ErrorInput`、`ErrorClass`、`FallbackPhase`、`FallbackAction`、`FallbackLifecycle`、`SessionFallbackState`、`FallbackEvent` |
| `Decision.fs` | `classifyError`：Abort→Ignore、401-403→ImmediateFallback、429/5xx→RetrySame、Exhausted |
| `Recovery.fs` | 完美平方启发式：`isPerfectSquare`、`scanStartIndex`、`selectModel`、`updateFailureCount` |
| `StateMachine.fs` | `transition(state, evt, cfg, chain)` → `(newState, action)`。处理 Idle/Retrying/Scanning/ScanningToolCallText/RecoveringToolCallText/Exhausted 各阶段 |

### Shell 运行时（`Shell/FallbackRuntime*`）

| 模块 | 职责 |
| :--- | :--- |
| `FallbackRuntimeState.fs` | 每次 session 的 fallback 可变状态：Phase、Chain、Model、AgentName、BusyCount、Consumed、ActiveGates、InjectedModel/At、SessionOwner、PendingLease、PendingNudgeLease、SessionGeneration、CancelGeneration、ActiveContinuationGen/CancelGen、forceStopped、compacted 等 |
| `FallbackRuntimeStateGates.fs` | 门闩标志位操作：`setGateActive`、`isGateActive`、`removeSessionGates`。四类门闩：`NudgeActive`、`EventHandlingActive`、`MainContinuationAwaitingStart`、`Inactive` |
| `FallbackEventBridge.fs` | 核心编排器（~1062 行）：`handleEvent` 接收宿主原始事件 → 翻译为 `FallbackEvent` → 驱动 FSM → 产生 `ContinuationIntent` → `executeContinuationIntent` 执行。`createHandler` 工厂为每个 session 创建独立 `SerialQueue` |
| `FallbackMessageCodec.fs` | `decodeModelFromObj`、`scanToolCallAsText`、`allTodosCompleted`、`isIdleNoContentAndNoTools`、`tryGetLastAssistantAbortInfo`、`tryGetLatestUserModel`、`isLastAssistantToolFinish`、`hasToolResultAfter` |
| `FallbackMessageParser.fs` | `containsToolCallAsText`：检测 XML/JSON 格式的 tool call 文本（未使用函数调用协议） |
| `FallbackConfigCodec.fs` | 从 `AGENTS.md` frontmatter 解析 `models:` 节；`resolveSubagentChain`、`resolveModelDirective`（三态：HostConfigured→DelegateToHost、有链→RetryChain、空→DelegateToHost） |
| `FallbackRecoveryWait.fs` | `waitForRecovery`、`waitForToolCallTextRecovery`：基于 `OnStateChanged` 回调的事件驱动等待（非定时轮询） |
| `FallbackRuntimeLifecycle.fs` | `FallbackContinueMode`、`FallbackTaskCompletion`、`phaseForContinue`、`lifecycleForTask` |
| `FallbackRuntimeFlags.fs` | `FallbackConsumedStatus`（Unknown、ConsumedByHost、PropagatedToOuter）、`FallbackSessionGateFlag`（Inactive、NudgeActive、EventHandlingActive、MainContinuationAwaitingStart） |
| `FallbackSubagentGate.fs` | `FallbackGateObservation` → `needFallbackContinue`、`gateDemandFromObservation`、`isSubagentSettledFromObservation`；用于子会话 `waitForSubagentSettle` 决策 |
| `FallbackGateObservation.fs` | `observe(runtime, sessionID)`：从 `FallbackRuntimeState` 构建 `FallbackGateObservation` |

### 宿主桥接（各 `*/Fallback*`）

| 宿主 | 桥接器 | 探测器 |
| :--- | :--- | :--- |
| OpenCode | `Opencode/FallbackHooks.fs` → `createOpencodeFallbackHandler` | `opencodeEventTranslator` |
| Mux | `Mux/FallbackHooks.fs` → `createMuxFallbackHandler` | `muxEventTranslator` |
| OMP | `Omp/FallbackHooks.fs` → `createOmpFallbackHandler` | `ompEventTranslator` |

## 公理

1. **零定时器**：禁 `setTimeout`/`setInterval`/`Date.now`（架构测试）
2. **真实 continue 探测**：不靠 sleep/backoff
3. **配置**：`AGENTS.md` YAML `models:` 覆盖宿主默认

```yaml
models:
  default: [ "provider/model", ... ]
  agents:
    sisyphus: [ ... ]
```

## 完美平方启发式

- $k$ = `failureCount`；$N$ = 当前链索引
- $k \in \{1,4,9,\ldots\}$（完全平方）→ 扫描从索引 **0** 重试
- 否则从当前 $N$ 继续
- 新用户消息 → $k=0$

## ErrorClass（Kernel `Decision.fs` 优先级）

| 条件 | ErrorClass |
| :--- | :--- |
| Cancelled / TaskComplete | Ignore |
| Abort 错误名 | Ignore |
| 401/402/403 | ImmediateFallback |
| 显式不可重试 | ImmediateFallback |
| 重试耗尽 | Exhausted |
| 可重试 / 429/5xx | RetrySame |
| 其他（安全网） | RetrySame |

## 续命六阶段生命周期

每个 `SendContinue` / `RecoverWithPrompt` 经历完整生命周期并通过 NDJSON 事件持久化：

```
requested → dispatch_started → dispatched → [failed | cancelled | settled]
```

1. **`continuation_requested`**：FSM 决策后，`handleEvent` 构造 `PendingLease`，记录 `continuationId` / `model` / `sessionGeneration` / `humanTurnId` / `cancelGeneration` 等
2. **`continuation_dispatch_started`**：进入宿主 API 调用前（`appendContinuationDispatchStartedOrFail`）
3. 最终派发门闩：`TryTransitionPendingLease("requested" → "dispatch_started")` 原子验证
4. **`continuation_dispatched`**：`IActionExecutor.SendContinue` 成功后记录
5. 第二次门闩：`TryTransitionPendingLease("dispatch_started" → "dispatched")` 验证
6. **`continuation_failed`** / **`continuation_cancelled`** / **`continuation_settled`**：终局事件

每次续命调用前都执行 `verifyLease` 验证：`sessionGeneration` / `humanTurnId` / `cancelGeneration` / `owner == "Fallback"` / 非 forceStopped / lifecycle == Active / pendingLease match。

## Consumed 路由

`handleEvent` 返回 `Consumed: bool`，决定谁继续处理该事件：

| 事件类型 | 消费条件 | 含义 |
| :--- | :--- | :--- |
| SessionError | 非 Exhausted 阶段 | 内环自愈，外环不感知 |
| SessionIdle | ScanningToolCallText / RecoveringToolCallText | 内环正在恢复 |
| SessionBusy | Retrying / Scanning | 内环正在重试 |
| Exhausted / 其他 | — | `Consumed=false`，外环可见 |

`FallbackRuntimeState.SetConsumed(sessionID, value)` 持久化，`GetConsumed` 供 `FallbackSubagentGate` 读取。

## 双重门闩（Dual Gate Flags）

`FallbackRuntimeState` 维护两把独立门闩，防止并发访问冲突：

| 门闩 | 设置时机 | 清除时机 |
| :--- | :--- | :--- |
| `EventHandlingActive` | 宿主事件处理开始时 | 宿主事件处理完毕（finally 块） |
| `MainContinuationAwaitingStart` | `SendContinue`/`RecoverWithPrompt` 决策后 | 收到 `SessionBusy`/`SessionIdle`/`SessionError`/`AssistantMessage` 事件 |

两门闩在 `FallbackSubagentGate.needFallbackContinue` 中组合判断：任一激活 → 需要继续等待。

## 空输出 Idle

最后 assistant 无 tool、text 为空 → `EmptyOutputError`：`FallbackEventBridge` 在 `SessionIdle` 时调用 `isIdleNoContentAndNoTools` 检测，构造 `FallbackEvent.SessionError` 而非 `SessionIdle`，触发 fallback continue，**同时阻止 nudge**（见 [10](./10-message-transform.md)）。

## 扫描工具调用文本（Tool-Call-as-Text Recovery）

`FallbackPhase.ScanningToolCallText` → `RecoveringToolCallText`：

1. `SessionIdle` 时 FSM 进入 `ScanningToolCallText`
2. `ScanToolCallAsText` 动作：`fallbackMessageCodec.scanToolCallAsText` 检测消息历史中最后一次 assistant 文本是否包含 XML/JSON 工具调用模式（`<tool_call>`、`<function>`、`<invoke>`、`<edit>` 等）
3. 检测到 → `RecoverWithPrompt(recoveryPrompt)` 注入恢复提示
4. 未检测到 → 检查 `isLastAssistantToolFinish` + `hasToolResultAfter` → 若工具已调用（`TaskComplete`）则正常结束，否则继续

## 注入事件 SSOT

`FallbackEventBridge.handleEvent` 先 `appendContinuationRequestedOrFail`（NDJSON 持久化），再 `TryTransitionPendingLease` 原子验证，最后 `IActionExecutor.SendContinue` 执行。

消费端（`resolveNudgeModel` / `tryGetLatestUserModel` / 哨兵 `IsNewUserMessage`）**不**嗅探消息文本，**只**读 `runtime.GetInjectedModel` + `runtime.IsInjectedSince` 内存投影（由事件 fold 回填）。重启时 `EventLogStore.ReadAllEvents` → `foldFallbackInjection` + `ownerAndLeaseFolder` 重建 `SessionState.FallbackInjection` 和 `PendingLease`。

## IEventTranslator 接口（宿主需实现）

| 方法 | 返回 |
| :--- | :--- |
| `TranslateError` | `FallbackEvent option` |
| `ExtractSessionID` | string |
| `IsSessionError` / `IsSessionBusy` / `IsSessionIdle` | bool |
| `IsNewUserMessage` | bool（含 `IsInjectedSince` 过滤） |
| `ExtractContinuationIdentity` | `(string * int) option` — 续命 ID + ordinal |
| `ExtractHostRunId` | `string option` — 用于过期检测 |
| `ExtractTurnObservation` | `TurnObservation option` — 子会话观测 |
| 其他提取方法 | 模型/agent/消息 ID 等 |

## IActionExecutor 接口（宿主需实现）

| 方法 | 用途 |
| :--- | :--- |
| `SendContinue(sessionID, model, continuationID)` | 注入零宽 U+200B 文本，触发续命 |
| `RecoverWithPrompt(sessionID, model, promptText, continuationID)` | 注入恢复提示文本 |
| `FetchMessages(sessionID)` | 获取全量消息，用于 `ScanToolCallAsText` / `allTodosCompleted` / `tryGetLastAssistantAbortInfo` |
| `PropagateFailure(sessionID)` | 外环可见失败 |
| `CaptureCurrentModel(sessionID)` | 获取当前模型 |
| `AbortRun(sessionID)` | 终止当前运行 |

## 子会话 Fallback 路由

子代理（Coder、Investigator 等）通过 `SubsessionActor` 运行，其 Fallback 处理方式不同：

- 子会话的错误事件不进入主 session 的 `FallbackEventBridge`，而是通过 `SubsessionEventRouter` 路由到子 `SubsessionActor`
- `SubsessionActor` 内部使用 `SubsessionService.StartRun` 启动，接受 `FallbackConfig` 和 `ModelDirective`
- 子会话的降级策略复用 `FallbackConfigCodec.resolveSubagentChain` 和 `resolveModelDirective`
- 子会话生命周期的 NDJSON 事件使用 `subsession_*` kind（见 [05](./05-event-sourcing.md)）

## 配置

`AGENTS.md` frontmatter `models:` 节：

```yaml
---
models:
  default: ["openai/gpt-4o", "anthropic/claude-3.5-sonnet"]
  agents:
    explorer: ["openai/gpt-4o-mini"]
---
```

### 子代理链解析优先级

`resolveSubagentChain` 按优先级选择：

1. `AGENTS.md` 中 `models.agents.<name>` 配置 → 非空则使用
2. 运行时子 session 链（`runtime.GetChain(childID)`）
3. 运行时父 session 链（`runtime.GetChain(parentSessionID)`）
4. 父 session 当前 live model（作为单例链）
5. 空链 → `DelegateToHost`（不拒绝，避免假阴性）

### ModelDirective 三态

`resolveModelDirective` 决定子代理模型选择权：

| 条件 | 结果 |
| :--- | :--- |
| 宿主已显式配置 agent 模型（opencode.jsonc） | `DelegateToHost` |
| 链非空 | `RetryChain chain` |
| 链为空 | `DelegateToHost` |

## 模块总表

| 层 | 路径 | 核心类型 |
| :--- | :--- | :--- |
| FSM | `Kernel/FallbackKernel/` | `SessionFallbackState`、`FallbackAction`、`FallbackPhase` |
| 续命事件 fold | `Kernel/EventLog/FallbackInjectionFold.fs` | `FallbackInjectionState`、`foldFallbackInjection` |
| 续命事件 fold | `Kernel/EventLog/Fold.fs` | `ownerAndLeaseFolder`、`EpisodeStage` |
| 续命事件 fold | `Kernel/EventLog/Types.fs` | `eventKindContinuationRequested` 等 6 种 |
| 运行时 | `Shell/FallbackRuntimeState` | `FallbackRuntimeState`、`PendingLease`、`NudgeLease` |
| 运行时桥 | `Shell/FallbackEventBridge` | `handleEvent`、`createHandler`、`verifyLease`、`executeContinuationIntent` |
| 运行时门闩 | `Shell/FallbackRuntimeStateGates` | `setGateActive`、`isGateActive` |
| 运行时消息 | `Shell/FallbackMessageCodec` | `decodeModelFromObj`、`scanToolCallAsText`、`isIdleNoContentAndNoTools` |
| 运行时消息解析 | `Shell/FallbackMessageParser` | `containsToolCallAsText` |
| 运行时配置 | `Shell/FallbackConfigCodec` | `loadFallbackConfig`、`resolveSubagentChain`、`resolveModelDirective` |
| 运行时等待 | `Shell/FallbackRecoveryWait` | `waitForRecovery`、`waitForToolCallTextRecovery` |
| 运行时观测 | `Shell/FallbackGateObservation` | `observe` |
| 运行时门闩决策 | `Kernel/FallbackSubagentGate` | `needFallbackContinue`、`isSubagentSettledFromObservation` |
| 运行时生命周期 | `Kernel/FallbackRuntimeLifecycle` | `FallbackContinueMode`、`FallbackTaskCompletion` |
| 运行时标志 | `Kernel/FallbackRuntimeFlags` | `FallbackConsumedStatus`、`FallbackSessionGateFlag` |

## 测试

`FallbackKernelTests`、`FallbackConfigCodecTests`、`FallbackIntegrationTests`、`FallbackEventBridgeTests`、`FallbackSubagentGateTests`。

## 相关

- [05-event-sourcing.md](./05-event-sourcing.md) § 续命事件
- [10-message-transform.md](./10-message-transform.md) § 空输出处理
- [11-subagents.md](./11-subagents.md) § 子会话 Actor 与 Fallback 协作
- [04-shell.md](./04-shell.md)