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
