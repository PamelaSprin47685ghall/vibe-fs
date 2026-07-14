# 05 — 事件溯源与持久化

## SSOT

工作区根目录：

```text
[workspace]/.wanxiangshu.ndjson
[workspace]/.wanxiangshu.ndjson.lock
```

**唯一 durable 真相**（review task、backlog、nudge 去重等）。宿主 `session.messages`、compaction 后注入的 anchor **不是** SSOT。

## 六条纪律

1. **意图不落盘**：未校验的自然语言、草稿参数不写入。
2. **事实不可改**：仅追加行；修正靠补偿事件，非覆盖旧行。
3. **内存 = fold**：`ReviewStore`、backlog 投影、nudge 表 = 对 NDJSON 的纯 fold + 可选进程缓存。
4. **先盘后内存**：append 成功后才更新缓存投影；失败 = 命令未发生。
5. **一行一事件**：每行自包含 JSON（Shell 解析为 `WanEvent`）。
6. **按 session 分区**：每行含 `session` 字段；fold 过滤 `sessionId`。

## 事件种类（Kernel SSOT）

定义于 `src/Kernel/EventLog/Types.fs`。**所有事件种类均在此处以 `eventKind*` 常量定义**，Shell 层 codec 引用这些常量，宿主层不复制字符串。

### 核心业务事件

| `Kind` 常量 | 含义 |
| :--- | :--- |
| `loop_activated` | With-Review 激活，payload 含 `task` |
| `loop_cancelled` | 取消 loop |
| `review_verdict` | 审查结论（accepted / needs_revision / terminated / cancelled） |
| `work_backlog_committed` | todowrite/task 校验通过后全量 todos + 五报告 + `select_methodology` |
| `submit_review_wip_recorded` | WIP 提交记录 |
| `assistant_completed` | 助手轮次完成（辅助 nudge 快照，含 `agent` / `model` / `turnId` / `openTodosJson`） |
| `subagent_spawned` | 子代理 spawn 成功（payload：`childId` / `agent` / `title`） |
| `subagent_continued` | `continue` 工具续跑子会话（payload：`childId` / `prompt`） |

### nudge 生命周期事件（六阶段闭环）

| `Kind` 含义 | 触发时机 |
| :--- | :--- |
| `nudge_requested` | nudge 决策通过，进入串行 Claim 前（payload：`action` / `anchor` / `nudgeId` / `nonce` / `generation` / `cancelGeneration` / `humanTurnId` / `nudgeOrdinal`） |
| `nudge_dispatched` | nudge 已成功派发（payload：`action` / `anchor` / `nudgeId` / `nudgeOrdinal`） |
| `nudge_failed` | nudge 派发失败（payload：`nudgeId` / `error` / `nudgeOrdinal`） |
| `nudge_cancelled` | nudge 被取消（如新用户消息打断，payload：`nudgeId` / `reason` / `nudgeOrdinal`） |
| `nudge_settled` | nudge 终局（payload：`nudgeId` / `status` / `nudgeOrdinal`） |
| `nudge_dedup_cleared` | 去重表清空（如新用户消息或 WIP 提交） |

去重逻辑：`foldNudgeDedup` 在 `nudge_requested` 时记录 `PendingNudge`，`nudge_dispatched` 时记 `LastDispatchedAnchor`；`nudge_dedup_cleared` / `submit_review_wip_recorded` / `human_turn_started` 清空两者。`isNudgeBlockedForAnchor` 检查当前 anchor 是否已在 Pending 或 LastDispatched 中。

### 人机交互轮次事件

| `Kind` 含义 | 触发时机 |
| :--- | :--- |
| `human_turn_started` | 用户新消息开始（payload：`turnId` / `provider` / `model` / `variant` / `agent` / `humanTurnOrdinal` / `messageId`） |
| `user_abort_observed` | 用户中断观察（payload 空） |

### 模型降级（Fallback）续命事件

| `Kind` 含义 | 触发时机 |
| :--- | :--- |
| `continuation_requested` | 降级决策选好模型，即将发起续命（payload：`continuationId` / `model` / `agent` / `at` / `generation` / `cancelGeneration` / `humanTurnId` / `owner` / `continuationOrdinal`） |
| `continuation_dispatch_started` | 续命已进入宿主 API 调用前（payload：`continuationId` / `continuationOrdinal`） |
| `continuation_dispatched` | 续命已成功派发（payload：`continuationId` / `model` / `agent` / `at` / `continuationOrdinal`） |
| `continuation_failed` | 续命失败（payload：`continuationId` / `error` / `continuationOrdinal`） |
| `continuation_cancelled` | 续命被取消（payload：`continuationId` / `reason` / `continuationOrdinal`） |
| `continuation_settled` | 续命终局（payload：`continuationId` / `humanTurnId` / `generation` / `status` / `continuationOrdinal`） |

`fallback_continue_injected`（旧版，payload：`model` / `agent` / `at`）仍保留但已逐步被上述六阶段续命事件取代。

### 上下文压缩（Compaction）事件

| `Kind` 含义 | 触发时机 |
| :--- | :--- |
| `compaction_started` | 宿主 compaction 开始（payload：`compactionId` / `generationAtStart` / `humanTurnId` / `compactionOrdinal`） |
| `compaction_settled` | compaction 完成或取消（payload：`compactionId` / `status` / `compactionOrdinal`） |
| `context_generation_changed` | compaction 后上下文代数变更（payload：`generation`） |

### 子会话 Actor 事件（Subsession）

