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

### Runtime 运行时（`src/Runtime/Fallback/`）

| 模块 | 职责 |
| :--- | :--- |
| `RuntimeStore.fs`、`SessionRuntime.fs` | 每次 session 的 fallback 可变状态与原子更新 |
| `GateState.fs` | 门闩标志位操作与 session gate 状态。四类门闩：`NudgeActive`、`EventHandlingActive`、`MainContinuationAwaitingStart`、`Inactive` |
| `Coordinator.fs`、`FallbackCoordination.fs` | `handleEvent` 接收标准化事件 → 驱动 FSM → 执行动作；每 session handler 通过 `SerialQueue` 排序相关事件 |
| `FallbackMessageCodec.fs` | `decodeModelFromObj`、`scanToolCallAsText`、`allTodosCompleted`、`isIdleNoContentAndNoTools`、`tryGetLastAssistantAbortInfo`、`tryGetLatestUserModel`、`isLastAssistantToolFinish`、`hasToolResultAfter` |
| `FallbackMessageParser.fs` | `containsToolCallAsText`：检测 XML/JSON 格式的 tool call 文本（未使用函数调用协议） |
| `FallbackConfigCodec.fs` | 从 `AGENTS.md` frontmatter 解析 `models:` 节；解析子代理链与模型 directive |
| `FallbackRecoveryWait.fs` | `waitForRecovery`、`waitForToolCallTextRecovery`：基于 `OnStateChanged` 回调的事件驱动等待（非定时轮询） |
| `src/Kernel/FallbackRuntimeLifecycle.fs` | `FallbackContinueMode`、`FallbackTaskCompletion`、`phaseForContinue`、`lifecycleForTask` |
| `src/Kernel/FallbackRuntimeFlags.fs` | `FallbackConsumedStatus`（Unknown、ConsumedByHost、PropagatedToOuter）、`FallbackSessionGateFlag`（Inactive、NudgeActive、EventHandlingActive、MainContinuationAwaitingStart） |
| `FallbackSubagentGate.fs` | `FallbackGateObservation` → `needFallbackContinue`、`gateDemandFromObservation`、`isSubagentSettledFromObservation`；用于子会话 `waitForSubagentSettle` 决策 |
| `FallbackGateObservation.fs` | `observe(runtime, sessionID)`：从 `FallbackRuntimeState` 构建 `FallbackGateObservation` |

### 宿主桥接（各 `*/Fallback*`）

| 宿主 | 桥接器 | 探测器 |
| :--- | :--- | :--- |
| OpenCode | `src/Hosts/OpenCode/Fallback/` | `EventTranslator.fs` |
| Mux | `src/Hosts/Mux/Fallback/` | 宿主事件翻译模块 |
| OMP | `src/Hosts/Omp/Fallback/` | 宿主事件翻译模块 |

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

## 续命生命周期（唯一路径）

权威路径图：[CONTINUATION_PATH.md](./CONTINUATION_PATH.md)。

```
requested → dispatch_started → dispatched → [failed | cancelled | settled]
```

1. **`continuation_requested`**：FSM 决策后构造 `PendingLease(Requested)`
2. **`continuation_dispatch_started`**：进入宿主 API 前；`Requested → DispatchStarted`
3. 物理发送：`IActionExecutor.SendContinue` →（OpenCode）`SessionDispatcher` → `session.prompt`
4. **`continuation_dispatched` / `Dispatched`**：仅 host evidence（`chat.message` / `HostReceiptWaiter`）经 `recordHostAcceptedContinuation`；**禁止** prompt Promise 返回即 Dispatched
5. **`continuation_failed` / `cancelled` / `settled`**：终局

每次续命调用前 `verifyLease`：generation / humanTurnId / cancelGeneration / owner / lifecycle / pendingLease。

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

最后 assistant 无 tool、text 为空 → `EmptyOutputError`：`Coordinator` 在 `SessionIdle` 时调用 `isIdleNoContentAndNoTools` 检测，构造 `FallbackEvent.SessionError` 而非 `SessionIdle`，触发 fallback continue，**同时阻止 nudge**（见 [10](./10-message-transform.md)）。

## 扫描工具调用文本（Tool-Call-as-Text Recovery）

`FallbackPhase.ScanningToolCallText` → `RecoveringToolCallText`：

1. `SessionIdle` 时 FSM 进入 `ScanningToolCallText`
2. `ScanToolCallAsText` 动作：`fallbackMessageCodec.scanToolCallAsText` 检测消息历史中最后一次 assistant 文本是否包含 XML/JSON 工具调用模式（`<tool_call>`、`<function>`、`<invoke>`、`<edit>` 等）
3. 检测到 → `RecoverWithPrompt(recoveryPrompt)` 注入恢复提示
4. 未检测到 → 检查 `isLastAssistantToolFinish` + `hasToolResultAfter` → 若工具已调用（`TaskComplete`）则正常结束，否则继续

## 注入事件 SSOT

生产唯一路径：Coordinator → ContinuationIntentExecution → `IActionExecutor.SendContinue`（OpenCode：`ActionExecutor` + `SessionDispatcher`）。`ContinuationHost` / `ContinuationCommandProcessor` / `ContinuationSupervisor` **已删除**，禁止复活（`ContinuationCleanupTests` + `ContinuationPathSsotTests`）。

消费端不嗅探消息文本；lease / generation / owner 来自 `FallbackRuntimeStore`。`InjectedAt` / `InjectedModel` 由 `recordHostAcceptedContinuation` 在 host accept 时写入。

