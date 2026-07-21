# 06 — With-Review 与 Nudge

## With-Review（/loop）

**目标**：开发过程中嵌入独立 reviewer 子代理，形成「实现 → submit_review → verdict → 可能修订」闭环。

### 状态机 SSOT

- 类型与转移：`src/Kernel/ReviewSession/StateMachine.fs`
- 5 状态 DU：`Inactive | Active of task | Locked of task * reviewerId | Accepted | NeedsRevision of feedback`
- 命令：`Activate`、`Submit`、`Lock`、`Unlock`、`Accept`、`RequestRevision`
- Durable task 字符串：`foldReviewTask` + `loop_activated` / `review_verdict` / `loop_cancelled` 事件

内存 `ReviewStore` = 事件投影缓存；**loop 是否活跃**以 NDJSON fold 为准（架构测试禁止 nudge 直读 store 捷径）。

### 典型转移

| 从 | 命令/事件 | 到 |
| :--- | :--- | :--- |
| Inactive | Activate(task) | Active(task) → `loop_activated` |
| Active | submit_review (wip) | Active → `submit_review_wip_recorded` |
| Active | Lock(reviewer) | Locked |
| * | verdict accepted | Accepted → task 清空 |
| * | verdict needs_revision | NeedsRevision(feedback) |

精确表以 `StateMachine.fs` 为准。

### 工具

- **`submit_review`**：worker 提交报告；可 `wip: true` 记录部分进度。
- **`return_reviewer`**：reviewer 子代理返回 verdict（与 `ReviewVerdict` Kernel 类型对齐）。

### Reviewer 轮次与 nudge 上限

`decideAfterRound`：无结果时可能触发 nudge，超过 `maxNudges` 则终止 loop（`Terminated`）。

## Nudge 子系统

### 三层架构

| 层 | 位置 | 职责 |
| :--- | :--- | :--- |
| 纯决策 | `src/Kernel/Nudge/` | 给定 `SessionSnapshot`，是否应 nudge、哪种 action |
| 运行时 | `src/Runtime/Nudge/` + `EventStore/NudgeEventWriter.fs` | 锁、去重、发送、错误与 abort |
| 宿主 | `src/Hosts/*/Nudge*` | 事件翻译、`sendNudge` 实现 |

**禁止**在 nudge 入口用内存布尔代替事件 fold 的 loop 态（`ompNudgeHooksDoNotReadReviewStoreForLoopState` 架构测试）。

### 决策路径

`deriveAction`（`NudgeDerivation.fs`）从 `SessionSnapshot` 推导 action：

```
Blocked → NudgeNone
Idle → NudgeNone
RunnerOnly → NudgeRunner
RunnerWithLoop → skipsReview ? NudgeNone : NudgeLoop
RunnerOnly → NudgeRunner
LoopWithTodos → skipsTodo ? (skipsReview ? NudgeNone : NudgeLoop) : NudgeTodo
LoopIdle → skipsReview ? NudgeNone : NudgeLoop
RunnerWithLoop → skipsReview ? NudgeNone : NudgeLoop
```

### 七轴工作状态（`Nudge/Types.fs`）

`SessionWorkState` = `Idle | TodosOnly | LoopIdle | LoopWithTodos | RunnerOnly | RunnerWithTodos | RunnerWithLoop | AllAxes`，从 `(hasActiveRunner, isLoopActive, openTodos)` 三轴穷举派生。

### Nudge 运行时核心（`NudgeFlow.fs`）

```
runNudgeFlowCore:
1. takeSnapshot → SessionSnapshot option
2. deriveAction → NudgeAction
3. selectNudgePrompt → promptText
4. tryClaimNudgeDispatch → 串行锁内追加 nudge_requested
5. claim 成功 → 设置 PendingNudgeLease / SessionOwner=Nudge / ActiveNudgeNonce
6. sendNudge → 成功 → nudge_dispatched；失败 → nudge_failed/cancelled/settled
```

### 去重

`NudgeDedupState`（`NudgeProjection.fs`）= `{ PendingNudge: (anchor, nudgeId) option; LastDispatchedAnchor: string option }`。

清除时机：`nudge_dedup_cleared`、`submit_review_wip_recorded`、`human_turn_started` 事件。

### 宿主实现

| 宿主 | 路径 |
| :--- | :--- |
| OpenCode | `src/Hosts/OpenCode/NudgeEffect.fs`、`NudgeTrigger.fs` |
| Mux | `src/Runtime/Nudge/NudgeRuntimeMux.fs` |
| OMP | `src/Hosts/Omp/NudgeHooks.fs` |

### 决策优先级

1. 有 open todos → `nudge-todo`
2. 子代理 / runner 活跃 → `nudge-runner`
3. review loop 活跃（**事件 fold**）→ `nudge-loop`
4. 否则 → none

### Nudge 生命周期事件

| 事件 | 时机 | 核心 payload |
| :--- | :--- | :--- |
| `nudge_requested` | Claim 成功 | `action`/`anchor`/`nudgeId`/`nonce`/`generation`/`cancelGeneration`/`humanTurnId`/`nudgeOrdinal` |
| `nudge_dispatched` | `session.prompt` 成功 | `action`/`anchor`/`nudgeId`/`nudgeOrdinal` |
| `nudge_failed` | 发送失败 | `nudgeId`/`error`/`nudgeOrdinal` |
| `nudge_cancelled` | 被新用户消息打断 | `nudgeId`/`reason`/`nudgeOrdinal` |
| `nudge_settled` | idle 确认完成 | `nudgeId`/`status`/`nudgeOrdinal` |

## 源码索引

| 主题 | 路径 |
| :--- | :--- |
| FSM | `Kernel/ReviewSession/StateMachine.fs` |
| Nudge 决策 | `Kernel/Nudge/NudgeDerivation.fs`（Runtime）、`Nudge.fs`（Kernel）、`Nudge/Types.fs` |
| Nudge 投影 | `Kernel/Nudge/NudgeProjection.fs`、`NudgeSnapshotProjection.fs` |
| Nudge 运行时 | `src/Runtime/Nudge/NudgeFlow.fs`、`NudgeDispatchClaim.fs` |
| Event append | `src/Runtime/EventStore/NudgeEventWriter.fs` |
| Event fold | `src/Kernel/EventSourcing/Fold.fs` |
| OpenCode nudge | `src/Hosts/OpenCode/NudgeEffect.fs`、`NudgeTrigger.fs` |
| Mux nudge | `src/Runtime/Nudge/NudgeRuntimeMux.fs` |
| OMP nudge | `src/Hosts/Omp/NudgeHooks.fs` |