| `Kind` 含义 | 触发时机 |
| :--- | :--- |
| `subsession_run_started` | 子会话 run 开始（payload：`childId` / `parentSessionId` / `runId`） |
| `subsession_run_settled` | 子会话 run 终局（payload：`childId` / `runId` / `status` / `detail`） |
| `subsession_turn_dispatch_requested` | 子会话 turn 派发请求（payload：`runId` / `turnId` / `turnOrdinal` / `model` / `prompt`） |
| `subsession_turn_started` | 子会话 turn 开始（payload：`runId` / `turnId` / `receipt`） |
| `subsession_turn_outcome_observed` | 子会话 turn 结果观察 |
| `subsession_turn_finished` | 子会话 turn 结束（payload：`finish` / `errorName` / `message` / `output`） |
| `subsession_abort_requested` | 子会话 abort 请求 |
| `subsession_session_poisoned` | 子会话中毒（payload：`reason`） |
| `subsession_physical_session_closed` | 子会话物理 session 关闭 |
| `subsession_decision_committed` | 子会话决策已持久化（原子信封，内含 `events` JSON 数组） |

### 万象阵事件

| `Kind` 含义 | 触发时机 |
| :--- | :--- |
| `squad_created` | 万象阵 Session 创建 |
| `tasks_created` | DAG 拆解产物 |
| `task_started` | worktree 创建 + slave 启动 |
| `task_submitted` | slave 调用 submit |
| `task_merged` | ff 合并成功 |
| `task_done` | slave 进程退出 |
| `task_error` | git/worktree 操作失败 |
| `squad_cancelled` | /squad-kill 触发 |

**万象阵** kind（同文件、`session` = 万象阵 session id）：`squad_created`、`tasks_created`、`task_started`、`task_submitted`、`task_merged`、`task_done`、`task_error`、`squad_cancelled`。运行时经 `AppendSquadEvent` **追加到同一文件** `[workspace]/.wanxiangshu.ndjson`（与万象术事件共用锁与 `EventLogStore`）；DAG fold 在 `Kernel/Wanxiangzhen` + `Shell/EventLogSquadProjection`。规格叙事见 [wanxiangzhen/02-event-sourcing.md](./wanxiangzhen/02-event-sourcing.md)（物理路径以 `EventLogCodec.eventLogFileName` 为准）。

## 信封字段（概念）

与 PRD-02 一致：`v`、`session`、`kind`、`at`、`payload`、`id`、`host` 等由 Shell codec 序列化；Kernel `WanEvent` 使用 `Map<string,string>` payload 参与 fold。

## Fold 函数

见 `src/Kernel/EventLog/Fold.fs`：

- **`foldReviewTask`**：`loop_activated` 设 task；`review_verdict` 终局 verdict 清空；`loop_cancelled` 清空。
- **`foldWorkBacklogSnapshot`**：取最后一次 `work_backlog_committed`。
- **`foldNudgeDedup`**：记录已派发 anchor；WIP / dedup_cleared 重置策略。
- **`foldNudgeSnapshot`**：供 nudge 决策的聚合视图（open todos、loop 是否活跃等）。
- **`foldSubagents`** / `SessionState.Subagents`：`subagent_spawned` / `subagent_continued` 投影。
- **`foldFallbackInjection`** / `SessionState.FallbackInjection`：`fallback_continue_injected`（见 [12-fallback.md](./12-fallback.md)）。
- **万象阵**：`EventLogSquadProjection.applyWanEvent` 与 `CoordinatorReplay`（读同一 NDJSON）。

`SessionState`（Shell 缓存）聚合上述投影，供 `EventLogRuntimeNudge` / `Sync` 读取。

## 写入 API（Shell）

`EventLogRuntimeAppend.fs` 暴露业务级 `appendLoopActivated`、`appendWorkBacklogCommitted`、`appendReviewVerdict` 等；内部统一 `appendAndCache`。

**`work_backlog_committed`**：在 `todowrite`/`task` 工具校验通过后调用，payload 由 `TodoWriteArgs` 构造。

## 锁与并发

- 跨进程：`O_CREAT|O_EXCL` 锁文件（架构测试 `eventLogUsesAdvisoryFlock` / proper-lockfile 策略以代码为准）。
- 进程内：`PromiseQueue.SerialQueue` 串行化 append。

## 损坏行处理

读盘时遇到截断/非法行：**在该行截断**，丢弃该行及之后字节，不跳过坏行继续 fold（避免建在错误基线上的状态）。

## 启动与恢复

1. 打开 workspace → `EventLogStore` 读 NDJSON → 构建 `SessionState` 缓存  
2. `syncAllSessionsFromEventLog` / 单 session `syncReviewFromEventLog`、`syncBacklogFromEventLog`  
3. 再注册宿主 hook  

**文本回放备选**：`ReviewReplaySync.syncReviewFromTexts` 从对话文本推断 task，仅作 fallback；首选 **事件重放**（`EventLogRuntimeSync`）。

## 与 compaction 的关系

宿主压缩上下文时，万象术**不**依赖「compaction 前消息」或 multi-frontmatter 锚点恢复 backlog/review。展示层仍可输出 front-matter 供 LLM 阅读，但程序判断以 fold 为准。

## 验证

- 单元：`tests/` 中 EventLog fold / codec 测试
- 架构：`nudgeDedupMustUseEventLogFold`、`nudgeLoopStateMustReplayHistory`
- 关键套件：`EventLogFoldTests`、`EventLogCodecTests`、`EventLogRuntimeTests`、`NudgeEventSourcingTests`

## 相关文档

- [06-review-and-nudge.md](./06-review-and-nudge.md)
- [07-work-backlog.md](./07-work-backlog.md)
- [18-glossary-and-ssot-map.md](./18-glossary-and-ssot-map.md)