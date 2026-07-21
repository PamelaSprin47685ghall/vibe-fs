# 12 — 模型降级 (Fallback)

## 动机

上游 401/402/403、429、5xx 或断连时，内环 Fallback 拦截错误、按链切换模型并 `continue` 探测；链耗尽才向父 agent 传播（`Consumed=false`）。子代理（SubsessionActor）错误路由到子会话 Fallback 桥，不污染主 session。

## 两层架构

### Kernel 纯规则（`FallbackKernel/`）

| 模块 | 职责 |
| :--- | :--- |
| `Types.fs` | `SessionFallbackState`（`Idle`/`Retrying`/`Scanning`/`ScanningToolCallText`/`RecoveringToolCallText`/`Exhausted`）、`LeaseStatus`（`Requested`/`DispatchStarted`/`AcceptanceUnknown`/`Dispatched`/`Running`/`Cancelled`/`Settled`）、`FallbackModel`、`FallbackChain`、`SessionOwner` 等 |
| `Decision.fs` | `classifyError`：Abort→Ignore、401-403→ImmediateFallback、429/5xx→RetrySame、Exhausted |
| `Recovery.fs` | 完美平方启发式：`isPerfectSquare`、`scanStartIndex`、`selectModel`、`updateFailureCount` |
| `StateMachine.fs` | `transition(state, evt, cfg, chain)` → `(newState, action)` |

### Runtime 运行时（`Fallback/`）

| 模块 | 职责 |
| :--- | :--- |
| `Coordinator.fs` | `handleFallbackTransition`：接收标准化事件 → 驱动 FSM → 执行动作 |
| `FallbackCoordination.fs` | `resolveChain`、`sendOrContinue`、`handleRetryingError` |
| `RuntimeStore.fs` | `FallbackRuntimeStore`：单 map + change listeners |
| `SessionRuntime.fs` | `FallbackSessionRuntime` 记录（含 `ActiveGates` 门闩） |
| `GateState.fs` | 四类门闩：`NudgeActive`、`EventHandlingActive`、`MainContinuationAwaitingStart`、`Inactive` |
| `FallbackConfigCodec.fs` | 从 `AGENTS.md` frontmatter `models:` 解析链；`resolveSubagentChain` / `resolveModelDirective` |
| `FallbackMessageCodec.fs` | `decodeModelFromObj`、`scanToolCallAsText`、`allTodosCompleted`、`isIdleNoContentAndNoTools` |
| `ContinuationExecution.fs` / `ContinuationExecutionCore.fs` | `executeSendContinue` 唯一物理路径 |
| `ContinuationIntentExecution.fs` | Outbox Intent 消费驱动 |
| `LeaseValidation.fs` / `LeaseValidationRules.fs` | 租约校验 |
| `FallbackSubagentGate.fs` | `needFallbackContinue`、`isSubagentSettledFromObservation` |

## 唯一物理续命路径（F-05）

```
IActionExecutor.SendContinue（唯一物理调用点）
  → SessionDispatcher.Dispatch
    → client.session.prompt
      → recordHostAcceptedContinuation（唯一 Dispatched 写入入口）
```

已删除且禁止复活：`ContinuationHost`、`ContinuationCommandProcessor`、`ContinuationSupervisor`。

## Consumed 路由

| 事件类型 | 消费条件 | 含义 |
| :--- | :--- | :--- |
| SessionError | 非 Exhausted | 内环自愈，外环不感知 |
| SessionIdle | ScanningToolCallText / RecoveringToolCallText | 内环正在恢复 |
| SessionBusy | Retrying / Scanning | 内环正在重试 |
| Exhausted / 其他 | — | `Consumed=false`，外环可见 |

## 双重门闩

| 门闩 | 设置时机 | 清除时机 |
| :--- | :--- | :--- |
| `EventHandlingActive` | 宿主事件处理开始 | finally 块 |
| `MainContinuationAwaitingStart` | `SendContinue` 决策后 | 收到 Busy/Idle/Error/AssistantMessage |

## 空输出 Idle

最后 assistant 无 tool、text 为空 → `EmptyOutputError` → `SessionError`（非 `SessionIdle`）→ 触发 fallback continue + 短路 nudge。

## 扫描工具调用文本

`ScanningToolCallText` → `RecoveringToolCallText`：检测消息历史中最后一次 assistant 文本是否含 XML/JSON 工具调用模式（`<tool_call>`、`<function>`、`<invoke>`、`<edit>`）。

## 子会话 Fallback 路由

子代理错误经 `SubsessionEventRouter` 路由到 `SubsessionActor`；内部使用 `FallbackConfig` + `ModelDirective`；生命周期事件用 `subsession_*` kind。

## 配置（AGENTS.md frontmatter）

```yaml
models:
  default: ["openai/gpt-4o", ...]
  agents:
    sisyphus: ["openai/gpt-4o-mini"]
```

子代理链解析优先级：`AGENTS.md agents.<name>` → 运行时子 session 链 → 运行时父 session 链 → 父 session live model → 空链 → `DelegateToHost`。
