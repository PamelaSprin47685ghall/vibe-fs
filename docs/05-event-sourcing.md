# 05 — 事件溯源与持久化

## SSOT

工作区根目录：`.wanxiangshu.ndjson` + `.wanxiangshu.ndjson.lock`（`src/Runtime/EventStore/EventLogFile.fs`）。万象阵 `squad_*`/`task_*` 行与此文件同行序追加，物理共用同一文件与同一锁。

## 六条纪律

1. **意图不落盘**：未校验的自然语言、草稿参数不写入。
2. **事实不可改**：仅追加行；修正靠补偿事件，非覆盖旧行。
3. **内存 = fold**：`ReviewStore`、nudge 表 = 对 NDJSON 的纯 fold + 可选进程缓存。
4. **先盘后内存**：append 成功后才更新缓存投影；失败 = 命令未发生。
5. **一行一事件**：每行自包含 JSON（`WanEvent`）。
6. **按 session 分区**：每行含 `session` 字段；fold 按 `sessionId` 过滤。

## 事件种类（Kernel SSOT）

定义于 `src/Kernel/EventSourcing/EventKind.fs`（52 个常量）。Runtime codec 引用这些常量，宿主层不复制字符串。

### 核心业务事件

| Kind 常量 | 含义 |
| :--- | :--- |
| `assistant_completed` | 助手轮次完成（含 `agent`/`model`/`turnId`/`openTodosJson`） |
| `loop_activated` | With-Review 激活（payload 含 `task`） |
| `loop_cancelled` | 取消 loop |
| `review_verdict` | 审查结论（accepted / needs_revision / terminated / cancelled） |
| `submit_review_wip_recorded` | WIP 提交记录 |
| `submit_review_reports_consumed` | 审查报告被消费记录 |
| `subagent_spawned` | 子代理 spawn 成功 |
| `subagent_continued` | `continue` 续跑子会话 |
| `fallback_continue_injected` | Fallback 注入续命 |
| `route_observed` | 路由观察 |

### nudge 生命周期事件（六阶段闭环）

| Kind | 含义 | 时机 |
| :--- | :--- | :--- |
| `nudge_requested` | nudge 已进入串行 Claim 前 | claim 成功时 |
| `nudge_dispatched` | nudge 已成功派发 | `session.prompt` 成功 |
| `nudge_failed` | nudge 派发失败 | 发送失败 |
| `nudge_cancelled` | nudge 被取消 | 新用户消息打断 |
| `nudge_settled` | nudge 终局 | 后续 idle 确认完成 |
| `nudge_dedup_cleared` | 去重表清空 | 新消息 / WIP 提交 |
| `nudge_owner_unknown` | nudge 归属未知 | 归属无法确定 |

去重：`NudgeDedupState` = `{ PendingNudge: (anchor, nudgeId) option; LastDispatchedAnchor: string option }`。`isBlocked` 检查当前 anchor 是否已在 Pending 或 LastDispatched 中。

### 人机交互轮次事件

| Kind | 含义 |
| :--- | :--- |
| `human_turn_started` | 用户新消息开始（含 `turnId`/`provider`/`model`/`agent` 等） |
| `user_abort_observed` | 用户中断观察 |

### 模型降级（Fallback）续命事件

| Kind | 含义 |
| :--- | :--- |
| `continuation_requested` | FSM 决策后构造 `PendingLease(Requested)` |
| `continuation_dispatch_started` | 即将调用宿主 API |
| `continuation_dispatched` | 宿主已接受（`recordHostAcceptedContinuation` 唯一写入入口） |
| `continuation_failed` | 续命失败 |
| `continuation_cancelled` | 续命取消 |
| `continuation_settled` | 续命终局 |
| `continuation_dispatch_claimed` | Effect Supervisor 从 Outbox 消费 Dispatch 意图 |
| `continuation_host_accepted` | 宿主接受续命 |
| `continuation_idle_reconciliation` | Idle 时协调 |
| `continuation_run_started` | 续命运行开始 |
| `continuation_superseded` | 续命被取代 |
| `continuation_assistant_observed` | 观察到 assistant 响应 |
| `compaction_started` | 上下文压缩开始 |
| `compaction_settled` | 上下文压缩终局 |
| `context_generation_changed` | 上下文 generation 变化 |

### 子会话 Actor 事件（10 种）

`subsession_run_started`、`subsession_run_settled`、`subsession_turn_dispatch_requested`、`subsession_turn_started`、`subsession_turn_outcome_observed`、`subsession_turn_finished`、`subsession_abort_requested`、`subsession_session_poisoned`、`subsession_physical_session_closed`、`subsession_decision_committed`（crash-atomic 信封）。

### 万象阵事件（8 种）

`squad_created`、`tasks_created`、`task_started`、`task_submitted`、`task_merged`、`task_done`、`task_error`、`squad_cancelled`。物理路径见 `src/Runtime/Wanxiangzhen/SquadEventWanCodec.fs` + `CoordinatorReplay.fs`。

## WanEvent 信封

```fsharp
type WanEvent =
    { V: int; Session: string; Kind: string; At: string
      Payload: Map<string, string>
      EventId: string option; WriterId: string option
      Sequence: int option; Checksum: string option }
```

编解码：`src/Runtime/EventStore/EventLogCodec.fs`（`wanEventToLine`、`tryParseEventLine`、`computeEventChecksum`）。

## Fold 函数（`src/Kernel/EventSourcing/Fold.fs`）

`SessionState` 为 28 轴复合投影，`applyEvent` 为主折叠入口。各轴独立模块：

| 投影轴 | 模块 |
| :--- | :--- |
| Review loop | `ReviewLoopFold.fs` |
| Nudge dedup | `NudgeProjection.fs` |
| Nudge snapshot | `NudgeSnapshotProjection.fs` |
| Subagents | `SubsessionProjection.fs` |
| Human turn | `SessionControl/HumanTurn.fs` |
| Owner/Lease/Episode | `SessionControl/Projection.fs`、`LeaseTransitions.fs` |

## Subsession 决策信封

`subsession_decision_committed` 事件含 `events` JSON 数组（`SubsessionEventStore.fs`），原子打包多事件为一行。解码 `tryDecodeWanEventBatch` 拆分数组；任意失败 → `SessionPoisoned(EventStoreCorrupt)`。

## 写路径

```
EventWriter.appendXxxOrFail
  → EventLogRuntimeStore.appendAndCacheOrFail
    → EventStore.AppendEvent / AppendEventsOrFail
      → EventLogIo.appendLine（文件锁 + 追加）
      → Fold.applyEvent（更新进程内 SessionState）
```

## 读路径

`ReadAllEvents` → `EventLogRuntimeSync` → review/fallback projection 重建。损坏行截断：不跳过坏行继续 fold。

## Durable Effect Law 与 Outbox

持久化事件与发起外部效应之间存在崩溃窗口。外部副作用必须满足：
1. 从已提交状态确定性重建，或
2. 作为持久化 Outbox Intent 写入（与领域事件同一提交屏障落盘）

当前续命通过 `ContinuationIntentExecution` 驱动；Effect Supervisor 从 Outbox 消费 Intent 演进方向。

## 锁与并发

- 跨进程：文件排他锁（`EventLogLock.fs`）
- 进程内：`PromiseQueue.SerialQueue` 串行化 append

## 启动与恢复

1. 打开 workspace → `EventStore` 读 NDJSON → 构建 `SessionState` 缓存
2. `syncAllSessionsFromEventLog` 或单 session `syncReviewFromEventLog`
3. 再注册宿主 hook