OpenCode continuation payload 文本为 ZWSP `"\u200B"`（`ActionExecutor.sendContinueImpl`）。

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
| `SendContinue(sessionID, model, continuationID)` | 注入 U+200B 文本（`continuationPayload`），触发续命 |
| `RecoverWithPrompt(sessionID, model, promptText, continuationID)` | 注入恢复提示文本 |
| `FetchMessages(sessionID)` | 获取全量消息，用于 `ScanToolCallAsText` / `allTodosCompleted` / `tryGetLastAssistantAbortInfo` |
| `PropagateFailure(sessionID)` | 外环可见失败 |
| `CaptureCurrentModel(sessionID)` | 获取当前模型 |
| `AbortRun(sessionID)` | 终止当前运行 |

`SendContinue` 的 payload 文本由 `ContinuationHost` 中的 `continuationPayload` 常量（`"\u200B"`）提供，不再使用各 `ActionExecutor` 中的 `zwsChar` 私有定义。

## 子会话 Fallback 路由

子代理（Coder、Inspector 等）通过 `SubsessionActor` 运行，其 Fallback 处理方式不同：

- 子会话的错误事件不进入主 session 的 Fallback Coordinator，而是通过 `SubsessionEventRouter` 路由到子 `SubsessionActor`
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
| 事件 kind | `src/Kernel/EventSourcing/EventKind.fs` | v1/v2 continuation kind 常量 |
| 事件 fold | `src/Kernel/EventSourcing/Fold.fs` | session generation、episode、owner/lease 投影 |
| v2 续命类型 | `src/Kernel/Fallback/Continuation.fs` | request、state、command、event、effect |
| v2 续命决策 | `src/Kernel/Fallback/ContinuationDecision.fs` | `decide` 纯函数 |
| v2 续命投影 | `src/Kernel/Fallback/ContinuationProjection.fs` | projection 与事件映射 |
| v2 事件 codec | `src/Runtime/Fallback/ContinuationEventCodec.fs` | 事件编解码 |
| v2 命令处理器 | `src/Runtime/Fallback/ContinuationCommandProcessor.fs` | 串行提交与 effect 产生 |
| v2 监督器 | `src/Runtime/Fallback/ContinuationSupervisor.fs` | 宿主 effect 执行与 command 回流 |
| v2 宿主接口 | `src/Runtime/Fallback/ContinuationHost.fs` | `IContinuationHost` |
| 运行时编排 | `src/Runtime/Fallback/Coordinator.fs`、`FallbackCoordination.fs` | 当前 Fallback 事件入口与动作执行 |
| 运行时门闩决策 | `Kernel/FallbackSubagentGate` | `needFallbackContinue`、`isSubagentSettledFromObservation` |
| 运行时生命周期 | `Kernel/FallbackRuntimeLifecycle` | `FallbackContinueMode`、`FallbackTaskCompletion` |
| 运行时标志 | `Kernel/FallbackRuntimeFlags` | `FallbackConsumedStatus`、`FallbackSessionGateFlag` |

## 测试

Fallback Kernel、配置、集成与 Subagent Gate 测试；具体入口以 `tests/runner.js` 的已注册条目为准。

## REF 架构演进方向

### Effect Supervisor 整合

当前仍存在 v1 直接执行路径；v2 已提供 `ContinuationCommandProcessor` + `ContinuationSupervisor`。REF 方向要求宿主 effect 全部收敛到持久化意图与 command 回流：

1. FSM 决策后，Continuation Intent 作为持久化 Outbox 事件写入（与领域事件同批提交）
2. Effect Supervisor 从持久化存储消费该 Intent，而非仅凭内存通知
3. 调用宿主 API（`SendContinue` / `RecoverWithPrompt`）
4. 处理超时/重试，结果的终局事件映射为 Command 回流至 Inbox
5. 重启时 Supervisor 从尚未确认的 Intent 恢复，重新发起或查询宿主动作

### At-least-once + Idempotent

每个 continuation 具有稳定身份：

```text
ContinuationId
Attempt
IdempotencyKey
```

宿主适配器应支持幂等操作（同一 ContinuationId 的 `SendContinue` 多次调用只生效一次）。

### 资源身份与 Deadline 持久化

- **Stable Resource Identity Law**：`ContinuationDeadline(continuationId)` 使用稳定 Key，而非 State 引用相等。
- Deadline 使用**绝对到期时间**（DeadlineAt）持久化：重启后 `remaining = DeadlineAt - Clock.Now`，`remaining > 0` 建立剩余时间的 Timer，`remaining <= 0` 立即触发超时 Command。
- 当前 `FallbackRecoveryWait` 的 `OnStateChanged` 回调驱动等待不依赖定时器，与 REF 的 RAII Resource Scope 方向一致。

### 资源分类

|当前组件|REF 分类|管理方式|
|:---|:---|:---|
|Continuation Deadline|Durable Resource|CommittedState 投影 → ResourcePlan|
|Fallback config 缓存|Invocation Resource|Session scope 内缓存，重启后重读|

## 相关

- [05-event-sourcing.md](./05-event-sourcing.md) § 续命事件
- [10-message-transform.md](./10-message-transform.md) § 空输出处理
- [11-subagents.md](./11-subagents.md) § 子会话 Actor 与 Fallback 协作
- [04-runtime.md](./04-runtime.md)
